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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "EP0006 CodeFix Provider")]
    public class EpiPartialControllerCodeFixProvider : CodeFixProvider
    {
        // The Upgrade Assistant will only use analyzers that have an associated code fix provider registered including
        // the analyzer's ID in the code fix provider's FixableDiagnosticIds array.
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EpiPartialControllerAnalyzer.DiagnosticId);
        private const string MicrosoftMvcNamespace = "Microsoft.AspNetCore.Mvc";
        private const string EPiServerMvcNamespace = "EPiServer.Web.Mvc";

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
            var node = root.FindNode(diagnosticSpan);
            if (node is null)
            {
                return;
            }

            if (diagnostic.Properties.ContainsKey(EpiPartialControllerAnalyzer.PartialView) && node is IdentifierNameSyntax identifierNameSyntax)
            {
                context.RegisterCodeFix(
                   CodeAction.Create(
                       Resources.EpiPartialControllerTitle,
                       c => ReplacePartialViewMethodAsync(context.Document, identifierNameSyntax, c),
                       nameof(Resources.EpiPartialControllerTitle)),
                   diagnostic);
            }
            else if (node is MethodDeclarationSyntax methodDeclaration)
            {

                context.RegisterCodeFix(
                    CodeAction.Create(
                        Resources.EpiPartialControllerTitle,
                        c => ReplaceNameAndReturnParameterAsync(context.Document, methodDeclaration, c),
                        nameof(Resources.EpiPartialControllerTitle)),
                    diagnostic);
            }
        }

        private static async Task<Document> ReplaceNameAndReturnParameterAsync(Document document, MethodDeclarationSyntax localDeclaration, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var updatedName = localDeclaration.WithIdentifier(SyntaxFactory.Identifier("InvokeComponent"));
            var updatedReturnType = updatedName.WithReturnType(SyntaxFactory.ParseTypeName("IViewComponentResult"));
            var updatedAccessibility = updatedReturnType.WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.ProtectedKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword)));
            var newRoot = root!.ReplaceNode(localDeclaration, updatedAccessibility);

            // Return document with transformed tree.
            var updatedDocument = document.WithSyntaxRoot(newRoot);

            return await updatedDocument.AddUsingIfMissingAsync(cancellationToken, MicrosoftMvcNamespace, EPiServerMvcNamespace);
        }

        private static async Task<Document> ReplacePartialViewMethodAsync(Document document, IdentifierNameSyntax identifierNameSyntax, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var comment = SyntaxFactory.Comment($"//The default convention for views and viewcomponent is '/Views/Shared/Components/ComponentName/Default.cshtml'");
            var leadingTrivia = GetAncestorsTrivia(identifierNameSyntax);
            var updatedName = identifierNameSyntax.WithIdentifier(SyntaxFactory.Identifier(SyntaxTriviaList.Create(SyntaxFactory.CarriageReturnLineFeed).AddRange(leadingTrivia).Add(comment).Add(SyntaxFactory.CarriageReturnLineFeed).AddRange(leadingTrivia), "View", SyntaxTriviaList.Empty));
            var newRoot = root!.ReplaceNode(identifierNameSyntax, updatedName);
            // Return document with transformed tree.
            return document.WithSyntaxRoot(newRoot);
        }

        private static SyntaxTriviaList GetAncestorsTrivia(IdentifierNameSyntax identifierNameSyntax) =>
            identifierNameSyntax.Ancestors().Where(a => a.HasLeadingTrivia).FirstOrDefault()?.GetLeadingTrivia()?? SyntaxTriviaList.Empty;
    }
}
