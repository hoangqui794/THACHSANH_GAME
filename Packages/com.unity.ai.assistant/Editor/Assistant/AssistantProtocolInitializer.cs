using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;
using Unity.AI.Assistant.Utils;
using AccessTokenRefreshUtility = Unity.AI.Assistant.Utils.AccessTokenRefreshUtility;
using OrchestrationDataUtilities = Unity.AI.Assistant.Socket.Utilities.OrchestrationDataUtilities;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// Initializes Editor-specific implementations for backend configuration
    /// </summary>
    [InitializeOnLoad]
    static class AssistantProtocolInitializer
    {
        static AssistantProtocolInitializer()
        {
            RegisterToolsAsCapabilities();
            SetupDelegates();
        }

        static void RegisterToolsAsCapabilities()
        {
            foreach (var function in ToolRegistry.FunctionToolbox.Tools)
            {
                var capability = function.FunctionDefinition.ToFunctionsObject();
                Unity.AI.Assistant.Backend.CapabilityRegistry.RegisterFunction(capability);
            }
        }

        static void SetupDelegates()
        {
            // Set up AccessTokenRefreshUtility delegation
            AccessTokenRefreshUtility.IndicateRefreshMayBeRequiredDelegate =
                Utils.AccessTokenRefreshUtility.IndicateRefreshMayBeRequired;

            // Set up OrchestrationDataUtilities delegation
            OrchestrationDataUtilities.FromEditorContextReportDelegate =
                Utils.OrchestrationDataUtilities.FromEditorContextReport;
        }
    }
}
