using System;
using System.Text;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;
using UnityEditorInternal;

namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    class ProfilingTools
    {
        public const string InitializeToolId = "Unity.Profiler.Initialize";
        
        // Note: Has to be a syntax-valid relative path
        const string k_ActiveSessionPath = ".active";

        const ulong k_GcMemoryAllocationThreshold = 8 * 1024; // 8KB

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
        }

        static void Shutdown()
        {
            ConversationCacheExtension.CleanUp();
        }

        [AgentTool(
            "Initializes a profiling session so that its data is available and return information about the session. " +
            "You should use this tool if you don't have access to a specific profiling session already.",
            id: InitializeToolId,
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static async Task<string> InitializeSession(
            ToolExecutionContext context,
            [Parameter("Optional: specify directly the path of the profiling session to load, if known. Leave empty otherwise.")]
            string sessionPath = null
        )
        {
            var profilingSessions = await SessionProvider.GetProfilingSessions(context);

            // Add an extra entry for the active session
            if (ProfilerUtils.HasInMemorySession())
            {
                var inMemorySession = new SessionProvider.ProfilerSessionInfo();
                inMemorySession.ProjectRelativePath = k_ActiveSessionPath;
                inMemorySession.FileName = "Active Session";
                profilingSessions.Insert(0, inMemorySession);
            }

            SessionProvider.ProfilerSessionInfo selectedSession = null;

            // No specific session provided: automatically pick or ask user
            if (string.IsNullOrEmpty(sessionPath))
            {
                // No profiling session found
                if (profilingSessions.Count == 0)
                {
                    // Only push interaction if not in auto-run mode
                    if (!AssistantEditorPreferences.AutoRun)
                    {
                        var recordSessionInteraction = new RecordSessionInteraction();
                        await context.Interactions.WaitForUser(recordSessionInteraction);
                    }

                    // Signal LLM as we cannot wait for the user to actually record a session
                    throw new Exception("No profiling sessions found. Need to wait for user to record one.");
                }

                // By default, pick the first (most recent or in-memory)
                selectedSession = profilingSessions[0];

                // If more than a single session, let the user pick one
                if (profilingSessions.Count != 1)
                {
                    // In auto-run mode, let the LLM decide
                    if (AssistantEditorPreferences.AutoRun)
                    {
                        var sb = new StringBuilder();
                        sb.Append("Available profiling sessions:\n");
                        foreach (var profilingSession in profilingSessions)
                        {
                            if (profilingSession.ProjectRelativePath == k_ActiveSessionPath)
                            {
                                sb.AppendFormat($" - {profilingSession.ProjectRelativePath} (last loaded or captured session)\n");
                            }
                            else
                            {
                                sb.AppendFormat($" - {profilingSession.ProjectRelativePath}\n");
                            }
                        }

                        sb.Append("Call this tool again with the path of the profiling session you want to load.");
                        return sb.ToString();
                    }

                    // Otherwise, ask the user to select the session
                    var pickSessionInteraction = new PickSessionInteraction(profilingSessions);
                    selectedSession = await context.Interactions.WaitForUser(pickSessionInteraction);
                    
                    // No session selected
                    if (selectedSession == null)
                        throw new Exception("No profiling session was selected.");
                }
            }
            // Session path provided: load it if available
            else
            {
                // Identify the session in the available list
                foreach (var profilingSession in profilingSessions)
                {
                    if (PathUtils.PathsEqual(profilingSession.ProjectRelativePath, sessionPath))
                    {
                        selectedSession = profilingSession;
                        break;
                    }
                }

                // Session could not be found
                if (selectedSession == null)
                    throw new Exception("Could not find the profiling session at the given path.");
            }

            // Skip loading if using active session
            if (selectedSession.ProjectRelativePath == k_ActiveSessionPath)
                return "Loaded in-memory profiling sessions.";

            // Cleanup the cache related to the current session if we load another capture
            context.Conversation.ClearFrameDataCache();

            // Load profiling session
            ProfilerDriver.LoadProfile(selectedSession.ProjectRelativePath, false);
            return $"Initialized session at path: {selectedSession.ProjectRelativePath}";
        }

        [AgentTool("Return an overall summary of the available profiling data and specific project settings.",
            "Unity.Profiler.GetOverallSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetOverallSummary(
            ToolExecutionContext context,
            [Parameter("Target Frame Time for the analysis")]
            float targetFrameTime
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return FrameRangeTimeSummaryProvider.GetSummary(frameDataCache, new Range(frameDataCache.FirstFrameIndex, frameDataCache.LastFrameIndex), targetFrameTime);
        }

        [AgentTool("Return a summary of the top samples of a specific frame based on the sample total time.",
            "Unity.Profiler.GetFrameTopTimeSamplesSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetFrameTopTimeSamplesSummary(
            ToolExecutionContext context,
            [Parameter("The index of the frame from which to get the summary")]
            int frameIndex,
            [Parameter("Target Frame Time for the analysis")]
            float targetFrameTime
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return FrameTimeSummaryProvider.GetSummary(frameDataCache, frameIndex, targetFrameTime);
        }

        [AgentTool("Return a summary of the top individual samples in a specific frame based on the sample self time.",
            "Unity.Profiler.GetFrameSelfTimeSamplesSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetFrameSelfTimeSamplesSummary(
            ToolExecutionContext context,
            [Parameter("The index of the frame from which to get the summary")]
            int frameIndex
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return MostExpensiveSamplesInFrameSummaryProvider.GetSummary(frameDataCache, frameIndex);
        }

        [AgentTool("Return a summary of the time profiling data over a range of multiple frames.",
            "Unity.Profiler.GetFrameRangeTopTimeSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetFrameRangeTopTimeSummary(
            ToolExecutionContext context,
            [Parameter("The index of the first frame from which to get the summary")]
            int startFrameIndex,
            [Parameter("The index of the last frame from which to get the summary")]
            int lastFrameIndex,
            [Parameter("Target Frame Time for the analysis")]
            float targetFrameTime
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return FrameRangeTimeSummaryProvider.GetSummary(frameDataCache, new Range(startFrameIndex, lastFrameIndex), targetFrameTime);
        }

        [AgentTool("Returns a summary of a given profiler sample.",
            "Unity.Profiler.GetSampleTimeSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetSampleTimeSummary(
            ToolExecutionContext context,
            [Parameter("The index of the frame the sample belongs to")]
            int frameIndex,
            [Parameter("The name of the thread the sample belongs to")]
            string threadName,
            [Parameter("Sample index")]
            int sampleIndex
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return SampleTimeSummaryProvider.GetSummary(frameDataCache, frameIndex, threadName, sampleIndex, false);
        }

        [AgentTool("Returns a summary of time of a given profiler sample during the bottom-up analysis.",
            "Unity.Profiler.GetBottomUpSampleTimeSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetBottomUpSampleTimeSummary(
            ToolExecutionContext context,
            [Parameter("The index of the frame the sample belongs to")]
            int frameIndex,
            [Parameter("The name of the thread the sample belongs to")]
            string threadName,
            [Parameter("Bottom-up analysis sample index")]
            int sampleIndex
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return SampleTimeSummaryProvider.GetSummary(frameDataCache, frameIndex, threadName, sampleIndex, true);
        }

        [AgentTool("Returns a summary of a given profiler sample specified by the Marker Id Path.",
            "Unity.Profiler.GetSampleTimeSummaryByMarkerPath",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetSampleTimeSummaryByMarkerPath(
            ToolExecutionContext context,
            [Parameter("The index of the frame the sample belongs to")]
            int frameIndex,
            [Parameter("The name of the thread the sample belongs to")]
            string threadName,
            [Parameter("Marker Id Path")]
            string markerIdPath
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return SampleTimeSummaryProvider.GetSummary(frameDataCache, frameIndex, threadName, markerIdPath);
        }

        [AgentTool("Returns a summary of related samples on other thread that are executed at the same time.",
            "Unity.Profiler.GetRelatedSamplesTimeSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetRelatedSamplesTimeSummary(
            ToolExecutionContext context,
            [Parameter("The index of the frame the samples belongs to")]
            int frameIndex,
            [Parameter("The name of the thread the original sample belongs to")]
            string threadName,
            [Parameter("Sample index")]
            int sampleIndex,
            [Parameter("Thread name to get a summary of related samples")]
            string relatedThreadName
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return SampleTimeSummaryProvider.GetRelatedThreadSummary(frameDataCache, frameIndex, threadName, sampleIndex, relatedThreadName, false);
        }

        #region GC Analysis Tools

        [AgentTool("Return an overall summary of GC allocations in the available profiling data.",
            "Unity.Profiler.GetOverallGcAllocationsSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetOverallGcAllocationsSummary(ToolExecutionContext context)
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return FrameRangeGcAllocationSummaryProvider.GetSummary(frameDataCache, new Range(frameDataCache.FirstFrameIndex, frameDataCache.LastFrameIndex), k_GcMemoryAllocationThreshold);
        }

        [AgentTool("Return a summary of the top GC allocation samples in the specific frame based.",
            "Unity.Profiler.GetFrameGcAllocationsSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetFrameGcAllocationsSummary(
            ToolExecutionContext context,
            [Parameter("The index of the frame from which to get the summary")]
            int frameIndex
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return FrameGcAllocationSummaryProvider.GetSummary(frameDataCache, frameIndex, k_GcMemoryAllocationThreshold);
        }

        [AgentTool("Return a summary of the GC allocations over a range of multiple frames.",
            "Unity.Profiler.GetFrameRangeGcAllocationsSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetFrameRangeGcAllocationsSummary(
            ToolExecutionContext context,
            [Parameter("The index of the first frame from which to get the summary")]
            int startFrameIndex,
            [Parameter("The index of the last frame from which to get the summary")]
            int lastFrameIndex
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return FrameRangeGcAllocationSummaryProvider.GetSummary(frameDataCache, new Range(startFrameIndex, lastFrameIndex), k_GcMemoryAllocationThreshold);
        }

        [AgentTool("Returns a summary of GC allocations of a given profiler sample.",
            "Unity.Profiler.GetSampleGcAllocationSummary",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetSampleGcAllocationSummary(
            ToolExecutionContext context,
            [Parameter("The index of the frame the sample belongs to")]
            int frameIndex,
            [Parameter("The name of the thread the original sample belongs to")]
            string threadName,
            [Parameter("Sample index")]
            int sampleIndex
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return SampleGcAllocationSummaryProvider.GetSummary(frameDataCache, frameIndex, threadName, sampleIndex);
        }

        [AgentTool("Returns a summary of a given profiler sample specified by the Marker Id Path.",
            "Unity.Profiler.GetSampleGcAllocationSummaryByMarkerPath",
            assistantMode: AssistantMode.Agent | AssistantMode.Ask)]
        public static string GetSampleGcAllocationSummaryByMarkerPath(
            ToolExecutionContext context,
            [Parameter("The index of the frame the sample belongs to")]
            int frameIndex,
            [Parameter("The name of the thread the original sample belongs to")]
            string threadName,
            [Parameter("Marker Id Path")]
            string markerIdPath
        )
        {
            var frameDataCache = context.Conversation.GetFrameDataCache();
            return SampleGcAllocationSummaryProvider.GetSummary(frameDataCache, frameIndex, threadName, markerIdPath);
        }

        #endregion
    }
}
