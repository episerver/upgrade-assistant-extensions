// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Epi.Source.Updater
{
    /// <summary>
    /// As with the analzyers, code fix providers that are registered into Upgrade Assistant's
    /// dependency injection container (by IExtensionServiceProvider) will be used during
    /// the source update step.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "EP0003 CodeFix Provider")]
    public class EpiMemberReplacementCodeFixProvider : CodeFixProvider
    {
        // The Upgrade Assistant will only use analyzers that have an associated code fix provider registered including
        // the analyzer's ID in the code fix provider's FixableDiagnosticIds array.
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EpiMemberReplacementAnalyzer.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root is null)
            {
                return;
            }

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var memberDeclaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().First();

            if (memberDeclaration is null)
            {
                return;
            }

            var construnctorDeclaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ConstructorDeclarationSyntax>().First();

            if (construnctorDeclaration is null)
            {
                return;
            }

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    Resources.EpiMemberReplacementTitle,
                    c => ReplaceMamberAsync(context.Document, memberDeclaration, c),
                    nameof(Resources.EpiMemberReplacementTitle)),
                diagnostic);

            context.RegisterCodeFix(
                CodeAction.Create(
                    Resources.EpiMemberReplacementTitle,
                    c => ReplaceConstructorAsync(context.Document, construnctorDeclaration, c),
                    nameof(Resources.EpiMemberReplacementTitle)),
                diagnostic);
        }

        private static async Task<Document> ReplaceMamberAsync(Document document, MemberDeclarationSyntax localDeclaration, CancellationToken cancellationToken)
        {
            // Remove PropertyData ParseToObject method.
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot!.RemoveNode(localDeclaration, SyntaxRemoveOptions.AddElasticMarker);

    //        ConstructorDeclaration("TestClientApi")
    //.WithInitializer(
    //    ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
    //        // could be BaseConstructorInitializer or ThisConstructorInitializer
    //        .AddArgumentListArguments(
    //            Argument(IdentifierName("entryPoint"))
    //        )
    //)

            // Return document with transformed tree.
            return document.WithSyntaxRoot(newRoot);
        }

        private static async Task<Document> ReplaceConstructorAsync(Document document, ConstructorDeclarationSyntax localDeclaration, CancellationToken cancellationToken)
        {
            // Remove PropertyData ParseToObject method.
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newRoot = oldRoot!.RemoveNode(localDeclaration, SyntaxRemoveOptions.AddElasticMarker);

            // Return document with transformed tree.
            return document.WithSyntaxRoot(newRoot);
        }
    }
}

