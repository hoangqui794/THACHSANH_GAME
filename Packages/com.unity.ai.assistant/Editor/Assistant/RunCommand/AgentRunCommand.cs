using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Editor.Utils;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    class AgentRunCommand
    {
        IRunCommand m_ActionInstance;
        RunCommandMetadata m_Metadata;
        List<ClassCodeTextDefinition> m_RequiredMonoBehaviours = new();

        public string Script { get; set; }
        public CompilationErrors CompilationErrors { get; set; }

        public string Title => m_Metadata.Title;

        public string Description => m_Metadata.Description;

        public bool Unsafe => m_Metadata.IsUnsafe;

        public bool MetadataCreated { get; private set; }

        public IRunCommand Instance => m_ActionInstance;

        public bool CompilationSuccess;
        internal CSharpCompilation Compilation { get; set; }

        public IEnumerable<ClassCodeTextDefinition> RequiredMonoBehaviours => m_RequiredMonoBehaviours;

        public readonly List<Object> CommandAttachments;

        public AgentRunCommand(List<Object> contextAttachments)
        {
            CommandAttachments =
                new List<Object>(AttachmentUtils.GetValidAttachment(contextAttachments));
        }

        public void Initialize(CSharpCompilation compilation, List<ClassCodeTextDefinition> embeddedMonoBehaviours)
        {
            Compilation = compilation;

            m_Metadata = RunCommandCodeAnalyzer.AnalyzeCommandAndExtractMetadata(Compilation);
            InitializeParameters();

            SetRequiredMonoBehaviours(embeddedMonoBehaviours);
        }

        void InitializeParameters()
        {
            foreach (var fieldInfo in m_Metadata.Parameters.Values)
            {
                var fieldType = fieldInfo.Type;
                if (typeof(Object).IsAssignableFrom(fieldType))
                {
                    Object defaultValue = null;
                    switch (fieldInfo.LookupType)
                    {
                        case LookupType.Attachment:
                            defaultValue = GetAttachmentByNameOrFirstCompatible(fieldInfo.LookupName, fieldType);
                            break;
                        case LookupType.Scene:
                            defaultValue = GameObject.Find(fieldInfo.LookupName);
                            break;
                        case LookupType.Asset:
                            defaultValue = AssetDatabase.LoadAssetAtPath(fieldInfo.LookupName, fieldType);
                            break;
                    }

                    fieldInfo.SetValue(defaultValue);
                }
                else if (fieldType.IsGenericType &&
                         fieldType.GetGenericTypeDefinition() == typeof(List<>) &&
                         typeof(Object).IsAssignableFrom(fieldType.GetGenericArguments()[0]))
                {
                    var elementType = fieldType.GetGenericArguments()[0];
                    var listInstance = Activator.CreateInstance(fieldType) as IList;

                    switch (fieldInfo.LookupType)
                    {
                        case LookupType.Attachment:
                            var attachments = GetAttachments(elementType);
                            foreach (var attachment in attachments)
                                listInstance?.Add(attachment);
                            break;
                        case LookupType.Asset:
                            var assets = AssetDatabase.LoadAllAssetsAtPath(fieldInfo.LookupName);
                            listInstance = assets
                                .Where(asset => elementType != null && elementType.IsInstanceOfType(asset)).ToList();
                            break;
                        case LookupType.Scene:
                            Debug.LogWarning("List of GameObject in the scene is not supported.");
                            break;
                    }

                    fieldInfo.SetValue(listInstance);
                }
                else if (fieldType.IsArray &&
                         typeof(Object).IsAssignableFrom(fieldType.GetElementType()))
                {
                    var elementType = fieldType.GetElementType();
                    Object[] arrayInstance = null;

                    switch (fieldInfo.LookupType)
                    {
                        case LookupType.Attachment:
                            var attachments = GetAttachments(elementType);
                            arrayInstance = attachments.ToArray();
                            break;
                        case LookupType.Asset:
                            var assets = AssetDatabase.LoadAllAssetsAtPath(fieldInfo.LookupName);
                            arrayInstance = assets
                                .Where(asset => elementType != null && elementType.IsInstanceOfType(asset)).ToArray();
                            break;
                        case LookupType.Scene:
                            Debug.LogWarning("Array of GameObject in the scene is not supported.");
                            break;
                    }

                    if (arrayInstance != null)
                    {
                        var typedArray = Array.CreateInstance(elementType, arrayInstance.Length);
                        for (int i = 0; i < arrayInstance.Length; i++)
                            typedArray.SetValue(arrayInstance[i], i);

                        fieldInfo.SetValue(typedArray);
                    }
                }
            }
        }

        public bool Execute(out ExecutionResult executionResult)
        {
            executionResult = new ExecutionResult(Title);

            if (m_ActionInstance == null)
                return false;

            executionResult.Start();

            try
            {
                m_ActionInstance.Execute(executionResult);

                // Unsafe actions usually mean deleting things - so we need to update the project view afterwards
                if (Unsafe)
                {
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                executionResult.LogError(e.ToString());
            }

            executionResult.End();

            return true;
        }

        public string[] GetPreviewSteps()
        {
            var previewSteps = new List<string>();

            MetadataCreated = true;

            var previewMessages = Description.Split('\n');
            foreach (var msg in previewMessages)
                previewSteps.Add(msg);

            return previewSteps.ToArray();
        }

        void SetRequiredMonoBehaviours(IEnumerable<ClassCodeTextDefinition> newBehaviors)
        {
            m_RequiredMonoBehaviours.Clear();
            m_RequiredMonoBehaviours.AddRange(newBehaviors);
        }

        public bool HasUnauthorizedNamespaceUsage()
        {
            return RunCommandCodeAnalyzer.HasUnauthorizedNamespaceUsage(Script);
        }

        IEnumerable<Object> GetAttachments(Type type)
        {
            var isComponentType = typeof(Component).IsAssignableFrom(type);

            foreach (var obj in CommandAttachments)
            {
                if (isComponentType && obj is GameObject go)
                {
                    var comp = go.GetComponent(type);
                    if (comp != null)
                        yield return comp;
                }
                else
                {
                    if (type.IsAssignableFrom(obj.GetType()))
                        yield return obj;
                }
            }
        }

        public Object GetAttachmentByNameOrFirstCompatible(string objectName, Type type)
        {
            var filtered = GetAttachments(type);
            if (string.IsNullOrEmpty(objectName))
                return filtered.FirstOrDefault();

            var objectByName = filtered.FirstOrDefault(a => a.name == objectName);
            return objectByName;
        }

        public RunCommandMetadata.CommandParameterInfo GetCommandParameter(string fieldName)
        {
            return m_Metadata.Parameters.GetValueOrDefault(fieldName);
        }

        public void SetInstance(IRunCommand commandInstance)
        {
            m_ActionInstance = commandInstance;

            // Initialize command parameter fields with stored Metadata value
            var instanceType = m_ActionInstance.GetType();
            var fields = instanceType.GetFields(System.Reflection.BindingFlags.Instance |
                                                System.Reflection.BindingFlags.Public |
                                                System.Reflection.BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                var fieldName = field.Name;
                if (!m_Metadata.Parameters.TryGetValue(fieldName, out var parameterInfo))
                    continue;

                field.SetValue(m_ActionInstance, parameterInfo.Value);
            }
        }
    }
}
