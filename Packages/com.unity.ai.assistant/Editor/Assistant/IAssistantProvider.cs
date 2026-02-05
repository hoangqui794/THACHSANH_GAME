using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.RunCommand;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Socket.ErrorHandling;

namespace Unity.AI.Assistant.Editor
{
    internal interface IAssistantProvider
    {
        // Dependencies
        IToolPermissions ToolPermissions { get; }
        IRunCommandInterpreter RunCommandInterpreter { get; }

        // Callbacks
        event Action<IEnumerable<AssistantConversationInfo>> ConversationsRefreshed;
        event Action<AssistantConversationId, Assistant.PromptState> PromptStateChanged;
        event Action<AssistantConversation> ConversationLoaded;
        event Action<AssistantConversation> ConversationChanged;
        event Action<AssistantConversation> ConversationCreated;
        event Action<AssistantConversationId> ConversationDeleted;

        /// <summary>
        /// Invoked when an error occurs during an active conversation. If this is invoked and the conversation is
        /// active, this error indicates that conversation has stopped. All errors are critical errors and the
        /// conversation will cease to perform work.
        /// </summary>
        event Action<AssistantConversationId, ErrorInfo> ConversationErrorOccured;

        event Action<AssistantMessageId, FeedbackData?> FeedbackLoaded;
        
        /// <summary>
        /// Invoked when message cost is received.
        /// </summary>
        event Action<AssistantMessageId, int?, bool> MessageCostReceived;

        /// <summary>
        /// Invoked when an incomplete message starts streaming
        /// </summary>
        event Action<AssistantConversationId, string> IncompleteMessageStarted;

        /// <summary>
        /// Invoked when an incomplete message is completed
        /// </summary>
        event Action<AssistantConversationId> IncompleteMessageCompleted;

        bool SessionStatusTrackingEnabled { get; }

        // Methods
        Task ConversationLoad(AssistantConversationId conversationId, CancellationToken ct = default);
        Task RecoverIncompleteMessage(AssistantConversationId conversationId);
        Task ConversationFavoriteToggle(AssistantConversationId conversationId, bool isFavorite);
        Task ConversationDeleteAsync(AssistantConversationId conversationId, CancellationToken ct = default);
        Task ConversationRename(AssistantConversationId conversationId, string newName, CancellationToken ct = default);
        Task RefreshConversationsAsync(CancellationToken ct = default);

        Task ProcessPrompt(AssistantConversationId conversationId, AssistantPrompt prompt, IAgent agent = null, CancellationToken ct = default);
        Task SendFeedback(AssistantMessageId messageId, bool flagMessage, string feedbackText, bool upVote);
        Task<FeedbackData?> LoadFeedback(AssistantMessageId messageId, CancellationToken ct = default);
        Task<int?> FetchMessageCost(AssistantMessageId messageId, CancellationToken ct = default);
        
        Task SendEditRunCommand(AssistantMessageId messageId, string updatedCode);

        void SuspendConversationRefresh();
        void ResumeConversationRefresh();

        void AbortPrompt(AssistantConversationId conversationId);

        // Run Command and Related to it
        void RunAgentCommand(AssistantConversationId conversationId, AgentRunCommand command, string fencedTag);
        ExecutionResult GetRunCommandExecution(int executionId);

        // Function Calling
        IFunctionCaller FunctionCaller { get; }

        Task RefreshProjectOverview(CancellationToken cancellationToken = default);
    }
}
