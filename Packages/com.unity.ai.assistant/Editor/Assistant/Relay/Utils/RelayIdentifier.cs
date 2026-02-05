using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace Unity.Relay.Editor
{
    /// <summary>
    /// Provides unique identification for each Unity Editor instance using process ID
    /// </summary>
    [InitializeOnLoad]
    static class RelayIdentifier
    {
        const string k_RelayPortPrefix = "RELAY-PORT";
        const string k_RelayProcessIdPrefix = "RELAY-PID";

        public static int EditorProcessId { get; }
        static int RelayPort { get; set; }
        public static int RelayProcessId { get; set; }

        static RelayIdentifier()
        {
            EditorProcessId = Process.GetCurrentProcess().Id;

            string portKey = $"{k_RelayPortPrefix}{EditorProcessId}";
            RelayPort = EditorPrefs.GetInt(portKey, 0);

            string pidKey = $"{k_RelayProcessIdPrefix}{EditorProcessId}";
            RelayProcessId = EditorPrefs.GetInt(pidKey, 0);
        }

        /// <summary>
        /// Get the relay port for this editor instance
        /// </summary>
        public static int GetRelayPort()
        {
            return RelayPort;
        }

        /// <summary>
        /// Set the relay port for this editor instance
        /// </summary>
        public static void SetRelayPort(int port)
        {
            string key = $"{k_RelayPortPrefix}{EditorProcessId}";
            EditorPrefs.SetInt(key, port);
            RelayPort = port;
        }

        /// <summary>
        /// Clear the relay port for this editor instance
        /// </summary>
        public static void ClearRelayPort()
        {
            string key = $"{k_RelayPortPrefix}{EditorProcessId}";
            EditorPrefs.DeleteKey(key);
            RelayPort = 0;
        }

        /// <summary>
        /// Get the relay server process ID for this editor instance
        /// </summary>
        public static int GetRelayProcessId()
        {
            return RelayProcessId;
        }

        /// <summary>
        /// Set the relay server process ID for this editor instance
        /// </summary>
        public static void SetRelayProcessId(int processId)
        {
            string key = $"{k_RelayProcessIdPrefix}{EditorProcessId}";
            EditorPrefs.SetInt(key, processId);
            RelayProcessId = processId;
        }

        /// <summary>
        /// Clear the relay server process ID for this editor instance
        /// </summary>
        public static void ClearRelayProcessId()
        {
            string key = $"{k_RelayProcessIdPrefix}{EditorProcessId}";
            EditorPrefs.DeleteKey(key);
            RelayProcessId = 0;
        }
    }
}
