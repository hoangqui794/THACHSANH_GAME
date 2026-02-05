using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Data;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    class RunCommandInterpreter : IRunCommandInterpreter
    {
        Dictionary<int, ExecutionResult> m_CommandExecutions = new();

        public AgentRunCommand BuildRunCommand(string commandScript, IEnumerable<Object> contextAttachments)
            => RunCommandUtils.BuildRunCommand(commandScript, contextAttachments);

        public void StoreExecution(ExecutionResult executionResult, AssistantConversationId conversationId)
        {
            m_CommandExecutions.Add(executionResult.Id, executionResult);

            var executionHistory = GetExecutionHistory(conversationId);

            executionHistory.Add(executionResult);

            LocalConversationStorageHelper.Save(conversationId.Value, executionHistory);
        }

        public List<ExecutionResult> RestoreExecutions(AssistantConversationId conversationId)
        {
            m_CommandExecutions.Clear();

            var executionHistory = GetExecutionHistory(conversationId);
            foreach (var executionResult in executionHistory)
            {
                m_CommandExecutions.Add(executionResult.Id, executionResult);
            }

            return executionHistory;
        }

        List<ExecutionResult> GetExecutionHistory(AssistantConversationId conversationId)
        {
            return LocalConversationStorageHelper.Load<ExecutionResult>(conversationId.Value);
        }

        public int GetNextExecutionId(AssistantConversationId conversationId)
        {
            // Find the smallest unused execution ID:
            var executionHistory = GetExecutionHistory(conversationId);
            return executionHistory.Count > 0
                ? executionHistory.Max(e => e.Id) + 1
                : 1;
        }

        public ExecutionResult RetrieveExecution(int id)
        {
            return m_CommandExecutions.GetValueOrDefault(id);
        }

        public ExecutionResult Execute(AgentRunCommand command)
            => RunCommandUtils.Execute(command);
    }
}
