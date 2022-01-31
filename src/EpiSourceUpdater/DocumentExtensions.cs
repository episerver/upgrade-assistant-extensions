using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Epi.Source.Updater
{
    internal static class DocumentExtensions
    {
        public static async Task<Document> AddUsingIfMissingAsync(this Document document, CancellationToken cancellationToken, params string[] namespaces)
        {
            var compilationRoot = (await document.GetSyntaxTreeAsync()).GetCompilationUnitRoot();
            var missingUsings = namespaces.Where(n => !compilationRoot.Usings.Any(u => u.Name.ToString() == n))
                .Select(n => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(n).WithLeadingTrivia(SyntaxFactory.Whitespace(" ")))
                            .WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed));
            if (missingUsings.Any())
            {
                var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
                var documentRoot = (CompilationUnitSyntax)editor.OriginalRoot;
                documentRoot = documentRoot.AddUsings(missingUsings.ToArray());
                editor.ReplaceNode(editor.OriginalRoot, documentRoot);
                document = editor.GetChangedDocument();
            }

            return document;
        }
    }
}
