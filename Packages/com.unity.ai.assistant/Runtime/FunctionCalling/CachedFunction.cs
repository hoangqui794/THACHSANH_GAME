using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Utils;
using UnityEngine;

namespace Unity.AI.Assistant.FunctionCalling
{
    class CachedFunction
    {
        /// <summary>
        /// This is metadata associated with the function that can be provided by a <see cref="IFunctionSource"/>. The
        /// keys and values of function metadata depends on the FunctionSource.
        /// </summary>
        public Dictionary<string, string> MetaData;
        public MethodInfo Method;
        public FunctionDefinition FunctionDefinition;

        /// <summary>
        /// If true, the function's first parameter is an optional ToolExecutionContext
        /// </summary>
        public bool HasContextParameter;

        /// <summary>
        /// The required editor mode for this function (flags).
        /// </summary>
        public ToolCallEnvironment ToolCallEnvironment;

        public object Invoke(object[] parameters)
        {
            if (Method == null)
            {
                InternalLog.LogError("Trying to invoke a null function!");
                return null;
            }

            FunctionCallingUtilities.ValidateEnvironmentOrThrow(ToolCallEnvironment);

            // Is this an async function?  Then log a warning and return null
            var isAsync = Method.GetCustomAttribute<AsyncStateMachineAttribute>() != null;

            if (isAsync)
                throw new Exception($"{Method.Name} is an async function, use InvokeAsync(...) instead.");

            return Method.Invoke(null, parameters);
        }

        public async Task<object> InvokeAsync(object[] parameters)
        {
            if (Method == null)
            {
                InternalLog.LogError("Trying to invoke a null function!");
                return null;
            }

            FunctionCallingUtilities.ValidateEnvironmentOrThrow(ToolCallEnvironment);

            var isAsync = Method.GetCustomAttribute<AsyncStateMachineAttribute>() != null;

            object result;

            if (isAsync)
            {
                result = await InvokeAsyncInternal(parameters);
            }
            else
            {
                result = Method.Invoke(null, parameters);
            }

            return result;
        }

        async Task<object> InvokeAsyncInternal(object[] parameters)
        {
            var task = (Task)Method.Invoke(null, parameters);
            await task;

            if (task.GetType().IsGenericType)
            {
                var resultProperty = task.GetType().GetProperty("Result");
                var result = resultProperty.GetValue(task);

                return result;
            }

            return null;
        }
    }
}
