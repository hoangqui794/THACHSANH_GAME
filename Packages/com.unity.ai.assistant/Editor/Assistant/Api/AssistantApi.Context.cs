using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Bridge.Editor;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor.Api
{
    static partial class AssistantApi
    {
        /// <summary>
        /// Context to attach to an agent run.
        /// </summary>
        public class AttachedContext
        {
            public bool IsEmpty => !Objects.Any() && !Logs.Any() && !VirtualAttachments.Any();

            /// <summary>
            /// List of objects to attach to the context.
            /// </summary>
            public List<Object> Objects { get; } = new();

            /// <summary>
            /// List of logs to attach to the context.
            /// </summary>
            public List<LogData> Logs { get; } = new();

            /// <summary>
            /// List of virtual attachments to attach to the context.
            /// </summary>
            public List<VirtualAttachment> VirtualAttachments { get; } = new();

            public void Add(Object obj) => Objects.Add(obj);
            public void AddRange(IEnumerable<Object> objs) => Objects.AddRange(objs);
            public void Add(LogData log) => Logs.Add(log);
            public void AddRange(IEnumerable<LogData> logs) => Logs.AddRange(logs);
            public void Add(VirtualAttachment attachment) => VirtualAttachments.Add(attachment);
            public void AddRange(IEnumerable<VirtualAttachment> attachments) => VirtualAttachments.AddRange(attachments);
        }
    }
}
