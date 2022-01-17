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
    public sealed class EpiHttpContextBaseAccessorAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID for diagnostics produced by this analyzer.
        /// </summary>
        public const string DiagnosticId = "EP0010";

        /// <summary>
        /// The diagnsotic category for diagnostics produced by this analyzer.
        /// </summary>
        private const string Category = "Upgrade";


        internal static readonly string HttpContextBaseParameterType = "ServiceAccessor<HttpContextBase>";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EpiHttpContextBaseTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EpiHttpContextBaseFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EpiHttpContextBaseDescription), Resources.ResourceManager, typeof(Resources));

        private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterSyntaxNodeAction(AnalyzeConstructor, SyntaxKind.ClassDeclaration);
        }

        private void AnalyzeConstructor(SyntaxNodeAnalysisContext context)
        {
            //var constructorDirective = (ConstructorDeclarationSyntax)context.Node;
            var classDirective = (ClassDeclarationSyntax)context.Node;

            foreach(var constructorSyntax in classDirective.Members.Where(m => m is ConstructorDeclarationSyntax))
            {
                var parameters = (constructorSyntax as ConstructorDeclarationSyntax).ParameterList.Parameters;
                if (parameters.Any(p => p.Type != null && p.Type.ToString().Equals(HttpContextBaseParameterType, StringComparison.OrdinalIgnoreCase)))
                {
                    var diagnostic = Diagnostic.Create(Rule, classDirective.GetLocation(), constructorSyntax.ToFullString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
