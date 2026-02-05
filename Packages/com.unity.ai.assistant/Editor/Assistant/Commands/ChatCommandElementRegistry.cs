using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Editor.Commands
{
    static class ChatCommandElementRegistry
    {
        static readonly IDictionary<string, Func<VisualElement>> k_Registry = new Dictionary<string, Func<VisualElement>>();

        public static void Clear()
        {
            k_Registry.Clear();
        }

        public static void Register(string tag, Func<VisualElement> element)
        {
            k_Registry.Add(tag, element);
        }

        public static VisualElement CreateElement(string tag)
        {
            return k_Registry.TryGetValue(tag, out var callback) ? callback() : null;
        }
    }
}
