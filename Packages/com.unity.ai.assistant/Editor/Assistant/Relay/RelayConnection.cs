using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.Utils;

namespace Unity.Relay.Editor
{
    /// <summary>
    /// Singleton manager for persistent WebSocket connection to Relay server.
    /// Maintains connection independently of UI windows and handles auto-reconnection.
    /// </summary>
    class RelayConnection
    {
        static RelayConnection s_Instance;

        WebSocketRelayClient m_Client;
        bool m_IsEnabled = true;
        bool m_IsReconnecting;
        float m_LastConnectionAttemptTime;
        const float k_ReconnectInterval = 5.0f; // seconds
        string m_ServerAddress;

        public static RelayConnection Instance
        {
            get
            {
                s_Instance ??= new RelayConnection();
                return s_Instance;
            }
        }

        public WebSocketRelayClient Client => m_Client;
        public bool IsConnected => m_Client?.IsConnected ?? false;

        // Events for connection state changes
        public event Action OnConnected;
        public event Action OnDisconnected;

        public void Initialize()
        {
            // Subscribe to Unity update loop for reconnection handling
            EditorApplication.update += Update;

            // Subscribe to Unity quit events to shutdown server
            EditorApplication.quitting += OnEditorQuitting;

            // Start initial connection attempt
            EditorApplication.delayCall += () => _ = ConnectAsync();

            ProjectScriptCompilation.OnRequestReload += SendWaitingDomainReloadMessage;
        }

        async Task ConnectAsync()
        {
            try
            {
                // Track the last time a connection was attempted to inform reconnection if it's time to reconnect
                m_LastConnectionAttemptTime = (float)EditorApplication.timeSinceStartup;
                // Dispose existing client
                if (m_Client != null)
                {
                    m_Client.Dispose();
                    m_Client = null;
                }

                // Get the server address for this editor instance
                m_ServerAddress = GetServerAddress();

                // Create and configure client
                m_Client = new WebSocketRelayClient();
                SetupClientEvents();

                // Attempt connection
                bool connected = await m_Client.ConnectAsync(m_ServerAddress);
                if (connected)
                {
                    InternalLog.Log($"[RelayConnection] Connection successful");
                }
                else
                {
                    InternalLog.LogWarning($"[RelayConnection] Connection failed - will retry automatically");
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[RelayConnection] Connection error: {ex.Message}");
            }
        }

        string GetServerAddress()
        {
            return $"ws://127.0.0.1:{RelayProcessManager.ServerPort}";
        }

        void Update()
        {
            if (!m_IsEnabled) return;

            // Handle automatic reconnection
            if (m_Client is { IsConnected: false } && !m_IsReconnecting)
            {
                float currentTime = (float)EditorApplication.timeSinceStartup;
                if (currentTime - m_LastConnectionAttemptTime > k_ReconnectInterval)
                {
                    ReconnectAsync();
                }
            }
        }

        void SetupClientEvents()
        {
            if (m_Client == null) return;

            // Subscribe to client events
            m_Client.OnConnected += () => {
                InternalLog.Log("[RelayConnection] WebSocket connected successfully");
                OnConnected?.Invoke();
            };

            m_Client.OnDisconnected += () => {
                InternalLog.LogWarning("[RelayConnection] WebSocket disconnected - will attempt reconnection");
                OnDisconnected?.Invoke();
            };
        }

        async void ReconnectAsync()
        {
            if (m_IsReconnecting) return;

            m_IsReconnecting = true;

            try
            {
                await ConnectAsync();
            }
            finally
            {
                m_IsReconnecting = false;
            }
        }

        void OnEditorQuitting()
        {
            // Send shutdown signal to server before Unity closes (fire-and-forget)
            if (m_Client?.IsConnected == true)
            {
                try
                {
                    // Fire-and-forget approach - don't wait for response to avoid Unity freeze
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await m_Client.ShutdownServerAsync();
                            InternalLog.Log("[RelayConnection] Server shutdown signal sent");
                        }
                        catch (Exception ex)
                        {
                            InternalLog.LogError($"[RelayConnection] Error sending shutdown signal: {ex.Message}");
                        }
                    });

                    // Give a brief moment for the message to be sent
                    System.Threading.Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    InternalLog.LogError($"[RelayConnection] Error initiating shutdown signal: {ex.Message}");
                }
            }

            // Clean up connection and port assignment immediately
            Close();
            RelayProcessManager.CleanupProcess();
        }

        void SendWaitingDomainReloadMessage()
        {
            if (m_Client?.IsConnected == true)
            {
                // Wait for the message to be sent
                Task.Run(async () =>
                {
                    try
                    {
                        await m_Client.SendWaitingDomainReloadAsync();
                    }
                    catch (Exception ex)
                    {
                        InternalLog.LogError($"[RelayConnection] Error sending RELAY_BLOCK_INCOMING_CLOUD_MESSAGES message: {ex.Message}");
                    }
                }).Wait();
            }
        }

        /// <summary>
        /// Request relay server to replay incomplete message through normal streaming route
        /// </summary>
        public async Task<bool> ReplayIncompleteMessageAsync()
        {
            if (m_Client?.IsConnected == true)
            {
                try
                {
                    return await m_Client.ReplayIncompleteMessageAsync();
                }
                catch (Exception ex)
                {
                    InternalLog.LogError($"[RelayConnection] Error replaying incomplete message: {ex.Message}");
                    return false;
                }
            }

            InternalLog.LogWarning("[RelayConnection] Not connected to relay server");
            return false;
        }

        void Close()
        {
            m_IsEnabled = false;

            if (m_Client != null)
            {
                m_Client.Dispose();
                m_Client = null;
            }

            EditorApplication.update -= Update;
            EditorApplication.quitting -= OnEditorQuitting;
        }
    }
}
