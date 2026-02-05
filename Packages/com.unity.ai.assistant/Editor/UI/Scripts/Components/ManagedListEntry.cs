using System;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class ManagedListEntry : ManagedTemplate
    {
        int m_Index;

        protected ManagedListEntry(string basePath = null)
            : base(basePath ?? AssistantUIConstants.UIModulePath)
        {
        }

        public int Index => m_Index;

        public bool DidComeIntoView { get; protected set; }

        public virtual void SetData(int index, object data, bool isSelected = false)
        {
            m_Index = index;
        }

        public virtual bool CameIntoView()
        {
            DidComeIntoView = true;
            return false;
        }
    }
}
