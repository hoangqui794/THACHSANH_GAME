using System.Collections.Generic;

namespace Unity.AI.Assistant.Agent.Dynamic.Extension.Editor
{
#if CODE_LIBRARY_INSTALLED
    public
#else
    internal
#endif
    class PreviewBuilder
    {
        List<string> m_Preview = new();

        public List<string> Preview => m_Preview;

        public void Append(string text)
        {
            m_Preview.Add(text);
        }
    }
}
