using System.IO;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AssistantProjectSettingsPage : ManagedTemplate
    {
        const string k_DefaultCustomInstructionsFileName = "Assets/AssistantCustomInstructions.txt";

        static readonly string k_FileLimitsTooltip =
            $"The guidelines will be limited to {AssistantConstants.UserGuidelineCharacterLimit:N0} characters";

        static readonly string k_CustomInstructionsTooLongWarning =
            $"The custom instructions exceed the limit of {AssistantConstants.UserGuidelineCharacterLimit:N0} characters. Shorten the content for better responses.";

        ObjectField m_CustomInstructionsField;

        VisualElement m_CustomInstructionsTooLongWarning;
        Button m_NewButton;

        TemplateContainer m_View;

        public AssistantProjectSettingsPage() :
            base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_View = view;

            LoadStyle(view,
                EditorGUIUtility.isProSkin
                    ? AssistantUIConstants.AssistantSharedStyleDark
                    : AssistantUIConstants.AssistantSharedStyleLight);
            LoadStyle(view, AssistantUIConstants.AssistantBaseStyle, true);

            m_CustomInstructionsField = view.Q<ObjectField>("customInstructionsField");
            m_CustomInstructionsField.allowSceneObjects = false;
            m_CustomInstructionsField.objectType = typeof(TextAsset);
            m_CustomInstructionsField.RegisterValueChangedCallback(CustomInstructionsValueChanged);

            m_CustomInstructionsTooLongWarning = view.Q<VisualElement>("customInstructionsTooLongWarningContainer");
            m_CustomInstructionsTooLongWarning.SetDisplay(false);

            var customInstructionsTooLongWarningText = m_View.Q<Label>("customInstructionsTooLongWarningText");
            customInstructionsTooLongWarningText.text = k_CustomInstructionsTooLongWarning;
            
            m_NewButton = m_View.SetupButton("newCustomInstructionsButton", NewCustomInstructions);

            // Callback on focus:
            m_View.RegisterCallback<FocusInEvent>(_ =>
            {
                RefreshCustomInstructions();
            });

            m_CustomInstructionsField.tooltip = k_FileLimitsTooltip;

            RefreshCustomInstructions();
            
            RegisterAttachEvents(OnAttach, OnDetach);
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            AssistantProjectPreferences.CustomInstructionsFilePathChanged -= OnCustomInstructionsPathChanged;
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            AssistantProjectPreferences.CustomInstructionsFilePathChanged += OnCustomInstructionsPathChanged;
            RefreshCustomInstructions();
        }

        void CustomInstructionsValueChanged(ChangeEvent<Object> evt)
        {
            var asset = evt.newValue as TextAsset;
            if (asset == null)
            {
                AssistantProjectPreferences.CustomInstructionsFilePath = null;
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);

            AssistantProjectPreferences.CustomInstructionsFilePath = assetPath;
        }

        void RefreshCustomInstructions()
        {
            var customInstructionsPath = AssistantProjectPreferences.CustomInstructionsFilePath;
            var instructions = AssetDatabase.LoadAssetAtPath<TextAsset>(customInstructionsPath);

            m_CustomInstructionsField.SetValueWithoutNotify(instructions);

            // Check if file contents exceed limits
            m_CustomInstructionsTooLongWarning.SetDisplay(instructions != null &&
                                                          instructions.text.Length >
                                                          AssistantConstants.UserGuidelineCharacterLimit);

            m_NewButton.SetDisplay(instructions == null);
        }

        void OnCustomInstructionsPathChanged()
        {
            RefreshCustomInstructions();
        }

        void NewCustomInstructions(PointerUpEvent evt)
        {
            var assetPath = AssetDatabase.GenerateUniqueAssetPath(k_DefaultCustomInstructionsFileName);
            File.WriteAllText(assetPath, "");
            AssetDatabase.ImportAsset(assetPath);

            AssistantProjectPreferences.CustomInstructionsFilePath = assetPath;

            // Highlight the newly created asset
            var createdAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            EditorGUIUtility.PingObject(createdAsset);
        }
    }
}
