using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Utils;
using UnityEngine;

namespace Unity.AI.Assistant.FunctionCalling
{
    class FunctionToolbox
    {
        readonly Dictionary<string, CachedFunction> k_ToolsById = new();

        public static JsonSerializer ParameterSerializer { get; } = new() { Converters = { new StringEnumConverter() } };

        public IEnumerable<CachedFunction> Tools => k_ToolsById.Values;

        public void Initialize(FunctionCache functionCache)
        {
            foreach (var function in functionCache.AllFunctions)
            {
                RegisterFunction(function);
            }
        }

        public void RegisterFunction(CachedFunction function) => k_ToolsById.TryAdd(function.FunctionDefinition.FunctionId, function);

        public bool TryGetId(MethodInfo methodInfo, out string toolId)
        {
            toolId = null;
            if (methodInfo == null)
                return false;

            foreach (var toolIdToFunction in k_ToolsById)
            {
                if (toolIdToFunction.Value.Method == methodInfo)
                {
                    toolId = toolIdToFunction.Key;
                    return true;
                }
            }
            return false;
        }

        public bool TryGetMethod(string toolId, out MethodInfo methodInfo)
        {
            methodInfo = null;
            if (!k_ToolsById.TryGetValue(toolId, out var cachedFunction))
                return false;

            methodInfo = cachedFunction.Method;
            return true;
        }

        public FunctionDefinition GetFunctionDefinition(string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
                throw new ArgumentNullException(nameof(toolId));

            if (!k_ToolsById.TryGetValue(toolId, out var cachedFunction))
                throw new KeyNotFoundException($"Tool with id '{toolId}' not found.");

            return cachedFunction.FunctionDefinition;
        }

        public async Task<object> RunToolByIDAsync(string id, JObject parameters, ToolExecutionContext context)
        {
            GetSelectorIdAndConvertArgs(id, parameters, context, out var tool, out var convertedArgs);

            var result = await tool.InvokeAsync(convertedArgs);
            return result;
        }

        protected void GetSelectorIdAndConvertArgs(string id, JObject parameters, ToolExecutionContext context, out CachedFunction function, out object[] convertedArgs)
        {
            convertedArgs = null;
            if (!k_ToolsById.TryGetValue(id, out function))
                throw new Exception($"Tool {id} does not exist.");

            convertedArgs = ConvertJsonParametersToObjects(parameters, function, context);
        }

        /// <summary>
        /// Convert JObject parameters to properly typed objects for method invocation,
        /// injecting the ToolExecutionContext where required.
        /// </summary>
        static object[] ConvertJsonParametersToObjects(JObject functionParameters, CachedFunction cachedFunction, ToolExecutionContext context)
        {
            var llmParams = cachedFunction.FunctionDefinition.Parameters.ToDictionary(p => p.Name);
            var methodParams = cachedFunction.Method.GetParameters();
            var convertedArgs = new object[methodParams.Length];

            for (var i = 0; i < methodParams.Length; i++)
            {
                var pInfo = methodParams[i];
                var paramName = pInfo.Name;
                var targetType = pInfo.ParameterType;

                // If the parameter is a ToolExecutionContext, inject it. This allows it to be anywhere in the signature.
                if (targetType == typeof(ToolExecutionContext))
                {
                    convertedArgs[i] = context;
                    continue;
                }

                // For all other parameters, perform the robust name-based lookup and conversion.
                if (!llmParams.TryGetValue(paramName, out var paramDef))
                {
                    // This can happen if a method parameter doesn't have the [Parameter] attribute and is not the context.
                    // We assume it's an optional parameter that the C# compiler will handle.
                    if (pInfo.IsOptional)
                    {
                        convertedArgs[i] = pInfo.DefaultValue;
                        continue;
                    }
                    throw new InvalidOperationException($"Method parameter '{paramName}' is not an LLM parameter and has no default value.");
                }

                if (functionParameters.TryGetValue(paramName, out var paramValue))
                {
                    convertedArgs[i] = ConvertJTokenToObject(paramValue, targetType, paramDef);
                }
                else if (!paramDef.Optional)
                {
                    throw new ArgumentException($"Required parameter '{paramName}' not provided");
                }
                else
                {
                    convertedArgs[i] = paramDef.DefaultValue;
                }
            }

            return convertedArgs;
        }

        /// <summary>
        /// Convert JToken to object using a definitive target Type from reflection.
        /// </summary>
        protected static object ConvertJTokenToObject(JToken token, Type targetType, ParameterDefinition paramDef)
        {
            if (token == null || token.Type == JTokenType.Null)
                return null;

            // First, handle enum conversion directly.
            // This ensures that invalid enum values will throw an exception that is NOT caught below.
            if (targetType.IsEnum)
            {
                // Let the converter handle it. If it throws, the test will correctly catch it.
                return token.ToObject(targetType, ParameterSerializer);
            }

            // For all other types, use the try-catch block to allow for the fallback.
            try
            {
                return token.ToObject(targetType, ParameterSerializer);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to convert parameter '{paramDef.Name}' to type '{targetType.FullName}' with Json.NET: {ex.Message}. Falling back to schema-based conversion.");
                // Fallback to the old method if direct conversion fails (e.g., for complex types).
                return ConvertUsingJsonSchema(token, paramDef.JsonSchema);
            }
        }

        /// <summary>
        /// Convert JToken using JSON schema for fallback conversion.
        /// </summary>
        static object ConvertUsingJsonSchema(JToken token, JObject schema)
        {
            var schemaType = schema["type"]?.Value<string>();

            return schemaType switch
            {
                "string" => token.Value<string>(),
                "integer" => token.Value<long>(),
                "number" => token.Value<double>(),
                "boolean" => token.Value<bool>(),
                "array" => ConvertJsonSchemaArray(token, schema),
                "object" => ConvertJsonSchemaObject(token, schema),
                _ => AssistantJsonHelper.ToObject<object>(token)
            };
        }

        /// <summary>
        /// Convert JSON array using schema information.
        /// </summary>
        static object ConvertJsonSchemaArray(JToken token, JObject schema)
        {
            if (token.Type != JTokenType.Array)
                return AssistantJsonHelper.ToObject<object>(token);

            var itemsSchema = schema["items"] as JObject;
            var jArray = (JArray)token;
            var count = jArray.Count;
            var resultArray = new object[count];

            if (itemsSchema == null)
            {
                for (var i = 0; i < count; i++)
                    resultArray[i] = AssistantJsonHelper.ToObject<object>(jArray[i]);
            }
            else
            {
                for (var i = 0; i < count; i++)
                    resultArray[i] = ConvertUsingJsonSchema(jArray[i], itemsSchema);
            }

            return resultArray;
        }

        /// <summary>
        /// Convert JSON object using schema information.
        /// </summary>
        static object ConvertJsonSchemaObject(JToken token, JObject schema)
        {
            if (token.Type != JTokenType.Object)
                return AssistantJsonHelper.ToObject<object>(token);

            var result = new Dictionary<string, object>();
            var properties = schema["properties"] as JObject;

            foreach (var prop in ((JObject)token).Properties())
            {
                if (properties != null && properties.TryGetValue(prop.Name, out var property))
                {
                    var propSchema = property as JObject;
                    result[prop.Name] = ConvertUsingJsonSchema(prop.Value, propSchema);
                }
                else
                {
                    result[prop.Name] = AssistantJsonHelper.ToObject<object>(prop.Value);
                }
            }

            return result;
        }
    }
}
