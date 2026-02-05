namespace Unity.AI.Assistant.Agent.Dynamic.Extension.Editor
{
#if CODE_LIBRARY_INSTALLED
    public
#else
    internal
#endif
        interface IRunCommand
    {
        string Title { get; }

        string Description { get; }

        public void Execute(ExecutionResult result);
    }
}
