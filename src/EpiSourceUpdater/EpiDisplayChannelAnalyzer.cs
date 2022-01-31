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
    public sealed class EpiDisplayChannelAnalyzer : EpiSubTypeAnalyzer
    {
        /// <summary>
        /// The diagnostic ID for diagnostics produced by this analyzer.
        /// </summary>
        public const string DiagnosticId = "EP0007";

        /// <summary>
        /// The diagnsotic category for diagnostics produced by this analyzer.
        /// </summary>
        private const string Category = "Upgrade";


        private static readonly string MethodName = "IsActive";
        private static readonly string HttpContextBase = "HttpContextBase";
        private static readonly string[] BaseTypes = new[] { "DisplayChannel" };

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EpiDisplayChannelTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EpiDisplayChannelFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EpiDisplayChannelDescription), Resources.ResourceManager, typeof(Resources));

        private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public EpiDisplayChannelAnalyzer() : base(BaseTypes)
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

            context.RegisterSyntaxNodeAction(AnalyzeIsActiveMethod, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeIsActiveMethod(SyntaxNodeAnalysisContext context)
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
                if (parameters.Count == 1 && HttpContextBase.Equals(parameters.Single().Type.ToString()) && IsSubType(methodDirective.Parent as ClassDeclarationSyntax))
                {
                    var diagnostic = Diagnostic.Create(Rule, methodDirective.GetLocation(), methodDirective.ToFullString());
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
