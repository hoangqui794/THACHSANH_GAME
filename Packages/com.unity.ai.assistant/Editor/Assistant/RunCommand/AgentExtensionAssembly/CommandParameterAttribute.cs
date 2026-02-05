using System;

namespace Unity.AI.Assistant.Agent.Dynamic.Extension.Editor
{
#if CODE_LIBRARY_INSTALLED
    public
#else
    internal
#endif
        enum LookupType
    {
        Attachment,
        Scene,
        Asset
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
#if CODE_LIBRARY_INSTALLED
    public
#else
    internal
#endif
    class CommandParameterAttribute : Attribute
    {
        public string LookupName { get; }
        public LookupType LookupType { get; }

        public CommandParameterAttribute(LookupType lookupType = LookupType.Attachment, string lookupName = "")
        {
            LookupName = lookupName;
            LookupType = lookupType;
        }
    }
}
