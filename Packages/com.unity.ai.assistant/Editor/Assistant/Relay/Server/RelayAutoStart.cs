using System.Threading.Tasks;
using Unity.AI.Assistant.Utils;
using UnityEngine;
using UnityEditor;

namespace Unity.Relay.Editor
{
    /// <summary>
    /// Automatically start the persistent Relay Application when Unity loads
    /// </summary>
    [InitializeOnLoad]
    static class RelayAutoStart
    {
        static RelayAutoStart()
        {
            EditorApplication.delayCall += InitializeConnection;
        }

        static async void InitializeConnection()
        {
            try
            {
                InternalLog.Log("[RelayAutoStart] Initializing persistent Relay connection...");

                await RelayProcessManager.RetrieveProcessOrStart();

                // Initialize local connection with the Relay (auto-connect)
                RelayConnection.Instance.Initialize();
            }
            catch (System.Exception ex)
            {
                InternalLog.LogError($"[RelayAutoStart] Error initializing connection: {ex.Message}");
            }
        }
    }
}
