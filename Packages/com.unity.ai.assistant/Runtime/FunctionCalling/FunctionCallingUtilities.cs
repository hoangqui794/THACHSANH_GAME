using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.ApplicationModels;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Utils;
using UnityEngine;
using UnityEngine.Pool;


namespace Unity.AI.Assistant.FunctionCalling
{
    static class FunctionCallingUtilities
    {
        internal const string k_SmartContextTag = "smart-context";
        internal const string k_AgentToolTag = "agent-tool";
        internal const string k_StaticContextTag = "static-context";
        internal const string k_CodeCorrectionTag = "code-correction";
        internal const string k_CodeExecutionTag = "code-execution";
        internal const string k_UITag = "ui";
        internal const string k_GameObjectTag = "game-object";
        internal const string k_CodeEditTag = "code-edit";
        internal const string k_PlayModeTag = "play-mode";
        internal const string k_ProjectOverviewTag = "project-overview";


        /// <summary>
        /// Validates that the current Unity editor mode matches the required mode for a function.
        /// Throws an InvalidOperationException with detailed error message if validation fails.
        /// </summary>
        /// <param name="requiredModes">The required editor modes for the function (as flags)</param>
        /// <exception cref="InvalidOperationException">Thrown when the current editor mode doesn't match any of the required modes</exception>
        internal static void ValidateEnvironmentOrThrow(ToolCallEnvironment requiredModes)
        {
            if (requiredModes == 0)
                LogAndThrow("Tool does not declare required modes");

            UnityEnvironment currentMode = EnvironmentUtils.GetEnvironment();

            // Check if current mode matches any of the required modes using flags
            switch (currentMode)
            {
                case UnityEnvironment.EditMode:
                    if (requiredModes.HasFlag(ToolCallEnvironment.EditMode))
                        return; // Valid
                    break;

                case UnityEnvironment.PlayMode:
                    if (requiredModes.HasFlag(ToolCallEnvironment.PlayMode))
                        return; // Valid
                    break;

                case UnityEnvironment.Runtime:
                    if (requiredModes.HasFlag(ToolCallEnvironment.Runtime))
                        return; // Valid
                    break;
            }

            // Generate specific error messages based on current mode and requirements
            var modeList = new List<string>();
            if (requiredModes.HasFlag(ToolCallEnvironment.Runtime))
                modeList.Add("Runtime Mode");
            if (requiredModes.HasFlag(ToolCallEnvironment.PlayMode))
                modeList.Add("Play Mode");
            if (requiredModes.HasFlag(ToolCallEnvironment.EditMode))
                modeList.Add("Edit Mode");
            string requiredModesText = modeList.Count == 1 ? modeList[0] : string.Join(" or ", modeList);

            string errorMessage;
            if (currentMode == UnityEnvironment.PlayMode && requiredModes == ToolCallEnvironment.EditMode)
            {
                errorMessage = "The Unity Editor is currently in Play Mode but this tool requires the editor to be in Edit Mode. ";
            }
            else if (currentMode == UnityEnvironment.EditMode && requiredModes == ToolCallEnvironment.PlayMode)
            {
                errorMessage = "The Unity Editor is currently in Edit Mode but this tool requires the editor to be in Play Mode. ";
            }
            else if (currentMode == UnityEnvironment.Runtime)
            {
                errorMessage = $"This tool requires Unity to be in {requiredModesText}, but the game is currently running in Runtime Mode. " +
                               "The tool needs to run in the Unity Editor.";
            }
            else
            {
                // Generic fallback
                errorMessage = $"This tool requires Unity to be in {requiredModesText}, but Unity is currently in {currentMode.ToString()}. ";
            }

            LogAndThrow(errorMessage);
        }

        internal static void LogAndThrow(String errorMessage)
        {
            Debug.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        /// <summary>
        /// Get a <see cref="ParameterDefinition"/> used to serialize parameters and send them to the server.
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="toolMethod"></param>
        /// <returns></returns>
        internal static ParameterDefinition GetParameterDefinition(ParameterInfo parameter, MethodInfo toolMethod)
        {
            var parameterAttribute = parameter.GetCustomAttribute<ParameterAttribute>();
            if (parameterAttribute == null)
            {
                Debug.LogWarning(
                    $"Method \"{toolMethod.Name}\" in \"{toolMethod.DeclaringType?.FullName}\" contains the parameter \"{parameter.Name}\" that must marked with the {nameof(ParameterAttribute)} attribute. This method will be ignored.");
                return null;
            }

            var parameterType = parameter.ParameterType;
            var parameterTypeName = parameterType.Name;
            var isOptional = parameter.IsDefined(typeof(ParamArrayAttribute), false) || parameter.HasDefaultValue;

            var defaultValue = parameter.HasDefaultValue ? parameter.DefaultValue : null;

            // Always generate JSON schema for ALL parameter types
            var jsonSchema = GenerateJsonSchema(parameterType);
            var def = new ParameterDefinition(parameterAttribute.Description, parameter.Name, parameterTypeName, jsonSchema, isOptional, defaultValue);
            return def;
        }

        /// <summary>
        /// Generate JSON schema from C# type using reflection
        /// </summary>
        static JObject GenerateJsonSchema(Type type)
        {
            var visitedTypes = HashSetPool<Type>.Get();
            try
            {
                return GenerateJsonSchemaInternal(type, visitedTypes);
            }
            catch (ArgumentException)
            {
                // Re-throw ArgumentExceptions (e.g., unsupported Dictionary key types)
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Failed to generate JSON schema for type {type.Name}: {ex.Message}");
                // Fallback to basic object schema for unexpected errors
                return new JObject
                {
                    ["type"] = "object",
                    ["description"] = $"Complex type: {type.Name}"
                };
            }
            finally
            {
                HashSetPool<Type>.Release(visitedTypes);
            }
        }

        /// <summary>
        /// Internal recursive schema generation with cycle detection
        /// </summary>
        static JObject GenerateJsonSchemaInternal(Type type, HashSet<Type> visitedTypes)
        {
            // Handle nullable types
            if (IsNullableType(type))
                type = Nullable.GetUnderlyingType(type);

            // Prevent infinite recursion
            if (visitedTypes.Contains(type))
            {
                return new JObject
                {
                    ["type"] = "object",
                    ["description"] = $"Circular reference to {type.Name}"
                };
            }

            visitedTypes.Add(type);

            var schema = new JObject();

            // Handle primitive types first
            if (type == typeof(string))
            {
                schema["type"] = "string";
                visitedTypes.Remove(type);
                return schema;
            }
            if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte))
            {
                schema["type"] = "integer";
                visitedTypes.Remove(type);
                return schema;
            }
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
            {
                schema["type"] = "number";
                visitedTypes.Remove(type);
                return schema;
            }
            if (type == typeof(bool))
            {
                schema["type"] = "boolean";
                visitedTypes.Remove(type);
                return schema;
            }

            // Handle enums
            if (type.IsEnum)
            {
                schema["type"] = "string";
                schema["enum"] = new JArray(Enum.GetNames(type));
                visitedTypes.Remove(type);
                return schema;
            }

            // Handle arrays
            if (type.IsArray)
            {
                schema["type"] = "array";
                schema["items"] = GenerateJsonSchemaInternal(type.GetElementType(), visitedTypes);
                return schema;
            }

            // Handle dictionaries
            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(Dictionary<,>) || genericTypeDefinition == typeof(IDictionary<,>))
                {
                    var keyType = type.GetGenericArguments()[0];
                    var valueType = type.GetGenericArguments()[1];

                    schema["type"] = "object";
                    // For now, we assume string keys (most common case)
                    // TODO: Handle non-string keys if needed in the future
                    if (keyType == typeof(string))
                    {
                        schema["additionalProperties"] = GenerateJsonSchemaInternal(valueType, visitedTypes);
                    }
                    else
                    {
                        throw new ArgumentException($"Dictionary keys of type '{keyType.Name}' are not supported. Only string keys are supported for Dictionary JSON schema generation.");
                    }
                    return schema;
                }
            }

            // Handle generic collections
            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(List<>) || genericTypeDefinition == typeof(IEnumerable<>) ||
                    genericTypeDefinition == typeof(ICollection<>) || genericTypeDefinition == typeof(IList<>))
                {
                    schema["type"] = "array";
                    schema["items"] = GenerateJsonSchemaInternal(type.GetGenericArguments()[0], visitedTypes);
                    return schema;
                }
            }

            // Handle custom classes/structs as objects
            schema["type"] = "object";
            schema["properties"] = new JObject();

            // Get public properties and fields
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var propSchema = GenerateJsonSchemaInternal(prop.PropertyType, visitedTypes);
                schema["properties"][prop.Name] = propSchema;
            }

            foreach (var field in fields)
            {
                var fieldSchema = GenerateJsonSchemaInternal(field.FieldType, visitedTypes);
                schema["properties"][field.Name] = fieldSchema;
            }

            visitedTypes.Remove(type);
            return schema;
        }


        /// <summary>
        /// Check if type is nullable
        /// </summary>
        static bool IsNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
        }

        /// <summary>
        /// Transforms a MethodInfo and a description into a FunctionDefinition that can be sent to the server.
        /// </summary>
        /// <param name="method">The method info this definition should define</param>
        /// <param name="description">The user written description destined for the LLM</param>
        /// <param name="id">Unique id of the tool. If null is provided, it'll be automatically determined.</param>
        /// <param name="assistantMode">The supported assistant modes for this tool</param>
        /// <param name="tags">Any tags associated with the function</param>
        /// <returns></returns>
        internal static FunctionDefinition GetFunctionDefinition(MethodInfo method, string description, string id,
            AssistantMode assistantMode, params string[] tags)
        {
            var parameters = method.GetParameters();

            bool valid = true;

            // Skip the first parameter if it's a ToolExecutionContext
            var startIndex = 0;
            if (parameters.Length > 0 && parameters[0].ParameterType == typeof(ToolExecutionContext))
                startIndex = 1;

            // Create parameter info list:
            var toolParameters = new List<ParameterDefinition>(parameters.Length - startIndex);
            for (var parameterIndex = startIndex; parameterIndex < parameters.Length; parameterIndex++)
            {
                var parameter = parameters[parameterIndex];
                var parameterInfo = GetParameterDefinition(parameter, method);
                if (parameterInfo == null)
                {
                    valid = false;
                    break;
                }

                toolParameters.Add(parameterInfo);
            }

            if (!valid)
            {
                return null;
            }

            return new FunctionDefinition(description, method.Name)
            {
                Namespace = method.DeclaringType.FullName,
                FunctionId = id ?? $"{method.DeclaringType.FullName.Replace('+', '.')}.{method.Name}",
                Parameters = toolParameters,
                AssistantMode = assistantMode,
                Tags = tags.ToList()
            };
        }
    }
}
