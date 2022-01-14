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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "EP0007 CodeFix Provider")]
    public class EpiDisplayChannelCodeFixProvider : CodeFixProvider
    {
        // The Upgrade Assistant will only use analyzers that have an associated code fix provider registered including
        // the analyzer's ID in the code fix provider's FixableDiagnosticIds array.
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EpiDisplayChannelAnalyzer.DiagnosticId);
        private const string HttpNamespace = "Microsoft.AspNetCore.Http";

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
            var methodDeclaration = root.FindNode(diagnosticSpan) as MethodDeclarationSyntax;
            if (methodDeclaration is null)
            {
                return;
            }

            context.RegisterCodeFix(
                     CodeAction.Create(
                         Resources.EpiDisplayChannelTitle,
                         c => ReplaceNameAndReturnParameterAsync(context.Document, methodDeclaration, c),
                         nameof(Resources.EpiDisplayChannelTitle)),
                     diagnostic);
        }

        private static async Task<Document> ReplaceNameAndReturnParameterAsync(Document document, MethodDeclarationSyntax localDeclaration, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var updatedParameter = localDeclaration.ParameterList.Parameters[0].WithType(SyntaxFactory.ParseTypeName("HttpContext")).WithTrailingTrivia();
            var updatedParameterList = localDeclaration.WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList<ParameterSyntax>(updatedParameter)));
            var newRoot = root!.ReplaceNode(localDeclaration, updatedParameterList);

            // Return document with transformed tree.
            var updatedDocument = document.WithSyntaxRoot(newRoot);
            return await updatedDocument.AddUsingIfMissingAsync(cancellationToken, HttpNamespace);
        }
    }
}
