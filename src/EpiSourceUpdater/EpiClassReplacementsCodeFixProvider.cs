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
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace Epi.Source.Updater
{
    /// <summary>
    /// As with the analzyers, code fix providers that are registered into Upgrade Assistant's
    /// dependency injection container (by IExtensionServiceProvider) will be used during
    /// the source update step.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "EP0001 CodeFix Provider")]
    public class EpiClassReplacementsCodeFixProvider : CodeFixProvider
    {
        // The Upgrade Assistant will only use analyzers that have an associated code fix provider registered including
        // the analyzer's ID in the code fix provider's FixableDiagnosticIds array.
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EpiClassReplacementsAnalyzer.DiagnosticId);

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

            var declaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();

            if (declaration is null)
            {
                return;
            }

            if (diagnostic.Properties.TryGetValue(EpiClassReplacementsAnalyzer.NewIdentifierKey, out var property) && property is not null)
            {
                // Register a code action that will invoke the fix.
                context.RegisterCodeFix(
                CodeAction.Create(
                    Resources.EpiClassUpgradeTitle,
                    c => ReplaceClassesAsync(context.Document, declaration, property, c),
                    nameof(Resources.EpiClassUpgradeTitle)),
                diagnostic);
            }
        }

        private static async Task<Document> ReplaceClassesAsync(Document document, ClassDeclarationSyntax localDeclaration, string newIdentifier, CancellationToken cancellationToken)
        {
            // Remove the leading trivia from the local declaration.
            var firstToken = localDeclaration.GetFirstToken();
            var leadingTrivia = firstToken.LeadingTrivia;
            var trimmedLocal = localDeclaration.ReplaceToken(
                firstToken, firstToken.WithLeadingTrivia(SyntaxTriviaList.Empty));

            var generator = SyntaxGenerator.GetGenerator(document);
            BaseTypeDeclarationSyntax b;
       //     var updatedbasedecl = b.WithBaseList(generator.ClassDeclaration);
       //     //generator.AddBaseType()
       ////     var updated = localDeclaration.WithBaseList();

       //     var newclass = SyntaxFactory.ClassDeclaration((newIdentifier + "<EntryContentBase>");
       //     newclass.AddMembers(localDeclaration.Members);
       //     newclass.AddMembers(localDeclaration.Members.ToArry());
            
            ClassDeclarationSyntax newNode = localDeclaration;

            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

             
            // Remove PropertyData ParseToObject method.
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            //var updatedRoot = oldRoot.ReplaceNode(localDeclaration, newclass);


            // Return document with transformed tree.
            return document.WithSyntaxRoot(oldRoot);
        }
    }
}
