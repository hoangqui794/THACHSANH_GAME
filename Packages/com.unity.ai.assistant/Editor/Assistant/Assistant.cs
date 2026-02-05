using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Backend.Socket;
using Unity.AI.Assistant.Editor.Config;
using Unity.AI.Assistant.Editor.Config.Credentials;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Socket.ErrorHandling;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEngine.Pool;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant : IAssistantProvider
    {
        public const string k_UserRole = "user";
        public const string k_AssistantRole = "assistant";
        public const string k_SystemRole = "system";

        static float s_LastRefreshTokenTime;

        public IAssistantBackend Backend { get; private set; }
        public IFunctionCaller FunctionCaller { get; private set; }

        public ICredentialsProvider CredentialsProvider { get; private set; }

        public IToolPermissions ToolPermissions => ToolInteractionAndPermissionBridge.ToolPermissions;
        public IToolInteractions ToolInteractions => ToolInteractionAndPermissionBridge.ToolInteractions;

        public ToolInteractionAndPermissionBridge ToolInteractionAndPermissionBridge { get; private set; }

        public Assistant(AssistantConfiguration configuration = null)
        {
            Reconfigure(configuration);
        }

        internal void Reconfigure(AssistantConfiguration configuration = null)
        {
            Backend = configuration?.Backend ?? new AssistantRelayBackend();

            ToolInteractionAndPermissionBridge = configuration?.Bridge ?? new ToolInteractionAndPermissionBridge(
                new AllowAllToolPermissions(),
                new AllowAllToolInteractions());
            
            // TODO: Why is IFunctionCaller an interface but not configurable
            FunctionCaller = new AIAssistantFunctionCaller(ToolInteractionAndPermissionBridge, ToolInteractionAndPermissionBridge);

			CredentialsProvider = configuration?.CredentialsProvider ?? new EditorCredentialsProvider();
        }

        public event Action<AssistantMessageId, FeedbackData?> FeedbackLoaded;
        
        public bool SessionStatusTrackingEnabled => Backend == null || Backend.SessionStatusTrackingEnabled;

        AssistantMessage AddInternalMessage(AssistantConversation conversation, string text, string role = null, bool sendUpdate = true, int indexOverride = -1)
        {
            var message = new AssistantMessage
            {
                Id = AssistantMessageId.GetNextInternalId(conversation.Id),
                IsComplete = true,
                Role = role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            message.AddMessageForRole(role, text, message.IsComplete);

            if (indexOverride > conversation.Messages.Count)
            {
                InternalLog.LogError($"Index override {indexOverride} is out of bounds for conversation with {conversation.Messages.Count} messages.");
                indexOverride = -1; // Fallback to adding at the end
            }

            if (indexOverride < 0)
            {
                conversation.Messages.Add(message);
            }
            else
            {
                conversation.Messages.Insert(indexOverride, message);
            }

            if (sendUpdate)
            {
                NotifyConversationChange(conversation);
            }

            return message;
        }

        AssistantMessage AddIncompleteMessage(AssistantConversation conversation, string text, string role = null, bool sendUpdate = true)
        {
            var message = new AssistantMessage
            {
                Id = AssistantMessageId.GetNextIncompleteId(conversation.Id),
                IsComplete = false,
                Role = role,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            if (!string.IsNullOrEmpty(text))
                message.AddMessageForRole(role, text, message.IsComplete);

            conversation.Messages.Add(message);
            if (sendUpdate)
            {
                NotifyConversationChange(conversation);
            }

            return message;
        }

        public async Task SendFeedback(AssistantMessageId messageId, bool flagMessage, string feedbackText, bool upVote)
        {
            var feedback = new MessageFeedback
            {
                MessageId = messageId,
                FlagInappropriate = flagMessage,
                Type = Category.ResponseQuality,
                Message = feedbackText,
                Sentiment = upVote ? Sentiment.Positive : Sentiment.Negative
            };

            // Failing to send feedback is non-critical. UX for failures here can be improved in a QOL pass if necessary.
            var result = await Backend.SendFeedback(await CredentialsProvider.GetCredentialsContext(), messageId.ConversationId.Value, feedback);

            if (result.Status != BackendResult.ResultStatus.Success)
                ErrorHandlingUtility.InternalLogBackendResult(result);
        }

        public async Task<FeedbackData?> LoadFeedback(AssistantMessageId messageId, CancellationToken ct = default)
        {
            if (!messageId.ConversationId.IsValid || messageId.Type != AssistantMessageIdType.External)
            {
                // Whatever we are asking for is not valid to be asked for
                return null;
            }

            var result =  await Backend.LoadFeedback(await CredentialsProvider.GetCredentialsContext(ct), messageId, ct);

            if (result.Status != BackendResult.ResultStatus.Success)
            {
#if ASSISTANT_INTERNAL_VERBOSE
                // if feedback fails to load, silently fail
                ErrorHandlingUtility.InternalLogBackendResult(result);
#endif
                return null;
            }

            FeedbackLoaded?.Invoke(messageId, result.Value);

            return result.Value;
        }
  
        /// <summary>
        /// Recover incomplete message from relay server cache after domain reload
        /// </summary>
        public async Task RecoverIncompleteMessage(AssistantConversationId conversationId)
        {
            try
            {
                InternalLog.Log($"Attempting to recover incomplete message for conversation: {conversationId}");

                // Wait for relay connection to be ready (max 5 seconds)
                int retries = 0;
                const int maxRetries = 10;
                while (!Relay.Editor.RelayConnection.Instance.IsConnected && retries < maxRetries)
                {
                    InternalLog.Log($"Waiting for relay connection... (attempt {retries + 1}/{maxRetries})");
                    await Task.Delay(500); // Wait 500ms between retries
                    retries++;
                }

                if (!Relay.Editor.RelayConnection.Instance.IsConnected)
                {
                    InternalLog.LogWarning("Relay connection not available after 5 seconds, cannot recover message");
                    return;
                }

                InternalLog.Log("Relay connected, initiating incomplete message recovery");

                // Get conversation from cache (should already be loaded)
                if (!m_ConversationCache.TryGetValue(conversationId, out var conversation))
                {
                    InternalLog.LogWarning("Conversation not in cache yet, cannot recover incomplete message");
                    return;
                }

                // Check if the conversation end with a user message without answers
                var lastConversationMessage = conversation.Messages.Last();
                if (lastConversationMessage == null || lastConversationMessage.Role.ToLower() != k_UserRole)
                    return;

                var message = AddIncompleteMessage(conversation, string.Empty, k_AssistantRole, sendUpdate: true);

                // Create workflow in recovery mode (skip initialization, just set up message handlers)
                var credentialsContext = await CredentialsProvider.GetCredentialsContext(CancellationToken.None);
                var workflow = Backend.GetOrCreateWorkflow(
                    credentialsContext,
                    FunctionCaller,
                    conversationId,
                    skipInitialization: true);

                if (workflow == null)
                {
                    InternalLog.LogError("Failed to create workflow for recovery");
                    return;
                }

                // Start listening to workflow events (handles both replayed and new streaming messages)
                 ResumeIncompleteMessage(workflow, conversation, message, CancellationToken.None);

                // Request replay - messages will flow through the workflow to ResumeIncompleteMessage handlers
                bool replayStarted = await Relay.Editor.RelayConnection.Instance.ReplayIncompleteMessageAsync();

                if (!replayStarted)
                    InternalLog.LogWarning("Failed to initiate replay");
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"Failed to recover incomplete message: {ex.Message}");
            }
        }

        public async Task RefreshProjectOverview(CancellationToken cancellationToken = default)
        {
            await ProjectOverview.RefreshProjectOverview(cancellationToken);
        }
    }
}
