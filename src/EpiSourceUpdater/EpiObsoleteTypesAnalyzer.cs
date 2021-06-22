// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Epi.Source.Updater
{
    /// <summary>
    /// Analyzer for identifying and removing obsolet types or methods.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class EpiObsoleteTypesAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID for diagnostics produced by this analyzer.
        /// <see href="https://github.com/episerver/upgrade-assistant-extensions/issues/4">Related issue</see>.
        /// </summary>
        public const string DiagnosticId = "EP0004";

        /// <summary>
        /// The diagnsotic category for diagnostics produced by this analyzer.
        /// </summary>
        private const string Category = "Upgrade";

        /// <summary>
        /// Key name for the diagnostic property containing the full name of the type
        /// the code fix provider should use to replace the syntax node identified
        /// in the diagnostic.
        /// </summary>
        public const string NewIdentifierKey = "TypeIdentifier";

        private static readonly string[] DisallowedTypes = new[] { "ParseToObject" };

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EpiDisallowedTypesTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EpiDisallowedTypesMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EpiDisallowedTypesDescription), Resources.ResourceManager, typeof(Resources));

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

            context.RegisterSyntaxNodeAction(AnalyzeStatements, SyntaxKind.MethodDeclaration);
        }

        private void AnalyzeStatements(SyntaxNodeAnalysisContext context)
        {
            var methodDirective = (MethodDeclarationSyntax)context.Node;

            var namespaceName = methodDirective.Identifier.Text?.ToString();

            if (namespaceName is null)
            {
                return;
            }

            if (DisallowedTypes.Any(name => namespaceName.Equals(name, StringComparison.Ordinal) || namespaceName.StartsWith($"{name}.", StringComparison.Ordinal)))
            {
                if (methodDirective.ReturnType.ToString().ToUpperInvariant() == "PROPERTYDATA")
                {
                    var diagnostic = Diagnostic.Create(Rule, methodDirective.GetLocation(), methodDirective.ToFullString());
                    context.ReportDiagnostic(diagnostic);
                }
                else
                {
                    var diagnostic = Diagnostic.Create(Rule, methodDirective.GetLocation(), namespaceName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }
}
