using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Unity.AI.Assistant.Editor.CodeAnalyze
{
    class FixMissingMetadataInCommands : CSharpFixProvider
    {
        static readonly string[] k_DiagnosticIds = { "CS0535" };

        public override bool CanFix(Diagnostic diagnostic)
        {
            if (!k_DiagnosticIds.Contains(diagnostic.Id))
                return false;

            var message = diagnostic.GetMessage();
            return message.Contains("'IRunCommand.Description'");
        }

        public override SyntaxTree ApplyFix(SyntaxTree tree, Diagnostic diagnostic)
        {
            var root = tree.GetRoot();

            // Extract IRunCommand class
            var classDeclaration = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c =>
                    c.BaseList != null &&
                    c.BaseList.Types.Any(t => t.Type is IdentifierNameSyntax { Identifier: { Text: "IRunCommand" } }));
            if (classDeclaration == null)
                return tree;

            var nodesToRemove = new List<SyntaxNode>();

            // Extract Title from CommandDescription attribute
            string titleText = string.Empty;
            AttributeSyntax commandTitleAttribute = null;
            AttributeListSyntax attributeListToRemove = null;

            if (classDeclaration.AttributeLists.Count > 0)
            {
                foreach (var attrList in classDeclaration.AttributeLists)
                {
                    commandTitleAttribute = attrList.Attributes
                        .FirstOrDefault(attr => attr.Name.ToString().EndsWith("CommandDescription"));

                    if (commandTitleAttribute != null)
                    {
                        attributeListToRemove = attrList;
                        break;
                    }
                }

                if (commandTitleAttribute?.ArgumentList?.Arguments.Count > 0)
                {
                    var firstArg = commandTitleAttribute.ArgumentList.Arguments[0];
                    if (firstArg.Expression is LiteralExpressionSyntax titleLiteral &&
                        titleLiteral.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        titleText = titleLiteral.Token.ValueText;
                    }

                    if (attributeListToRemove.Attributes.Count == 1)
                        nodesToRemove.Add(attributeListToRemove);
                    else
                        nodesToRemove.Add(commandTitleAttribute);
                }
            }

            // Extract BuildPreview method
            var descriptionText = string.Empty;
            var previewMethod = classDeclaration.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(m => m.Identifier.Text == "BuildPreview");

            if (previewMethod != null)
            {
                // Extract Append call string
                var appendCalls = previewMethod.DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(inv =>
                        inv.Expression is MemberAccessExpressionSyntax memberAccess &&
                        memberAccess.Name.Identifier.Text == "Append" &&
                        inv.ArgumentList.Arguments.Count == 1 &&
                        inv.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax literal &&
                        literal.IsKind(SyntaxKind.StringLiteralExpression))
                    .ToList();

                if (appendCalls.Count != 0)
                {
                    descriptionText = string.Join('\n', appendCalls
                        .Select(call =>
                            ((LiteralExpressionSyntax)call.ArgumentList.Arguments[0].Expression).Token.ValueText));
                }

                nodesToRemove.Add(previewMethod);
            }

            // Create property Description
            var descriptionLiteralExpr = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                SyntaxFactory.Literal(descriptionText));

            var propertyDeclaration = SyntaxFactory.PropertyDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)), "Description")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(descriptionLiteralExpr))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            // Create Title property
            var titleLiteralExpr = SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(titleText));

            var titleProperty = SyntaxFactory.PropertyDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)), "Title")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(titleLiteralExpr))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                .WithLeadingTrivia(SyntaxFactory.CarriageReturnLineFeed)
                .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);

            // Update class
            var updatedClass = classDeclaration;

            // Remove all unnecessary code
            updatedClass = updatedClass.RemoveNodes(nodesToRemove, SyntaxRemoveOptions.KeepNoTrivia) ?? updatedClass;

            // Add new properties
            var membersToAdd = new List<MemberDeclarationSyntax> { titleProperty, propertyDeclaration };

            var insertIndex = 0;
            foreach (var member in membersToAdd)
            {
                updatedClass = updatedClass.WithMembers(updatedClass.Members.Insert(insertIndex, member));
                insertIndex++;
            }

            var newRoot = root.ReplaceNode(classDeclaration, updatedClass);
            return SyntaxFactory.SyntaxTree(newRoot.NormalizeWhitespace());
        }
    }
}
