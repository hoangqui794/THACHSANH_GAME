using Unity.AI.Assistant.Data;

namespace Unity.AI.Assistant.Data
{
    interface IAssistantMessageBlock
    {
    }

    class ThoughtBlock : IAssistantMessageBlock
    {
        public string Content;
    }

    class PromptBlock : IAssistantMessageBlock
    {
        public string Content;
    }

    class ResponseBlock : IAssistantMessageBlock
    {
        public string Content;
        public bool IsComplete;
    }

    class FunctionCallBlock : IAssistantMessageBlock
    {
        public AssistantFunctionCall Call;
    }

    class ErrorBlock : IAssistantMessageBlock
    {
        public string Error;
    }
}
