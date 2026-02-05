using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Socket.Workflows.Chat;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.FunctionCalling
{
    class AIAssistantFunctionCaller : IFunctionCaller
    {
        IToolPermissions ToolPermissions { get; }
        IToolInteractions ToolInteractions { get; }

        public AIAssistantFunctionCaller(IToolPermissions toolPermissions, IToolInteractions toolInteractions)
        {
            ToolPermissions = toolPermissions;
            ToolInteractions = toolInteractions;
        }

        /// <inheritdoc />
        public void CallByLLM(IChatWorkflow workFlow, string functionId, JObject functionParameters, Guid callId, CancellationToken cancellationToken)
        {
            MainThread.DispatchAndForgetAsync(async () =>
            {
                var conversationContext = new ConversationContext(workFlow);
                var result = await CallFunction(conversationContext, functionId, callId, functionParameters, cancellationToken);
                workFlow.SendFunctionCallResponse(result, callId);
            });
        }

        async Task<IFunctionCaller.CallResult> CallFunction(ConversationContext conversationContext, string functionId, Guid callId, JObject functionParameters, CancellationToken cancellationToken)
        {
            InternalLog.Log($"Calling tool: {functionId} ({callId}) with parameters: {functionParameters}");
            try
            {
                // Build execution context
                var callInfo = new ToolExecutionContext.CallInfo(functionId, callId, functionParameters);
                var permission = new ToolCallPermissions(callInfo, ToolPermissions, cancellationToken);
                var interactions = new ToolCallInteractions(callInfo, ToolInteractions, cancellationToken);
                var context = new ToolExecutionContext(conversationContext, callInfo, permission, interactions);

                // Check that tool can be executed
                await context.Permissions.CheckCanExecute();

                var result = await ToolRegistry.FunctionToolbox.RunToolByIDAsync(functionId, functionParameters, context);
                var jsonResult = result != null ? AssistantJsonHelper.FromObject(result, FunctionToolbox.ParameterSerializer) : JValue.CreateNull();

                return IFunctionCaller.CallResult.SuccessfulResult(jsonResult);
            }
            catch (Exception e)
            {
                InternalLog.LogWarning($"Calling tool {functionId} failed: {e.Message}\n{e.StackTrace}");
                return IFunctionCaller.CallResult.FailedResult(GetExceptionErrorMessage(e));
            }
        }

        /// <summary>
        /// Convert legacy string[] parameters to JObject format.
        /// </summary>
        static JObject ConvertStringArrayToJObject(string[] parameters)
        {
            var jsonParameters = new JObject();

            foreach (var param in parameters)
            {
                var colonIndex = param.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = param.Substring(0, colonIndex);
                    var value = param.Substring(colonIndex + 1);

                    try
                    {
                        jsonParameters[key] = JToken.Parse(value);
                    }
                    catch
                    {
                        jsonParameters[key] = value;
                    }
                }
            }

            return jsonParameters;
        }

        static string GetExceptionErrorMessage(Exception e)
        {
            return e.InnerException?.Message ?? e.Message;
        }
    }
}
