// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using CS = Microsoft.CodeAnalysis.CSharp;
using CSSyntax = Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Epi.Source.Updater
{
    /// <summary>
    /// Analyzer for identifying usage of base class types that should be replaced with other types.
    /// Diagnostics are created based on mapping configurations.
    /// <see href="https://github.com/episerver/upgrade-assistant-extensions/issues/2">Related issue</see>.
    /// <see href="https://github.com/episerver/upgrade-assistant-extensions/issues/3">Related issue</see>.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class EpiClassReplacementsAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID for diagnostics produced by this analyzer.
        /// </summary>
        public const string DiagnosticId = "EP0002";

        /// <summary>
        /// Key name for the diagnostic property containing the full name of the type
        /// the code fix provider should use to replace the syntax node identified
        /// in the diagnostic.
        /// </summary>
        public const string NewIdentifierKey = "EpiClassIdentifier";

        /// <summary>
        /// The diagnsotic category for diagnostics produced by this analyzer.
        /// </summary>
        private const string Category = "Upgrade";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EpiClassUpgradeTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EpiClassUpgradeMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EpiClassUpgradeDescription), Resources.ResourceManager, typeof(Resources));

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

            context.RegisterCompilationStartAction(context =>
            {
                // Load analyzer configuration defining the types that should be mapped.
                var mappings = EpiClassMapLoader.LoadMappings(context.Options.AdditionalFiles);

                // If type maps are present, register syntax node actions to analyze for those types
                if (mappings.Any())
                {
                    // Register actions for handling both C# and VB identifiers
                    context.RegisterSyntaxNodeAction(context => AnalyzeIdentifier(context, mappings), CS.SyntaxKind.ClassDeclaration);
                }
            });
        }

        private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context, IEnumerable<TypeMapping> mappings)
        {
            var classDirective = (CSSyntax.ClassDeclarationSyntax)context.Node;
            if (classDirective is null)
            {
                return;
            }

            if (classDirective.BaseList is null)
            {
                return;
            }

            foreach (var baseType in classDirective.BaseList.Types)
            {
                if (baseType is null)
                {
                    return;
                }

                var nameOfFirstBaseType = baseType.Type.ToString();

                foreach (var map in mappings)
                {
                    if (nameOfFirstBaseType.Contains(map.OldName))
                    {
                        // Store the new identifier name that this identifier should be replaced with for use
                        // by the code fix provider.
                        var properties = ImmutableDictionary.Create<string, string?>().Add(NewIdentifierKey, map.NewName);

                        var diagnosti = Diagnostic.Create(Rule, baseType.GetLocation(), properties, nameOfFirstBaseType);
                        context.ReportDiagnostic(diagnosti);
                    }
                }
            }
        }
    }
}
