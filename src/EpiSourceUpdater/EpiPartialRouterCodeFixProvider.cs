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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "EP0009 CodeFix Provider")]
    public class EpiPartialRouterCodeFixProvider : CodeFixProvider
    {
        // The Upgrade Assistant will only use analyzers that have an associated code fix provider registered including
        // the analyzer's ID in the code fix provider's FixableDiagnosticIds array.
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EpiPartialRouterAnalyzer.DiagnosticId);
        private const string RoutingNamespace = "EPiServer.Core.Routing";
        private const string RoutingPipelineNamespace = "EPiServer.Core.Routing.Pipeline";

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
            var methodDeclaration = root.FindNode(diagnosticSpan) as ClassDeclarationSyntax;
            if (methodDeclaration is null)
            {
                return;
            }

            context.RegisterCodeFix(
                     CodeAction.Create(
                         Resources.EpiPartialRouterTitle,
                         c => ReplaceParametersAsync(context.Document, methodDeclaration, c),
                         nameof(Resources.EpiPartialRouterTitle)),
                     diagnostic);
        }

        private static async Task<Document> ReplaceParametersAsync(Document document, ClassDeclarationSyntax localDeclaration, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            var routePartialMethod = FindMethod(localDeclaration, "RoutePartial");
            var parameters = routePartialMethod.ParameterList.Parameters;
            var obsoleteParameter = routePartialMethod.ParameterList.Parameters[1];
            parameters = parameters.Remove(obsoleteParameter);
            parameters = parameters.Add(obsoleteParameter.WithType(SyntaxFactory.ParseTypeName("UrlResolverContext")));
            var updatedMethod = routePartialMethod.WithParameterList(SyntaxFactory.ParameterList(parameters));
            
            var newRoot = root.ReplaceNode(routePartialMethod, updatedMethod);
            var updatedDocument = document.WithSyntaxRoot(newRoot);
            localDeclaration = newRoot.FindNode(localDeclaration.Span) as ClassDeclarationSyntax;

            var virtualPathMethod = FindMethod(localDeclaration, "GetPartialVirtualPath");
            parameters = virtualPathMethod.ParameterList.Parameters;
            parameters = parameters.RemoveAt(3);
            parameters = parameters.RemoveAt(2);
            parameters = parameters.RemoveAt(1);
            parameters = parameters.Add(SyntaxFactory.Parameter(SyntaxFactory.Identifier("urlGeneratorContext")).WithType(SyntaxFactory.ParseTypeName("UrlGeneratorContext")));
            updatedMethod = virtualPathMethod.WithParameterList(SyntaxFactory.ParameterList(parameters));
            
            newRoot = newRoot.ReplaceNode(virtualPathMethod, updatedMethod);
            updatedDocument = document.WithSyntaxRoot(newRoot);

            return await updatedDocument.AddUsingIfMissingAsync(cancellationToken, RoutingNamespace, RoutingPipelineNamespace);
        }

        private static MethodDeclarationSyntax FindMethod(ClassDeclarationSyntax classDeclaration, string methodName)
        {
            return classDeclaration.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.Text == methodName); 
        }
    }
}
