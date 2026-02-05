namespace Unity.AI.Assistant.Editor.Commands
{
    class CodeCommand : ChatCommandHandler
    {
        public const string k_CommandName = "code";
        public override string Command => k_CommandName;
        public override bool UseContext => true;
        public override bool UseSmartContext => true;
        public override bool UseDependencies => true;
        public override bool UseProjectSummary => true;
        public override string PlaceHolderText => "Write scripts with a dedicated code engine";
        public override string Tooltip => "Generating code is experimental and may not be reliable or consistent. We recommend using it only for testing.";
        public override string Icon => "mui-icon-cmd-code";

        public override bool ShowInList => true;
    }
}
