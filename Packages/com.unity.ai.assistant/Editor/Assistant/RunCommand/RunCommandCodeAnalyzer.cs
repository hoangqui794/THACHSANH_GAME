using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using UnityEngine;
using ExpressionEvaluator = Unity.AI.Assistant.Editor.CodeAnalyze.ExpressionEvaluator;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    static class RunCommandCodeAnalyzer
    {
        static readonly string[] k_UnauthorizedNamespaces = { "System.Net", "System.Diagnostics", "System.Runtime.InteropServices", "System.Reflection" };

        static readonly SymbolDisplayFormat s_FullyQualifiedFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
            globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted
        );

        static string[] k_UnsafeMethods = new[]
        {
            "UnityEditor.AssetDatabase.DeleteAsset",
            "UnityEditor.FileUtil.DeleteFileOrDirectory",
            "System.IO.File.Delete",
            "System.IO.Directory.Delete",
            "System.IO.File.Move",
            "System.IO.Directory.Move"
        };

        static ExpressionEvaluator s_ExpressionEvaluator = new ExpressionEvaluator();

        public static RunCommandMetadata AnalyzeCommandAndExtractMetadata(CSharpCompilation compilation)
        {
            var result = new RunCommandMetadata();

            var commandTree = compilation.SyntaxTrees.FirstOrDefault();
            if (commandTree == null)
                return result;

            var model = compilation.GetSemanticModel(commandTree);
            var root = commandTree.GetCompilationUnitRoot();

            var runCommandInterfaceSymbol = compilation.GetTypeByMetadataName(typeof(IRunCommand).FullName);
            if (runCommandInterfaceSymbol == null)
                return result;

            var commandClass = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(cds =>
                {
                    var classSymbol = model.GetDeclaredSymbol(cds);
                    return classSymbol != null &&
                           classSymbol.AllInterfaces.Contains(runCommandInterfaceSymbol,
                               SymbolEqualityComparer.Default);
                });

            if (commandClass != null)
            {
                ExtractClassAttributes(model, commandClass, result);
                ExtractCommandParameters(compilation, model, commandClass, result);
            }

            var walker = new PublicMethodCallWalker(model);
            walker.Visit(root);

            foreach (var methodCall in walker.PublicMethodCalls)
            {
                if (k_UnsafeMethods.Contains(methodCall))
                {
                    result.IsUnsafe = true;
                    break;
                }
            }

            return result;
        }

        static void ExtractClassAttributes(SemanticModel model, ClassDeclarationSyntax commandClass, RunCommandMetadata result)
        {
            // Extract Title from property
            var titleProperty = commandClass.Members
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(pds => pds.Identifier.ValueText == "Title");

            if (titleProperty?.ExpressionBody != null)
            {
                var constantValue = model.GetConstantValue(titleProperty.ExpressionBody.Expression);
                if (constantValue is { HasValue: true, Value: string title })
                {
                    result.Title = title;
                }
            }

            // Extract Description from property
            var descriptionProperty = commandClass.Members
                .OfType<PropertyDeclarationSyntax>()
                .FirstOrDefault(pds => pds.Identifier.ValueText == "Description");

            if (descriptionProperty?.ExpressionBody != null)
            {
                var constantValue = model.GetConstantValue(descriptionProperty.ExpressionBody.Expression);
                if (constantValue is { HasValue: true, Value: string description })
                {
                    result.Description = description;
                }
            }
        }

        static void ExtractCommandParameters(CSharpCompilation compilation, SemanticModel model,
            ClassDeclarationSyntax commandClass, RunCommandMetadata result)
        {
            var parameterAttributeSymbol =
                compilation.GetTypeByMetadataName(typeof(CommandParameterAttribute).FullName);
            if (parameterAttributeSymbol == null) return;

            var fieldsWithAttribute = commandClass.Members
                .OfType<FieldDeclarationSyntax>()
                .Where(fds => fds.AttributeLists.Any());

            foreach (var fieldDeclaration in fieldsWithAttribute)
            {
                var attributeSyntax = fieldDeclaration.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .FirstOrDefault(attr =>
                        SymbolEqualityComparer.Default.Equals(model.GetTypeInfo(attr).Type, parameterAttributeSymbol));

                if (attributeSyntax == null) continue;

                foreach (var variable in fieldDeclaration.Declaration.Variables)
                {
                    if (!(model.GetDeclaredSymbol(variable) is IFieldSymbol fieldSymbol)) continue;

                    var fieldType = FindType(fieldSymbol.Type);
                    if (fieldType == null)
                    {
                        Debug.LogWarning(
                            $"Could not resolve type '{fieldSymbol.Type}' for field '{fieldSymbol.Name}'.");
                        continue;
                    }

                    var (lookupType, lookupName) = ParseAttributeArguments(model, attributeSyntax);

                    object defaultValue = GetDefaultValue(fieldType);
                    if (variable.Initializer?.Value != null)
                    {
                        var expressionText = variable.Initializer.Value.ToString();
                        defaultValue = s_ExpressionEvaluator.Evaluate(expressionText);
                    }

                    result.Parameters[fieldSymbol.Name] = new RunCommandMetadata.CommandParameterInfo()
                    {
                        Type = fieldType, LookupType = lookupType, LookupName = lookupName, Value = defaultValue
                    };
                }
            }
        }

        static (LookupType, string) ParseAttributeArguments(SemanticModel model, AttributeSyntax attributeSyntax)
        {
            var lookupType = LookupType.Attachment; // Default
            var lookupName = ""; // Default

            if (attributeSyntax.ArgumentList == null) return (lookupType, lookupName);

            var attributeConstructor = model.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;
            if (attributeConstructor == null) return (lookupType, lookupName);

            for (int i = 0; i < attributeSyntax.ArgumentList.Arguments.Count; i++)
            {
                var argSyntax = attributeSyntax.ArgumentList.Arguments[i];
                var constantValue = model.GetConstantValue(argSyntax.Expression);
                if (!constantValue.HasValue) continue;

                string parameterName = argSyntax.NameColon?.Name.Identifier.ValueText ??
                                       (i < attributeConstructor.Parameters.Length
                                           ? attributeConstructor.Parameters[i].Name
                                           : null);

                if (parameterName == null) continue;

                switch (parameterName)
                {
                    case "lookupType" when constantValue.Value is int enumValue:
                        lookupType = (LookupType)enumValue;
                        break;
                    case "lookupName" when constantValue.Value is string stringValue:
                        lookupName = stringValue;
                        break;
                }
            }

            return (lookupType, lookupName);
        }

        public static bool HasUnauthorizedNamespaceUsage(string script)
        {
            var tree = SyntaxFactory.ParseSyntaxTree(script);
            return tree.ContainsNamespaces(k_UnauthorizedNamespaces);
        }

        static Type FindType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol is INamedTypeSymbol { IsGenericType: true } namedTypeSymbol)
            {
                var genericTypeDefName = namedTypeSymbol.ConstructedFrom.ToDisplayString(s_FullyQualifiedFormat);
                var genericTypeDef = Type.GetType(genericTypeDefName + "`" + namedTypeSymbol.TypeArguments.Length);

                if (genericTypeDef != null)
                {
                    var typeArgs = namedTypeSymbol.TypeArguments.Select(FindType).ToArray();
                    if (typeArgs.All(t => t != null))
                        return genericTypeDef.MakeGenericType(typeArgs);
                }
            }

            var fullyQualifiedTypeName = typeSymbol.ToDisplayString(s_FullyQualifiedFormat);
            return Type.GetType(fullyQualifiedTypeName) ??
                   AppDomain.CurrentDomain.GetAssemblies()
                       .Select(assembly => assembly.GetType(fullyQualifiedTypeName))
                       .FirstOrDefault(type => type != null);
        }


        static object GetDefaultValue(Type type)
        {
            if (type == null)
                return null;

            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
