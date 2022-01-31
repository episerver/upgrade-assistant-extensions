// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Epi.Source.Updater
{
    /// <summary>
    /// Analyzer for identifying and removing obsolet types or methods.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EpiPartialControllerAnalyzer : EpiSubTypeAnalyzer
    {
        /// <summary>
        /// The diagnostic ID for diagnostics produced by this analyzer.
        /// </summary>
        public const string DiagnosticId = "EP0006";

        /// <summary>
        /// The diagnsotic category for diagnostics produced by this analyzer.
        /// </summary>
        private const string Category = "Upgrade";


        private static readonly string MethodName = "Index";
        internal static readonly string PartialView = "PartialView";
        private static readonly string[] BaseTypes = new[] { "PartialContentController", "BlockController", "PartialContentComponent", "BlockComponent" };

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EpiPartialControllerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EpiPartialControllerFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EpiPartialControllerDescription), Resources.ResourceManager, typeof(Resources));

        private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public EpiPartialControllerAnalyzer() : base(BaseTypes)
        { }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterSyntaxNodeAction(AnalyzeIndexMethod, SyntaxKind.MethodDeclaration);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                compilationContext.RegisterSyntaxNodeAction(AnalyzePartialViewUsage, SyntaxKind.InvocationExpression);
            });
        }

        private void AnalyzePartialViewUsage(SyntaxNodeAnalysisContext context)
        {
            var identifierExpression = (context.Node as InvocationExpressionSyntax)?.Expression as IdentifierNameSyntax;
            if (identifierExpression is null)
            {
                return;
            }

            // If the accessed member isn't named "PartialView" bail out
            if (!PartialView.Equals(identifierExpression.Identifier.Text))
            {
                return;
            }
            
            //Only change if inside a partial controller
            if (!IsSubType(FindClassDeclaration(context.Node)))
            {
                return;
            }

            var diagnostic = Diagnostic.Create(Rule, identifierExpression.GetLocation(), ImmutableDictionary.Create<string, string?>().Add(PartialView, true.ToString()));
            context.ReportDiagnostic(diagnostic);
        }

        private void AnalyzeIndexMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDirective = (MethodDeclarationSyntax)context.Node;

            var namespaceName = methodDirective.Identifier.Text?.ToString();

            if (namespaceName is null)
            {
                return;
            }

            if (namespaceName.Equals(MethodName, StringComparison.Ordinal))
            {
                if (methodDirective.ReturnType.ToString().ToUpperInvariant() == "ACTIONRESULT" && IsSubType(methodDirective.Parent as ClassDeclarationSyntax))
                {
                    var diagnostic = Diagnostic.Create(Rule, methodDirective.GetLocation(), methodDirective.ToFullString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

       
    }
}
