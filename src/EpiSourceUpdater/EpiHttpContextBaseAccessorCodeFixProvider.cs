// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System;
using System.Collections.Generic;
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
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = "EP00010 CodeFix Provider")]
    public class EpiHttpContextBaseAccessorCodeFixProvider : CodeFixProvider
    {

        // The Upgrade Assistant will only use analyzers that have an associated code fix provider registered including
        // the analyzer's ID in the code fix provider's FixableDiagnosticIds array.
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(EpiHttpContextBaseAccessorAnalyzer.DiagnosticId);
        private const string MicrosoftAspNetCoreHttpNamespace = "Microsoft.AspNetCore.Http";

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
            var classDeclarationSyntax = root.FindNode(diagnosticSpan) as ClassDeclarationSyntax;
            if (classDeclarationSyntax is null)
            {
                return;
            }

            context.RegisterCodeFix(
                     CodeAction.Create(
                         Resources.EpiHttpContextBaseTitle,
                         c => ReplaceParameterTypeAsync(context.Document, classDeclarationSyntax, c),
                         nameof(Resources.EpiHttpContextBaseTitle)),
                     diagnostic);
        }

        private static async Task<Document> ReplaceParameterTypeAsync(Document document, ClassDeclarationSyntax classDeclarationSyntax, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var updatedClassDeclaration = UpdateMemberField(classDeclarationSyntax);
            var updatedClassDeclarationByCostr = UpdateConstructor(updatedClassDeclaration);
            var newRoot = root!.ReplaceNode(classDeclarationSyntax, updatedClassDeclarationByCostr);

            // Return document with transformed tree.
            var updatedDocument = document.WithSyntaxRoot(newRoot);
            return await updatedDocument.AddUsingIfMissingAsync(cancellationToken, MicrosoftAspNetCoreHttpNamespace);
        }

        private static ClassDeclarationSyntax UpdateMemberField(ClassDeclarationSyntax classDecSyntax)
        {
            var updatedMembers = new List<MemberDeclarationSyntax>();
            foreach (var m in classDecSyntax.Members)
            {
                if (m is FieldDeclarationSyntax field && field.Declaration.Type.ToString().Equals(EpiHttpContextBaseAccessorAnalyzer.HttpContextBaseParameterType, StringComparison.OrdinalIgnoreCase))
                {
                    var svcAccessorHttpContextBaseMember = m as FieldDeclarationSyntax;
                    var variableDeclaration = svcAccessorHttpContextBaseMember.Declaration.WithType(SyntaxFactory.ParseTypeName("IHttpContextAccessor").WithTrailingTrivia(SyntaxFactory.Whitespace(" "))).WithVariables(svcAccessorHttpContextBaseMember.Declaration.Variables);
                    var iHttpContextBaseMember = SyntaxFactory.FieldDeclaration(svcAccessorHttpContextBaseMember.AttributeLists, (m as FieldDeclarationSyntax).Modifiers, variableDeclaration);
                    updatedMembers.Add(iHttpContextBaseMember);
                }
                else
                {
                    updatedMembers.Add(m);
                }
            }
            return classDecSyntax.WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>().AddRange(updatedMembers));
        }

        private static ClassDeclarationSyntax UpdateConstructor(ClassDeclarationSyntax classDeclarationSyntax)
        {
            ClassDeclarationSyntax updatedClassDeclaration = classDeclarationSyntax;
            foreach (var constructorSyntax in classDeclarationSyntax.Members.Where(m => m is ConstructorDeclarationSyntax))
            {
                var parameters = new List<ParameterSyntax>();

                foreach (var parameter in (constructorSyntax as ConstructorDeclarationSyntax).ParameterList.Parameters)
                {
                    if (parameter.Type != null && parameter.Type.ToString().Equals(EpiHttpContextBaseAccessorAnalyzer.HttpContextBaseParameterType, StringComparison.OrdinalIgnoreCase))
                    {
                        var updatedParameter = parameter.WithType(SyntaxFactory.ParseTypeName("IHttpContextAccessor")).WithTrailingTrivia();
                        parameters.Add(updatedParameter);
                    }
                    else
                    {
                        parameters.Add(parameter);
                    }
                }

                var updateConstructor =  (constructorSyntax as ConstructorDeclarationSyntax).WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters)));
                updatedClassDeclaration = updatedClassDeclaration.ReplaceNode(constructorSyntax, updateConstructor);
            }
            return updatedClassDeclaration;
        }
    }
}
