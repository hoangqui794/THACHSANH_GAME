using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Unity.AI.Assistant.Utils;
using Debug = UnityEngine.Debug;

namespace Unity.Relay.Editor
{
    /// <summary>
    /// Manage lifecycle of the Relay process
    /// </summary>
    static class RelayProcessManager
    {
        class ServerValidationResponse
        {
            public string type { get; set; }
            public string status { get; set; }
            public bool serverReady { get; set; }
        }

        public struct RelayProcessStatus
        {
            /// <summary>
            /// Has the Relay been started. I.E. has a start up attempt been made
            /// </summary>
            public bool IsStarted;

            /// <summary>
            /// Is the Relay currently running? If the Relay <see cref="IsStarted"/> and is not <see cref="IsRunning"/>,
            /// then the process has exited since startup.
            /// </summary>
            public bool IsRunning;

            /// <summary>
            /// The message provided on exit after starting
            /// </summary>
            public string ExitMessage;
        }

        const int k_StartPort = 9001;
        const int k_MaxPort = 9100;
        const int k_AutoShutdownDelaySeconds = 180;

        static readonly string k_RelayPath = Path.GetFullPath("Packages/com.unity.ai.assistant/RelayApp~");

        static int s_ServerPort;
        static Process s_ProcessHandle;

        public static int ServerPort => s_ServerPort;

        public static RelayProcessStatus ProcessStatus;

        static RelayProcessManager()
        {
            // Cache port number
            s_ServerPort = RelayIdentifier.GetRelayPort();
        }

        /// <summary>
        /// Internal method to start the Relay server
        /// </summary>
        public static async Task RetrieveProcessOrStart()
        {
            if (!HasProcessHandle())
            {
                if (TryRetrieveProcess(RelayIdentifier.GetRelayProcessId()))
                {
                    ProcessStatus = new RelayProcessStatus()
                    {
                        IsStarted = true,
                        IsRunning = true
                    };

                    return; // Process was retrieved, no need to start
                }
            }

            if (string.IsNullOrEmpty(k_RelayPath) || !Directory.Exists(k_RelayPath))
            {
                Debug.LogError($"[RelayProcessManager] Server path not found: {k_RelayPath}");
                return;
            }

            try
            {
                // Find platform-specific executable
                string relayExecutable = GetRelayExecutablePath();

                // There is a risk that an old server is unpacked and never removed from the file system. Because of
                // this it is safer to clean up and unpack the server every startup. The unzip process has a low one
                // time cost.
                if(GetCurrentPlatform() == "mac")
                    ForceUnpackExecutable();

                if (string.IsNullOrEmpty(relayExecutable) || !File.Exists(relayExecutable))
                {
                    Debug.LogError($"[RelayProcessManager] Relay executable not found: {relayExecutable}");
                    return;
                }

                // Find an available port
                s_ServerPort = FindAvailablePort();
                if (s_ServerPort == 0)
                {
                    Debug.LogError("[RelayProcessManager] No available ports found in range 9001-9100");
                    return;
                }
                RelayIdentifier.SetRelayPort(s_ServerPort);

                // Start server with compiled executable
                var startInfo = new ProcessStartInfo
                {
                    FileName = relayExecutable,
                    Arguments = $"{s_ServerPort} {RelayIdentifier.EditorProcessId} {k_AutoShutdownDelaySeconds}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                s_ProcessHandle = Process.Start(startInfo);
                ProcessStatus = new RelayProcessStatus() { IsStarted = true, IsRunning = true };

                if (s_ProcessHandle != null)
                {
                    // Store the process ID for reconnection after domain reload
                    RelayIdentifier.SetRelayProcessId(s_ProcessHandle.Id);

                    SetupProcessMonitoring();

                    // Wait for server startup
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RelayProcessManager] Error starting server: {ex.Message}");
            }
        }

        static int FindAvailablePort()
        {
            for (int port = k_StartPort; port <= k_MaxPort; port++)
            {
                if (IsPortAvailable(port))
                {
                    return port;
                }
            }
            return 0;
        }

        static bool IsPortAvailable(int port)
        {
            try
            {
                var tcpListener = new TcpListener(IPAddress.Loopback, port);
                tcpListener.Start();
                tcpListener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void SetupProcessMonitoring()
        {
            if (s_ProcessHandle == null) return;

            s_ProcessHandle.EnableRaisingEvents = true;
            s_ProcessHandle.Exited += (sender, e) =>
            {
                ProcessStatus = new RelayProcessStatus()
                {
                    IsStarted = true,
                    IsRunning = false,
                    ExitMessage = $"Relay exited. code={s_ProcessHandle.ExitCode} stderr=\"{s_ProcessHandle.StandardError.ReadToEnd()}\""
                };

                CleanupProcess();
            };
        }

        static bool TryRetrieveProcess(int processId)
        {
            if (processId == 0)
                return false;

            try
            {
                var existingProcess = Process.GetProcessById(processId);
                if (existingProcess.HasExited) return false;

                s_ProcessHandle = existingProcess;
                SetupProcessMonitoring();

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RelayProcessManager] Error reconnecting to process {processId}: {ex.Message}");
                return false;
            }
        }

        public static bool HasProcessHandle()
        {
            if (s_ProcessHandle == null) return false;
            return !s_ProcessHandle.HasExited;
        }

        static bool IsServerRunningOnPort(int port)
        {
            try
            {
                var webSocketTask = Task.Run(async () => await ValidateServerWithEditor(port, RelayIdentifier.EditorProcessId));
                return webSocketTask.Wait(2000) && webSocketTask.Result;
            }
            catch
            {
                return false;
            }
        }

        static async Task<bool> ValidateServerWithEditor(int port, int editorProcessId)
        {
            try
            {
                using var webSocket = new System.Net.WebSockets.ClientWebSocket();
                string testAddress = $"ws://127.0.0.1:{port}?validationCheck=true";
                var uri = new Uri(testAddress);

                using var cts = new System.Threading.CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMilliseconds(2000));

                await webSocket.ConnectAsync(uri, cts.Token);

                // Wait for the test response from server
                var buffer = new byte[1024];
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

                if (result.MessageType != System.Net.WebSockets.WebSocketMessageType.Text) return false;

                var response = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                var testResponse = UnityEngine.JsonUtility.FromJson<ServerValidationResponse>(response);
                return testResponse is { status: "success", serverReady: true };
            }
            catch
            {
                return false;
            }
        }

        public static void CleanupProcess()
        {
            s_ServerPort = 0;

            RelayIdentifier.ClearRelayPort();
            RelayIdentifier.ClearRelayProcessId();
        }

        public static void KillServer()
        {
            try
            {
                if (s_ProcessHandle != null && !s_ProcessHandle.HasExited)
                {
                    Debug.Log($"[RelayProcessManager] Forcefully killing server process (PID: {s_ProcessHandle.Id})");

                    s_ProcessHandle.Kill();
                    s_ProcessHandle.WaitForExit(500);
                    s_ProcessHandle.Dispose();
                    s_ProcessHandle = null;
                }
                else
                {
                    Debug.Log("[RelayProcessManager] No server process to kill");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RelayProcessManager] Error killing server process: {ex.Message}");
            }
            finally
            {
                CleanupProcess();
            }
        }

        static string GetRelayExecutablePath()
        {
            string platform = GetCurrentPlatform();

            if (platform == "mac")
            {
                if(RuntimeInformation.OSArchitecture == Architecture.Arm64)
                    return Path.Combine(k_RelayPath, $"relay_mac_arm64.app/Contents/MacOS/relay_mac_arm64");
                if(RuntimeInformation.OSArchitecture == Architecture.X64)
                    return Path.Combine(k_RelayPath, $"relay_mac_x64.app/Contents/MacOS/relay_mac_x64");

                throw new Exception($"Could not find relay paths. {RuntimeInformation.OSArchitecture} compatible " +
                                    $"relay does not exist");
            }

            return Path.Combine(k_RelayPath, $"relay_{platform}");
        }

        static void ForceUnpackExecutable()
        {
            // Only mac platform needs unpacking
            if (GetCurrentPlatform() != "mac")
                return;

#if ASSISTANT_INTERNAL
            var stopwatch = Stopwatch.StartNew();
#endif

            // Unpack the mac executable. This is a zip of reasonably small size. The unzip  process can occur on this
            // thread without significant blocking time
            try
            {
                string arch = RuntimeInformation.OSArchitecture switch
                {
                    Architecture.Arm64 => "arm64",
                    Architecture.X64 => "x64",
                    _ => throw new Exception($"{RuntimeInformation.OSArchitecture} not supported on mac. Cannot " +
                                             $"unpack relay.")
                };

                // This is created in the folder when unpacking the application. It contains extra metadata for apple
                string macosxPath = Path.Combine(k_RelayPath, "__MACOSX");
                if(Directory.Exists(macosxPath))
                    Directory.Delete(macosxPath, true);

                // This is the last unpacked app
                string appPath = Path.Combine(k_RelayPath, $"relay_mac_{arch}.app");
                if(Directory.Exists(appPath))
                    Directory.Delete(appPath, true);

                // Unzip the relay
                var zipPath = Path.Combine(k_RelayPath, $"relay_mac_{arch}");
                ZipFile.ExtractToDirectory(zipPath, k_RelayPath);

                // Set the execution permission for the relay
                var relayExecutablePath = Path.Combine(
                    k_RelayPath,
                    $"relay_mac_{arch}.app/Contents/MacOS/relay_mac_{arch}");

                var chmodInfo = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{relayExecutablePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(chmodInfo)?.WaitForExit();
            }
            catch (Exception)
            {
                Debug.LogError("$[RelayManager] The AI Assistant Relay Server failed to unzip or set permissions");
                throw;
            }

#if ASSISTANT_INTERNAL
            InternalLog.Log($"[RelayManager] The relay server was unzipped and it's permissions were set. " +
                            $"execution_time={stopwatch.Elapsed.Milliseconds}ms");
#endif
        }

        static string GetCurrentPlatform()
        {
#if UNITY_EDITOR_WIN
            return "win.exe";
#elif UNITY_EDITOR_OSX
            return "mac";
#elif UNITY_EDITOR_LINUX
            return "linux";
#else
            throw new NotSupportedException("Unsupported platform");
#endif
        }
    }
}
