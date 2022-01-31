// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Epi.Source.Updater
{
    /// <summary>
    /// As with the analzyers, code fix providers that are registered into Upgrade Assistant's
    /// dependency injection container (by IExtensionServiceProvider) will be used during
    /// the source update step.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "EP0008 CodeFix Provider")]
    public class EpiMetadataAwareCodeFixProvider : CodeFixProvider
    {
        // The Upgrade Assistant will only use analyzers that have an associated code fix provider registered including
        // the analyzer's ID in the code fix provider's FixableDiagnosticIds array.
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EpiMetadataAwareAnalyzer.DiagnosticId);
        private const string MicrosoftMetadataNamespace = "Microsoft.AspNetCore.Mvc.ModelBinding.Metadata";

        public sealed override FixAllProvider GetFixAllProvider() =>
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            WellKnownFixAllProviders.BatchFixer;

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root is null)
            {
                return;
            }

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan) as ClassDeclarationSyntax;
            if (node is null)
            {
                return;
            }

            context.RegisterCodeFix(
                    CodeAction.Create(
                        Resources.MetadataAwareTitle,
                        c => RefactorToDisplayModeProvider(context.Document, node, c),
                        nameof(Resources.MetadataAwareTitle)),
                    diagnostic);
        }

        private async Task<Document> RefactorToDisplayModeProvider(Document document, ClassDeclarationSyntax node, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var metadataType = node.BaseList.Types.OfType<BaseTypeSyntax>().FirstOrDefault(t => t.Type is IdentifierNameSyntax nameSyntax && nameSyntax.Identifier.Text == "IMetadataAware");
            var baseListWithoutInterface = node.BaseList.RemoveNode(metadataType, SyntaxRemoveOptions.KeepLeadingTrivia | SyntaxRemoveOptions.KeepTrailingTrivia | SyntaxRemoveOptions.KeepEndOfLine);
            var baseListWithDisplayModeProvider = baseListWithoutInterface.AddTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("IDisplayMetadataProvider"))); 
            var withDisplayModeProvider = node.WithBaseList(baseListWithDisplayModeProvider);

            var withReplacedMethod = RefactorMethod(withDisplayModeProvider);

            var newRoot = root.ReplaceNode(node, withReplacedMethod);

            // Return document with transformed tree.
            var updatedDocument = document.WithSyntaxRoot(newRoot);
            return await updatedDocument.AddUsingIfMissingAsync(cancellationToken, MicrosoftMetadataNamespace);
        }

        private ClassDeclarationSyntax RefactorMethod(ClassDeclarationSyntax classDeclaration)
        {
            var onMetadataCreated = classDeclaration.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.Text == "OnMetadataCreated");
            if (onMetadataCreated != null)
            {
                var originalParameterName = onMetadataCreated.ParameterList.Parameters[0].Identifier.Text;
                var withUpdatedName = onMetadataCreated.WithIdentifier(SyntaxFactory.Identifier("CreateDisplayMetadata"));
                var updatedParameter = onMetadataCreated.ParameterList.Parameters[0].WithIdentifier(SyntaxFactory.Identifier("context")).WithType(SyntaxFactory.ParseTypeName("DisplayMetadataProviderContext")).WithTrailingTrivia();
                var withUpdatedParameterList = withUpdatedName.WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(updatedParameter)));

                var withUpdatedBody = withUpdatedParameterList.WithBody(withUpdatedParameterList.Body.WithStatements(
                    withUpdatedName.Body.Statements.Insert(0, SyntaxFactory.ParseStatement($"var {originalParameterName} = context.DisplayMetadata.AdditionalValues[ExtendedMetadata.ExtendedMetadataDisplayKey] as ExtendedMetadata;")
                        .WithLeadingTrivia(withUpdatedParameterList.Body.Statements[0].GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed))));

                var baseCallIndex = -1;
                for (var i = 0; i < withUpdatedBody.Body.Statements.Count; i++)
                {
                    var statement = withUpdatedBody.Body.Statements[i];
                    if (statement is ExpressionStatementSyntax expressionStatement && expressionStatement.Expression is InvocationExpressionSyntax invocationExpression)
                    {
                        if (invocationExpression.Expression is MemberAccessExpressionSyntax memberAccessSyntax && memberAccessSyntax.Name.Identifier.Text == $"OnMetadataCreated")
                        {
                            baseCallIndex = i;
                            break;
                        }
                    }
                }

                if (baseCallIndex > -1)
                {
                    var newStatement = SyntaxFactory.ParseStatement($"base.CreateDisplayMetadata(context);")
                        .WithLeadingTrivia(withUpdatedBody.Body.Statements[baseCallIndex].GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed);
                    var statements = withUpdatedBody.Body.Statements.RemoveAt(baseCallIndex);
                    statements = statements.Insert(baseCallIndex, newStatement);
                    withUpdatedBody = withUpdatedBody.WithBody(withUpdatedBody.Body.WithStatements(statements));
                }

                var members = classDeclaration.Members.Remove(onMetadataCreated);
                members = members.Add(withUpdatedBody);
                classDeclaration = classDeclaration.WithMembers(members);
            }

            return classDeclaration;
        }
    }
}
