using System.Collections.Generic;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Data
{
    internal struct MessageModel
    {
        public AssistantMessageId Id;

        // Note: This is purely for display and optional, do NOT use this for any processing
        //       i.e do not depend on this being there at all times!
        public string Command;

        public bool IsComplete;
        public MessageModelRole Role;

        // Note: For now we re-use the same data as the API layer for simplicity, there are several helper methods attached to this
        //       If major changes happen on it we will move to a distinct model for the UI
        public AssistantContextEntry[] Context;
        public FeedbackData? Feedback;

        public List<IMessageBlockModel> Blocks;
        public long Timestamp;

        public bool HasContent() => Blocks != null && Blocks.Count > 0;

        public bool HasEqualContent(MessageModel other)
        {
            if (HasContent() != other.HasContent())
                return false;

            if (Blocks?.Count != other.Blocks?.Count)
                return false;

            for (int i = 0; i < Blocks.Count; i++)
            {
                if (!Blocks[i].Equals(other.Blocks[i]))
                    return false;
            }

            var feedbackEquals = other.Feedback.Equals(Feedback);
            var contextEquals = ArrayUtils.ArrayEquals(other.Context, Context);
            var completeEquals = other.IsComplete == IsComplete;
            var timestampEquals = other.Timestamp == Timestamp;

            return feedbackEquals && contextEquals && completeEquals && timestampEquals;
        }
    }
}
