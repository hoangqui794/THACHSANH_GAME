using System.Text.RegularExpressions;
using Unity.AI.Assistant.Data;

namespace Unity.AI.Assistant.Editor.Commands
{
    internal static class ChatCommandParser
    {
        private static readonly Regex s_CommandPattern = new Regex(@"^\/(\w+)\s+(.*)", RegexOptions.Singleline);

        internal static bool IsCommand(string text)
        {
            return text.StartsWith('/');
        }

        internal static bool IsValidCommand(string text)
        {
            bool startsWithSlash = text.StartsWith('/');

            if (startsWithSlash)
            {
                return ChatCommands.TryGetCommandHandler(text.Remove(0, 1), out var handler);
            }

            return false;
        }

        public static string Parse(AssistantPrompt input)
        {
            var match = s_CommandPattern.Match(input.Value);

            if (match.Success)
            {
                var cmdText = match.Groups[1].Value.ToLower();
                input.Value = match.Groups[2].Value;

                if (ChatCommands.TryGetCommandHandler(cmdText, out var handler))
                    return cmdText;

                // If command is unknown default, to Ask
                return AskCommand.k_CommandName;
            }

            return AskCommand.k_CommandName;
        }

        public static bool Parse(string prompt, out ChatCommandHandler handler)
        {
            var match = s_CommandPattern.Match(prompt);

            if (match.Success)
            {
                var cmdText = match.Groups[1].Value.ToLower();

                if (ChatCommands.TryGetCommandHandler(cmdText, out handler))
                {
                    return true;
                }
            }

            handler = null;
            return false;
        }
    }
}
