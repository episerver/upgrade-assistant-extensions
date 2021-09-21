// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "EP0003 CodeFix Provider")]
    public class FindUIConfigurationReplacementCodeFixProvider : CodeFixProvider
    {
        // The Upgrade Assistant will only use analyzers that have an associated code fix provider registered including
        // the analyzer's ID in the code fix provider's FixableDiagnosticIds array.
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(FindUIConfigurationReplacementAnalyzer.DiagnosticId);

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

            foreach(var diagnostic in context.Diagnostics)
            {
                var diagnosticSpan = diagnostic.Location.SourceSpan;
                var memberDeclaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<VariableDeclarationSyntax>().FirstOrDefault();
                var parameterDeclaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ParameterSyntax>().FirstOrDefault();

                if (parameterDeclaration is null && memberDeclaration is null)
                {
                    continue;
                }

                // Register a code action that will invoke the fix.
                if (memberDeclaration != null)
                {
                    context.RegisterCodeFix(
                       CodeAction.Create(
                           Resources.EpiMemberReplacementTitle,
                           c => ReplaceFieldAsync(context.Document, memberDeclaration, c),
                           nameof(Resources.EpiMemberReplacementTitle)),
                       diagnostic);
                }

                if (parameterDeclaration != null)
                {
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            Resources.EpiMemberReplacementTitle,
                            c => ReplaceConstructorAsync(context.Document, parameterDeclaration, c),
                            nameof(Resources.EpiMemberReplacementTitle)),
                        diagnostic);
                }
            }
           
        }

        private static async Task<Document> ReplaceFieldAsync(Document document, VariableDeclarationSyntax localDeclaration, CancellationToken cancellationToken)
        {
            // Replace Field Declaration with new Type.
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var baseType = localDeclaration;
            var genericName = (SimpleNameSyntax)baseType.Type;

            var newnode = genericName.WithIdentifier(SyntaxFactory.Identifier("FindOptions"));
            var newRoot = oldRoot!.ReplaceNode(genericName, newnode);

            // Return document with transformed type.
            return document.WithSyntaxRoot(newRoot);
        }

        private static async Task<Document> ReplaceConstructorAsync(Document document, ParameterSyntax localDeclaration, CancellationToken cancellationToken)
        {
            // Replace Contructor Parameter with new Type.
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var newParam = localDeclaration.WithType(SyntaxFactory.IdentifierName("FindOptions").WithTriviaFrom(localDeclaration.Type));
            var newNode = oldRoot!.ReplaceNode(localDeclaration, newParam);

            // Return document with transformed Parameter.
            return document.WithSyntaxRoot(newNode);
        }
    }
}

