using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    static class RunCommandUtils
    {
        internal const string k_DynamicAssemblyName = "Unity.AI.Assistant.Agent.Dynamic.Extension.Editor";
        internal const string k_DynamicCommandNamespace = "Unity.AI.Assistant.Agent.Dynamic.Extension.Editor";
        internal const string k_DynamicCommandClassName = "CommandScript";

        internal static readonly Regex k_CsxMarkupRegex =
            new("```csx(.*?)```", RegexOptions.Compiled | RegexOptions.Singleline);

        const string k_DynamicCommandFullClassName = k_DynamicCommandNamespace + "." + k_DynamicCommandClassName;

        const string k_DummyCommandScript =
            "\nusing UnityEngine;\nusing UnityEditor;\n\ninternal class CommandScript : IRunCommand\n{\n    public void Execute(ExecutionResult result) {}\n    public void Preview(PreviewBuilder builder) {}\n}";


        static readonly DynamicAssemblyBuilder m_Builder = new(k_DynamicAssemblyName, k_DynamicCommandNamespace);

        static RunCommandUtils()
        {
            Task.Run(() => m_Builder.Compile(k_DummyCommandScript, out _));
        }

        internal static AgentRunCommand BuildRunCommand(string commandScript, IEnumerable<Object> contextAttachments)
        {
            commandScript = ScriptPreProcessor(commandScript, out var embeddedMonoBehaviours);

            var compilationSuccessful =
                m_Builder.TryCompileCode(commandScript, out var compilationLogs, out var compilation);
            var updatedScript = compilation.GetSourceCode();
            var runCommand =
                new AgentRunCommand(contextAttachments.ToList()) { CompilationErrors = compilationLogs, Script = updatedScript };

            if (runCommand.HasUnauthorizedNamespaceUsage())
            {
                runCommand.CompilationSuccess = false;
            }
            else if (compilationSuccessful)
            {
                runCommand.CompilationSuccess = true;
                runCommand.Initialize(compilation, embeddedMonoBehaviours);
            }
            else
            {
                InternalLog.LogWarning($"Unable to compile the command:\n{compilationLogs}");
            }

            return runCommand;
        }

        static string ScriptPreProcessor(string commandScript, out List<ClassCodeTextDefinition> additionalScripts)
        {
            var tree = SyntaxFactory.ParseSyntaxTree(commandScript);

            // Remove embedded MonoBehaviours that already exist in the project
            additionalScripts = tree.ExtractTypesByInheritance<MonoBehaviour>(out var usingDirectives)
                .ChangeModifiersToPublic().ToCodeTextDefinition(usingDirectives);
            for (var i = additionalScripts.Count - 1; i >= 0; i--)
            {
                var monoBehaviour = additionalScripts[i];
                if (UserAssemblyContainsType(monoBehaviour.ClassName))
                {
                    tree = tree.RemoveType(monoBehaviour.ClassName);
                    additionalScripts.RemoveAt(i);
                }
            }

            return tree.GetText().ToString();
        }

        static bool UserAssemblyContainsType(string typeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assemblyCSharp = assemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");

            if (assemblyCSharp != null)
            {
                var type = assemblyCSharp.GetType(typeName);
                return type != null;
            }

            return false;
        }

        internal static ExecutionResult Execute(AgentRunCommand command)
        {
            using var stream = new MemoryStream();
            var result = command.Compilation.Emit(stream);
            if (!result.Success)
            {
                return new ExecutionResult(command.Title) { SuccessfullyStarted = false };
            }

            stream.Seek(0, SeekOrigin.Begin);
            var agentAssembly = m_Builder.LoadAssembly(stream);
            var commandInstance = CreateRunCommandInstance(agentAssembly);
            command.SetInstance(commandInstance);

            command.Execute(out var executionResult);

            return executionResult;
        }

        internal static IRunCommand CreateRunCommandInstance(Assembly dynamicAssembly)
        {
            var type = dynamicAssembly.GetType(k_DynamicCommandFullClassName);
            if (type == null)
                return null;

            return Activator.CreateInstance(type) as IRunCommand;
        }
    }
}
