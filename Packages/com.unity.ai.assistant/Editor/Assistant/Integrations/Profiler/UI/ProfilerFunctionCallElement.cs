using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    [FunctionCallRenderer(ProfilingTools.InitializeToolId)]
    class InitializeFunctionCallElementBase : DefaultFunctionCallRenderer
    {
        public override string Title => "Initialize Profiler Assistant";
    }
}
