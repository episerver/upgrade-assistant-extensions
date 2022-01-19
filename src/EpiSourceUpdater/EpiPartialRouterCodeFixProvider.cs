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
        private const string DependencyInjectionNamespace = "Microsoft.Extensions.DependencyInjection";

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
            if (declaration is ClassDeclarationSyntax classDeclaration)
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        Resources.EpiPartialRouterTitle,
                        c => ReplaceParametersAsync(context.Document, classDeclaration, c),
                        nameof(Resources.EpiPartialRouterTitle)),
                    diagnostic);
            }
            else if (declaration is InvocationExpressionSyntax invocationExpression)
            {
                context.RegisterCodeFix(
                   CodeAction.Create(
                       Resources.EpiPartialRouterTitle,
                       c => HandlePartialRouteRegistration(context.Document, invocationExpression, c),
                       nameof(Resources.EpiPartialRouterTitle)),
                   diagnostic);
            }
           
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

        private async Task<Document> HandlePartialRouteRegistration(Document document, InvocationExpressionSyntax invocationExpression, CancellationToken cancellationToken)
        {
            //find the registration statement
            var currentCodeBlock = GetSurroundingBlock(invocationExpression);
            var getPartialRouterArgument = invocationExpression.ArgumentList.Arguments[0].Expression;
            
            if (currentCodeBlock is not null)
            {
                var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                var index = -1;
                var statements = currentCodeBlock.Statements;
                //find where registration statement is
                for (var i = 0; i < statements.Count; i++)
                {
                    var statement = statements[i];
                    if (statement is ExpressionStatementSyntax expressionStatement && expressionStatement.Expression == invocationExpression)
                    {
                        index = i;
                        break;
                    }
                }

                if (index > -1)
                {
                    var statement = statements[index];
                    statements = statements.RemoveAt(index);
                    var methodDeclaration = GetSurroundingMethod(invocationExpression);
                    if (methodDeclaration is not null && methodDeclaration.Identifier.Text == "ConfigureContainer")
                    {
                        var parameterName = methodDeclaration.ParameterList.Parameters[0].Identifier.Text;
                        if (getPartialRouterArgument is ObjectCreationExpressionSyntax objectCreationExpression)
                        {
                            statements = statements.Insert(index, SyntaxFactory.ParseStatement($"{parameterName}.Services.AddSingleton<IPartialRouter, {objectCreationExpression.Type}>();")
                                 .WithLeadingTrivia(statement.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed));
                        }
                        else
                        {
                            statements = statements.Insert(index, SyntaxFactory.ParseStatement($"{parameterName}.Services.AddSingleton<IPartialRouter>({getPartialRouterArgument.ToString()});")
                                 .WithLeadingTrivia(statement.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.CarriageReturnLineFeed));
                        }
                    }
                    else
                    {
                        //The registration is done somewhere where we do not have access to IServiceCollection, add a comment on how registration should be done       
                        var parameterType = getPartialRouterArgument is ObjectCreationExpressionSyntax objectCreationExpression ? objectCreationExpression.Type.ToString() : "CustomPartialRouter";
                        var comment = SyntaxFactory.Comment($"//Partial router should be registered in ioc container like 'services.AddSingleton<IPartialRouter, {parameterType}>()'");
                        statements = statements.Insert(index, statement.WithLeadingTrivia(statement.GetLeadingTrivia().Add(comment).Add(SyntaxFactory.CarriageReturnLineFeed).AddRange(statement.GetLeadingTrivia())));
                    }

                    var newCodeBlock = currentCodeBlock.WithStatements(statements);
                    var newroot = root.ReplaceNode(currentCodeBlock, newCodeBlock);
                    return await document.WithSyntaxRoot(newroot).AddUsingIfMissingAsync(cancellationToken, DependencyInjectionNamespace);
                }
            }

            return document;
        }

        private BlockSyntax GetSurroundingBlock(InvocationExpressionSyntax invocationExpression)
        {
            var parent = invocationExpression.Parent;
            while (parent is not null && parent is not BlockSyntax)
            {
                parent = parent.Parent; 
            }
            return parent as BlockSyntax;
        }

        private MethodDeclarationSyntax GetSurroundingMethod(InvocationExpressionSyntax invocationExpression)
        {
            var parent = invocationExpression.Parent;
            while (parent is not null && parent is not MethodDeclarationSyntax)
            {
                parent = parent.Parent;
            }
            return parent as MethodDeclarationSyntax;
        }
    }
}
