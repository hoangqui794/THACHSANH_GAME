namespace Unity.AI.Assistant.Editor.Commands
{
    class AskCommand : ChatCommandHandler
    {
        public const string k_CommandName = "ask";
        public override string Command => k_CommandName;
        public override bool ShowInList => true;
        public override bool UseContext => true;
        public override bool UseSmartContext => true;
        public override bool UseDependencies => true;
        public override bool UseProjectSummary => true;
        public override string PlaceHolderText => "Ask about Unity, / to start a shortcut";
    }
}
