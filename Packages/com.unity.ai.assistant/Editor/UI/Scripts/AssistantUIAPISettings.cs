using Unity.AI.Assistant.Editor.Commands;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    internal static class AssistantUIAPISettings
    {
        public static void Initialize()
        {
            ChatCommandElementRegistry.Clear();

            ChatCommandElementRegistry.Register("csharp_validate", () => new ChatElementCommandCodeBlock { ValidateCode = true });
            ChatCommandElementRegistry.Register("uxml", () => new ChatElementUI());
        }
    }
}
