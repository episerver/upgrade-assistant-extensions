// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Immutable;
using CS = Microsoft.CodeAnalysis.CSharp;
using CSSyntax = Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Epi.Source.Updater
{
    /// <summary>
    /// Analyzer for identifying usage of types that should be replaced with other types.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EpiAttributeRemoverAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID for diagnostics produced by this analyzer.
        /// </summary>
        public const string DiagnosticId = "EP0001";

        /// <summary>
        /// Key name for the diagnostic property containing the full name of the type
        /// the code fix provider should use to replace the syntax node identified
        /// in the diagnostic.
        /// </summary>
        public const string NewIdentifierKey = "AttributeIdentifier";

        /// <summary>
        /// The diagnsotic category for diagnostics produced by this analyzer.
        /// </summary>
        private const string Category = "Upgrade";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EpiAttributeRemoverTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EpiAttributeRemoverMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EpiAttributeRemoverDescription), Resources.ResourceManager, typeof(Resources));

        private static readonly DiagnosticDescriptor Rule = new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <summary>
        /// Initializes the analyzer by registering analysis callback methods.
        /// </summary>
        /// <param name="context">The context to use for initialization.</param>
        public override void Initialize(AnalysisContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterSyntaxNodeAction(context => AnalyzeIdentifier(context), CS.SyntaxKind.ClassDeclaration);
        }

        /// <summary>
        /// Analyzes an identifier syntax node to determine if it likely represents any of the types present
        /// in <see cref="IdentifierMappings"/>.
        /// </summary>
        /// <param name="context">The syntax node analysis context including the identifier node to analyze.</param>
        private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context)
        {
            var classDirective = (CSSyntax.ClassDeclarationSyntax)context.Node;
            if (classDirective is null)
            {
                return;
            }

            if (classDirective.AttributeLists.Count == 0)
            {
                return;
            }

            foreach (var attrib in classDirective.AttributeLists)
            {
                if (attrib.Attributes[0].Name.ToString() == "TemplateDescriptor")
                {
                    foreach (var arg in attrib.Attributes[0].ArgumentList.Arguments)
                    {
                        if (arg.NameEquals.Name.Identifier.Text == "Default")
                        {
                            var diagnostic = Diagnostic.Create(Rule, attrib.Attributes[0].GetLocation(), attrib.Attributes[0].Name.ToString());
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }
    }
}
