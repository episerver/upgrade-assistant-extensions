// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "EP0001 CodeFix Provider")]
    public class EpiAttributeRemoverCodeFixProvider : CodeFixProvider
    {
        // The Upgrade Assistant will only use analyzers that have an associated code fix provider registered including
        // the analyzer's ID in the code fix provider's FixableDiagnosticIds array.
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EpiAttributeRemoverAnalyzer.DiagnosticId);

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

            var declaration = root.FindNode(diagnosticSpan);

            if (declaration is null)
            {
                return;
            }


            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
            CodeAction.Create(
                Resources.EpiAttributeRemoverTitle,
                c => RemoveDefaultPropertyAsync(context.Document, (AttributeSyntax)declaration, new CancellationToken()),
                nameof(Resources.EpiAttributeRemoverTitle)),
            diagnostic);
        }

        private static async Task<Document> RemoveDefaultPropertyAsync(Document document, AttributeSyntax localDeclaration, CancellationToken cancellationToken)
        {
            // Remove the leading trivia from the local declaration.
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newNode = localDeclaration;

            foreach (var arg in localDeclaration.ArgumentList.Arguments)
            {
                if (arg.NameEquals.Name.Identifier.Text == "Default")
                {
                    newNode = localDeclaration.RemoveNode(arg, SyntaxRemoveOptions.KeepNoTrivia);
                }
            }

            var updatedRoot = oldRoot!.ReplaceNode(localDeclaration, newNode);

            // Return document with transformed tree.
            return document.WithSyntaxRoot(updatedRoot);
        }
    }
}
