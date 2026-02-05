using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.UI.Editor.Scripts.ConversationSearch;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class AssistantConversationPanel : ManagedTemplate
    {
        VisualElement m_ConversationRoot;
        ChatScrollView<MessageModel, ChatElementWrapper> m_ConversationList;

        VisualElement m_OverlayElements;

        Button m_ScrollToBottomButton;

        int m_ErrorCount;

        ResponseFeedbackQueue m_FeedbackQueue;

        public AssistantConversationPanel() : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_OverlayElements = view.Q<VisualElement>("conversationOverlayElements");

            m_ConversationRoot = view.Q<VisualElement>("conversationRoot");
            m_ConversationList = new ChatScrollView<MessageModel, ChatElementWrapper>
            {
                EnableDelayedElements = false,
                EnableScrollLock = true
            };

            m_ConversationList.UserScrolled += UpdateOverlayButtons;

            m_ConversationList.Initialize(Context);
            m_ConversationRoot.Add(m_ConversationList);

            Context.SearchHelper = new AssistantViewSearchHelper(m_ConversationList, Context);

            m_ScrollToBottomButton = view.SetupButton("conversationScrollToBottomButton", _ => ScrollToBottom());

            Context.ConversationScrollToEndRequested += () => ScrollToBottom(true);

            Context.API.ConversationChanged += OnConversationChanged;

            m_FeedbackQueue = new ResponseFeedbackQueue(Context);
            m_FeedbackQueue.LoadedFeedback += OnFeedbackLoaded;

            UpdateVisibility();
        }

        public void Populate(ConversationModel conversation)
        {
            m_ConversationList.BeginUpdate();
            for (var i = 0; i < conversation.Messages.Count; i++)
            {
                var msg = conversation.Messages[i];
                m_ConversationList.AddData(msg);

                if (msg.Role == MessageModelRole.User)
                {
                    m_ErrorCount = 0;
                }
            }

            for (var i = conversation.Messages.Count-1; i >= 0; i--)
            {
                var msg = conversation.Messages[i];
                if (msg.Role == MessageModelRole.Assistant)
                {
                    m_FeedbackQueue.QueueRefresh(msg.Id, i);
                }
            }

            m_ConversationList.EndUpdate();
            UpdateVisibility();
        }

        public void ClearConversation()
        {
            m_ConversationList.ClearData();
            m_FeedbackQueue.Clear();

            UpdateVisibility();
            UpdateOverlayButtons();
        }

        public bool TryPushInteraction(ToolExecutionContext.CallInfo callInfo, VisualElement userInteraction)
        {
            var chatElementsWrappers = m_ConversationList.VisualElements;
            if (chatElementsWrappers.Count == 0)
                return false;

            for (var i = chatElementsWrappers.Count - 1; i >= 0; i--)
            {
                var chatElementWrapper = chatElementsWrappers[i];
                if (chatElementWrapper.TryPushInteraction(callInfo, userInteraction))
                {
                    m_ConversationList.ScrollToEnd();
                    return true;
                }
            }

            return false;
        }

        public bool TryPopInteraction(ToolExecutionContext.CallInfo callInfo, VisualElement userInteraction)
        {
            var chatElementsWrappers = m_ConversationList.VisualElements;
            if (chatElementsWrappers.Count == 0)
                return false;

            for (var i = chatElementsWrappers.Count - 1; i >= 0; i--)
            {
                var chatElementWrapper = chatElementsWrappers[i];
                if (chatElementWrapper.TryPopInteraction(callInfo, userInteraction))
                    return true;
            }

            return false;
        }

        void UpdateVisibility()
        {
            m_ConversationList.SetDisplay(m_ConversationList.HasContent);
            m_OverlayElements.SetDisplay(m_ConversationList.HasContent);
        }

        void OnConversationChanged(AssistantConversationId conversationId)
        {
            // Compare the conversation content against the display:
            // - Add new messages
            // - Update messages that got changed
            var conversation = Context.Blackboard.GetConversation(conversationId);
            if (conversation == null)
            {
                // We have not received this conversation data yet
                return;
            }

            // If there are more messages present in the conversation than visuals displayed, remove visuals from the
            // end of the m_ConversationList one by one
            if (conversation.Messages.Count < m_ConversationList.Data.Count)
            {
                for (int i = m_ConversationList.Data.Count - 1; i >= conversation.Messages.Count; i--)
                    m_ConversationList.RemoveData(i);
            }

            // Compare the conversation content against the display:
            // - Add new messages
            // - Update messages that got changed
            bool scrollToEndRequired = false;
            for (var messageIndex = 0; messageIndex < conversation.Messages.Count; messageIndex++)
            {
                var incomingMessage = conversation.Messages[messageIndex];
                if (messageIndex >= m_ConversationList.Data.Count)
                {
                    AddChatMessage(incomingMessage);
                    scrollToEndRequired = true;
                }
                else
                {
                    var localMessage = m_ConversationList.Data[messageIndex];

                    var messageHasContentUpdate = !incomingMessage.HasEqualContent(localMessage);
                    m_ConversationList.UpdateData(messageIndex, incomingMessage);

                    if (messageHasContentUpdate)
                    {
                        InternalLog.LogToFile(
                            conversationId.ToString(),
                            ("event", "Updating message in ui because of content change"),
                            ("index", messageIndex.ToString()),
                            ("total_messages_in_ui_currently", conversation.Messages.Count.ToString())
                        );

                        m_ConversationList.ScrollToEndIfNotLocked();
                    }
                }
            }

            if (scrollToEndRequired)
            {
                m_ConversationList.ScrollToEndIfNotLocked();
                UpdateOverlayButtons();
            }
        }

        void AddChatMessage(MessageModel message)
        {
            if (message.Role == MessageModelRole.Error)
            {
                if (m_ErrorCount > 0)
                {
                    InternalLog.Log($"MSG_ADD - Skipping a 2nd error in a row: {message.Id}");
                    return;
                }

                m_ErrorCount++;
            }

            InternalLog.Log($"MSG_ADD: {message.Id}");

            m_ConversationList.AddData(message);
        }

        void ScrollToBottom(bool scrollIfNotLocked = false)
        {
            if (scrollIfNotLocked)
                m_ConversationList.ScrollToEndIfNotLocked();
            else
                m_ConversationList.ScrollToEnd();

            UpdateOverlayButtons();
        }

        void UpdateOverlayButtons()
        {
            m_ScrollToBottomButton.SetDisplay(m_ConversationList.CanScrollDown);
        }

        void OnFeedbackLoaded(AssistantMessageId id, int index, FeedbackData? feedback)
        {
            if (m_ConversationList.Data.Count > index)
            {
                var message = m_ConversationList.Data[index];

                if (message.Id != id)
                {
                    Debug.LogError($"Feedback ID {id} does not match message ID {message.Id}");
                    return;
                }

                message.Feedback = feedback;

                m_ConversationList.UpdateData(index, message);
                ScrollToBottom(true);
            }
        }
    }
}
