using System.Collections.Generic;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Backend
{
    /// <summary>
    /// Registry for capability declarations that bridges Runtime and Editor assemblies
    /// </summary>
    static class CapabilityRegistry
    {
        static List<FunctionsObject> s_RegisteredFunctions = new();

        public static List<FunctionsObject> GetFunctionCapabilities()
        {
            return new List<FunctionsObject>(s_RegisteredFunctions);
        }

        /// <summary>
        /// Registers a funtion capability (called from Editor assembly during initialization)
        /// </summary>
        public static void RegisterFunction(FunctionsObject functionCapability)
        {
            if (functionCapability == null)
                return;

            s_RegisteredFunctions.Add(functionCapability);
            InternalLog.Log($"Registered function capability: {functionCapability.FunctionName} (ID: {functionCapability.FunctionId})");
        }

        internal static void Clear()
        {
            s_RegisteredFunctions.Clear();
        }
    }
}
