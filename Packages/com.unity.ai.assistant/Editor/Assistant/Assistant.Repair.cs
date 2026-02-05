using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant
    {
        readonly HashSet<AssistantMessageId> k_MessagesUnderRepair = new();

        public bool IsUnderRepair(AssistantMessageId messageId)
        {
            return k_MessagesUnderRepair.Contains(messageId);
        }

        /// <summary>
        /// Returns whether or not the given messageId can be sent for repair.
        /// Only the most recent message in the active message can be repaired.
        /// </summary>
        /// <param name="conversationId">The conversation to repair the message of</param>
        /// <param name="messageId">The id of the message to be repaired</param>
        /// <returns>True if a repair call can be sent for the given message, false otherwise</returns>
        public bool ValidRepairTarget(AssistantConversationId conversationId, AssistantMessageId messageId)
        {
            if (!m_ConversationCache.TryGetValue(conversationId, out var conversation))
            {
                // we don't have this conversation loaded or cached
                return false;
            }

            // Messages can be repaired if they are the last message in the conversation
            return (conversation.Messages.FindIndex(match => match.Id == messageId) == (conversation.Messages.Count - 1));
        }
    }
}
