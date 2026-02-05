using UnityEditor;
using System;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// User preferences shown in Edit -> Preferences...
    /// </summary>
    static partial class AssistantEditorPreferences
    {
        const string k_SettingsPrefix = "AIAssistant.";
        const string k_SendPromptModifierKey = k_SettingsPrefix + "SendPromptUseModifierKey";
        const string k_AutoRun = k_SettingsPrefix + "AutoRun";
        const string k_CollapseReasoningWhenComplete = k_SettingsPrefix + "CollapseReasoningWhenComplete";

        public static event Action<bool> UseModifierKeyToSendPromptChanged;
        public static event Action<bool> AutoRunChanged;
        public static event Action<bool> CollapseReasoningWhenCompleteChanged;

        /// <summary>
        /// When true, pressing enter with modifier key (ex: ctrl) sends the prompt without having to click the button to send the prompt
        /// </summary>
        public static bool UseModifierKeyToSendPrompt
        {
            get => EditorPrefs.GetBool(k_SendPromptModifierKey, false);
            set
            {
                if (UseModifierKeyToSendPrompt != value)
                {
                    EditorPrefs.SetBool(k_SendPromptModifierKey, value);
                    UseModifierKeyToSendPromptChanged?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// When enabled, do not ask the user for permissions.
        /// </summary>
        public static bool AutoRun
        {
            get => EditorPrefs.GetBool(k_AutoRun, false);
            set
            {
                if (AutoRun != value)
                {
                    EditorPrefs.SetBool(k_AutoRun, value);
                    AutoRunChanged?.Invoke(value);
                }
            }
        }

        /// <summary>
        /// If true, when the reasoning is completed, it'll collapse the reasoning section.
        /// </summary>
        public static bool CollapseReasoningWhenComplete
        {
            get => EditorPrefs.GetBool(k_CollapseReasoningWhenComplete, false);
            set
            {
                if (CollapseReasoningWhenComplete != value)
                {
                    EditorPrefs.SetBool(k_CollapseReasoningWhenComplete, value);
                    CollapseReasoningWhenCompleteChanged?.Invoke(value);
                }
            }
        }
    }
}
