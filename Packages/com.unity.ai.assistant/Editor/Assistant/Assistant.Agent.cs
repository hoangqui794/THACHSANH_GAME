using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.RunCommand;
using Unity.AI.Assistant.Editor.CodeBlock;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor
{
    internal partial class Assistant
    {
        /// <summary>
        /// Run C# command dynamically with Roslyn
        /// </summary>
        public IRunCommandInterpreter RunCommandInterpreter { get; } = new RunCommandInterpreter();

        public void RunAgentCommand(AssistantConversationId conversationId, AgentRunCommand command, string fencedTag)
        {
            var executionResult = RunCommandInterpreter.Execute(command);

            executionResult.FencedTag = fencedTag;
            if (m_ConversationCache.TryGetValue(conversationId, out var conversation))
            {
                executionResult.MessageIndex = conversation.Messages.Count;
                executionResult.Id = RunCommandInterpreter.GetNextExecutionId(conversation.Id);
            }

            RunCommandInterpreter.StoreExecution(executionResult, conversationId);

            StoreExecutionInConversation(conversationId, executionResult);
        }

        void StoreExecutionInConversation(AssistantConversationId conversationId, ExecutionResult executionResult)
        {
            if (m_ConversationCache.TryGetValue(conversationId, out var conversation))
            {
                AddInternalMessage(conversation, $"```{executionResult.FencedTag}\n{executionResult.Id}\n```", k_SystemRole, indexOverride: executionResult.MessageIndex);
            }
        }

        void UpdateLocalConversationFromPreviousExecutions(AssistantConversation conversation)
        {
            var executionHistory = RunCommandInterpreter.RestoreExecutions(conversation.Id);
            foreach (var execution in executionHistory)
            {
                StoreExecutionInConversation(conversation.Id, execution);
            }
        }

        public async Task SendEditRunCommand(AssistantMessageId messageId, string updatedCode)
        {
            // get the appropriate workflow
            if (messageId.ConversationId.IsValid)
            {
                var workflow = Backend.GetOrCreateWorkflow(await CredentialsProvider.GetCredentialsContext(), FunctionCaller, messageId.ConversationId);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Run(() =>
                {
                    workflow?.SendEditRunCommandRequest(messageId.FragmentId, updatedCode).WithExceptionLogging();
                });
#pragma warning restore CS4014
            }
        }

        public ExecutionResult GetRunCommandExecution(int executionId)
        {
            return RunCommandInterpreter.RetrieveExecution(executionId);
        }
    }
}
