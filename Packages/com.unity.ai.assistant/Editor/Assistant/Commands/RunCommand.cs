namespace Unity.AI.Assistant.Editor.Commands
{
    class RunCommand : ChatCommandHandler
    {
        public const string k_CommandName = "run";
        public override string Command => k_CommandName;
        public override bool UseContext => true;
        public override bool UseSmartContext => true;
        public override bool UseDependencies => true;
        public override bool UseProjectSummary => true;
        public override string PlaceHolderText => "Run commands in the Editor";
        public override string Tooltip => "Running commands is experimental and may not be reliable or consistent. We recommend using it only for testing.";
        public override string Icon => "mui-icon-cmd-run";
        public override bool ShowInList => true;
    }
}
