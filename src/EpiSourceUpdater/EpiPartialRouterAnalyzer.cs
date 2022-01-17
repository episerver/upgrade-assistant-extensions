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
    public sealed class EpiPartialRouterAnalyzer : EpiSubTypeAnalyzer
    {
        /// <summary>
        /// The diagnostic ID for diagnostics produced by this analyzer.
        /// </summary>
        public const string DiagnosticId = "EP0009";

        /// <summary>
        /// The diagnsotic category for diagnostics produced by this analyzer.
        /// </summary>
        private const string Category = "Upgrade";


        private static readonly string MethodName = "RoutePartial";
        private static readonly string RegistrationMethod = "RegisterPartialRouter";
        private static readonly string[] BaseTypes = new[] { "IPartialRouter" };

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EpiPartialRouterTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EpiPartialRouterMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EpiPartialRouterDescription), Resources.ResourceManager, typeof(Resources));

        private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public EpiPartialRouterAnalyzer() : base(BaseTypes)
        {
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterSyntaxNodeAction(AnalyzeIfInterfaceMethod, SyntaxKind.MethodDeclaration);
            context.RegisterCompilationStartAction(compilationContext =>
            {
                compilationContext.RegisterSyntaxNodeAction(AnalyzePartialRouteRegistration, SyntaxKind.InvocationExpression);
            });
        }

        private void AnalyzeIfInterfaceMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDirective = (MethodDeclarationSyntax)context.Node;

            var namespaceName = methodDirective.Identifier.Text?.ToString();

            if (namespaceName is null)
            {
                return;
            }

            if (namespaceName.Equals(MethodName, StringComparison.Ordinal))
            {
                var parameters = methodDirective.ParameterList.Parameters;
                if (IsSubType(methodDirective.Parent as ClassDeclarationSyntax))
                {
                    var diagnostic = Diagnostic.Create(Rule, methodDirective.Parent.GetLocation(), methodDirective.ToFullString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private void AnalyzePartialRouteRegistration(SyntaxNodeAnalysisContext context)
        {
            var memberExpression = (context.Node as InvocationExpressionSyntax)?.Expression as MemberAccessExpressionSyntax;
            if (memberExpression is null)
            {
                return;
            }

            // If the accessed member isn't named "RegisterPartialRouter" bail out
            if (!RegistrationMethod.Equals(memberExpression.Name.Identifier.Text))
            {
                return;
            }

            //If we have already acted by adding a comment then we should not report
            if (!memberExpression.GetLeadingTrivia().Any(t => t.Kind() == SyntaxKind.SingleLineCommentTrivia && t.ToString().StartsWith("//Partial router should be registered in ioc container")))
            {
                var diagnostic = Diagnostic.Create(Rule, context.Node.GetLocation());
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
