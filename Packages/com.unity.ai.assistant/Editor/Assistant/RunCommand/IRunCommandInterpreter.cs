using System.Collections.Generic;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Data;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    internal interface IRunCommandInterpreter
    {
        AgentRunCommand BuildRunCommand(string commandScript, IEnumerable<Object> contextAttachments);
        void StoreExecution(ExecutionResult executionResult, AssistantConversationId conversationId);
        List<ExecutionResult> RestoreExecutions(AssistantConversationId conversationId);
        int GetNextExecutionId(AssistantConversationId conversationId);
        ExecutionResult RetrieveExecution(int id);
        ExecutionResult Execute(AgentRunCommand command);
    }
}
