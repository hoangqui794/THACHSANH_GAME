using System;
using System.IO;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    [Serializable]
    class AssistantProjectSettings
    {
        public string CustomInstructionsGUID;
    }

    static class AssistantProjectPreferences
    {
        static readonly string k_SettingsPath =
            Path.Combine("ProjectSettings", "Packages", AssistantConstants.PackageName, "Settings.json");

        internal static Action CustomInstructionsFilePathChanged;

        static AssistantProjectSettings s_Settings;

        internal static AssistantProjectSettings Settings
        {
            get
            {
                if (s_Settings == null)
                {
                    if (!File.Exists(k_SettingsPath))
                    {
                        s_Settings = new AssistantProjectSettings();
                        Save();
                    }
                    else
                    {
                        try
                        {
                            var json = File.ReadAllText(k_SettingsPath);
                            s_Settings = JsonUtility.FromJson<AssistantProjectSettings>(json);
                        }
                        catch (Exception ex)
                        {
                            InternalLog.LogException(ex);
                            s_Settings = new AssistantProjectSettings();
                        }
                    }
                }

                return s_Settings;
            }
        }

        static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(k_SettingsPath));

                var json = JsonUtility.ToJson(s_Settings, true);
                File.WriteAllText(k_SettingsPath, json);
            }
            catch (Exception ex)
            {
                InternalLog.LogException(ex);
            }
        }

        public static string CustomInstructionsFilePath
        {
            get
            {
                var guidString = Settings.CustomInstructionsGUID;
                if (GUID.TryParse(guidString, out var guid))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    return path;
                }

                return null;
            }
            set
            {
                var guid = AssetDatabase.AssetPathToGUID(value);

                Settings.CustomInstructionsGUID = guid;
                Save();

                CustomInstructionsFilePathChanged?.Invoke();
            }
        }
    }
}
