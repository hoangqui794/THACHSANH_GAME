using System.ComponentModel;

namespace Unity.AI.Assistant.Editor.Commands
{
    enum ChatCommandType
    {
        [Description("Ask questions and get help")]
        Ask,
        [Description("Give Assistant a task to do")]
        Run,
        [Description("Generate code")]
        Code,
        [Description("Generate Match Three")]
        Match3,
    }
}
