// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Options;
using CS = Microsoft.CodeAnalysis.CSharp;
using CSSyntax = Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Epi.Source.Updater
{
    /// <summary>
    /// Analyzer for identifying obsolete using references needed to be removed.
    /// <see href="https://github.com/episerver/upgrade-assistant-extensions/issues/6">Related issue</see>.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EpiObsoleteUsingAnalyzer : DiagnosticAnalyzer
    {
        private readonly ObsoletePropertyOptions? _options;

        public EpiObsoleteUsingAnalyzer(IOptions<ObsoletePropertyOptions> obsoletePropertyOptions)
        {
            _options = obsoletePropertyOptions?.Value;
        }

        /// <summary>
        /// The diagnostic ID for diagnostics produced by this analyzer.
        /// </summary>
        public const string DiagnosticId = "EP0005";

        /// <summary>
        /// Key name for the diagnostic property containing the full name of the type
        /// the code fix provider should use to replace the syntax node identified
        /// in the diagnostic.
        /// </summary>
        public const string NewIdentifierKey = "EpiObsoleteUsing";

        /// <summary>
        /// The diagnsotic category for diagnostics produced by this analyzer.
        /// </summary>
        private const string Category = "Upgrade";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EpiObsoleteUsingTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EpiObsoleteUsingMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EpiObsoleteUsingDescription), Resources.ResourceManager, typeof(Resources));

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

            context.RegisterSyntaxNodeAction(AnalyzeUsingDirectives, SyntaxKind.UsingDirective);
        }

        /// <summary>
        /// Analyze usings on a class to determine if they need to be removed.
        /// </summary>
        /// <param name="context">The syntax node analysis context including the identifier node to analyze.</param>
        private void AnalyzeUsingDirectives(SyntaxNodeAnalysisContext context)
        {
            var usingDirective = (CSSyntax.UsingDirectiveSyntax)context.Node;

            if (usingDirective is null)
            {
                return;
            }

            string usingFullName;
            var type = usingDirective.Name.GetType();

            if (type == typeof(Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax))
            {
                usingFullName = ((Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax)usingDirective.Name).ToFullString();
            }
            else if (type == typeof(Microsoft.CodeAnalysis.CSharp.Syntax.QualifiedNameSyntax))
            {
                usingFullName = ((Microsoft.CodeAnalysis.CSharp.Syntax.QualifiedNameSyntax)usingDirective.Name).ToFullString();
            }
            else
            {
                return;
            }

            if (_options is null || _options.Usings is null)
            {
                return;
            }

            if (_options.Usings.Where(u => u.Equals(usingFullName, StringComparison.OrdinalIgnoreCase)).Any())
            {
                var diagnosti = Diagnostic.Create(Rule, usingDirective.GetLocation(), usingFullName);
                context.ReportDiagnostic(diagnosti);
            }
            else
            {
                return;
            }
        }
    }
}
