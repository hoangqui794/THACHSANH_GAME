using System;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.ConversationSearch;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    internal class AssistantUIContext
    {
        public AssistantUIContext(IAssistantProvider assistant)
        {
            // NOTE: For now we just default to the previous singleton, later we will divert into separate `Assistant` instances for open windows
            Blackboard = new AssistantBlackboard();
            API = new AssistantUIAPIInterpreter(assistant, Blackboard);
        }

        public readonly AssistantBlackboard Blackboard;
        public readonly AssistantUIAPIInterpreter API;

        public Action ConversationScrollToEndRequested;
        public Action<AssistantConversationId> ConversationRenamed;
        public Action<VirtualAttachment> VirtualAttachmentAdded;

        public Func<bool> WindowDockingState;

        public AssistantViewSearchHelper SearchHelper;

        public void Initialize()
        {
            API.Initialize();

            Blackboard.ClearActiveConversation();
        }

        public void Deinitialize()
        {
            API.Deinitialize();
        }

        public void SendScrollToEndRequest()
        {
            ConversationScrollToEndRequested?.Invoke();
        }

        public void SendConversationRenamed(AssistantConversationId id)
        {
            ConversationRenamed?.Invoke(id);
        }
    }
}
