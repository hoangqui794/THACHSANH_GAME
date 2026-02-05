using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    class RunCommandMetadata
    {
        public class CommandParameterInfo
        {
            public Type Type;
            public object Value;

            public string LookupName { get; set; }
            public LookupType LookupType { get; set; }

            public void SetValue(object newValue)
            {
                Value = newValue;
            }
        }

        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsUnsafe { get; set; }
        public Dictionary<string, CommandParameterInfo> Parameters { get; } = new ();
    }
}
