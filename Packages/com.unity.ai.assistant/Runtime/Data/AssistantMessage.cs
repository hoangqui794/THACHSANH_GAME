using System;
using System.Collections.Generic;

namespace Unity.AI.Assistant.Data
{
    [Serializable]
    class AssistantMessage
    {
        /// <summary>
        /// Indicates that this is an error message and should be displayed as such
        /// </summary>
        public bool IsError;

        /// <summary>
        /// Indicates that the message is complete and no longer streaming in
        /// </summary>
        public bool IsComplete;

        public AssistantMessageId Id;
        public string Role;
        public AssistantContextEntry[] Context;
        public List<IAssistantMessageBlock> Blocks = new();

        public long Timestamp;
        public int MessageIndex;
        
        public static AssistantMessage AsError(AssistantMessageId id, string message)
        {
            var msg =  new AssistantMessage()
            {
                Id = id,
                IsError = true,
                IsComplete = true,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            msg.Blocks.Add(new ErrorBlock { Error = message });
            return msg;
        }
    }
}
