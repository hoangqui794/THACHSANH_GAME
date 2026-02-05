namespace Unity.AI.Assistant.FunctionCalling
{
    internal interface IFunctionSource
    {
        CachedFunction[] GetFunctions();
    }
}
