﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using CS = Microsoft.CodeAnalysis.CSharp;
using CSSyntax = Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Epi.Source.Updater
{
    /// <summary>
    /// Analyzer for identifying usage of types that should be replaced with other types.
    /// Diagnostics are created based on mapping configurations.
    /// <see href="https://github.com/episerver/upgrade-assistant-extensions/issues/1">Related issue</see>.
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

            context.RegisterCompilationStartAction(context =>
            {
                // Load analyzer configuration defining the types that should be mapped.
                var mappings = EpiClassMapLoader.LoadMappings(context.Options.AdditionalFiles);

                // If type maps are present, register syntax node actions to analyze for those types
                if (mappings.Any())
                {
                    // Register actions for handling both C# and VB identifiers
                    context.RegisterSyntaxNodeAction(context => AnalyzeCSharpIdentifier(context, mappings), CS.SyntaxKind.ClassDeclaration);
                }
            });
        }

        /// <summary>
        /// Creates a type upgrade diagnsotic.
        /// </summary>
        /// <param name="location">The location the diagnostic occurs at.</param>
        /// <param name="properties">Properties (including the name of the new identifier that code fix providers should substitute in) that should be included in the diagnostic.</param>
        /// <param name="messageArgs">Arguments (the simple name of the identifier to be replaced and the full name of the identifier to replace it) to be used in diagnotic messages.</param>
        /// <returns>A diagnostic to be shown to the user.</returns>
        private static Diagnostic CreateDiagnostic(Location location, ImmutableDictionary<string, string?> properties, params object[] messageArgs)
            => Diagnostic.Create(Rule, location, properties, messageArgs);

        private static void AnalyzeCSharpIdentifier(SyntaxNodeAnalysisContext context, IEnumerable<TypeMapping> mappings)
        {
            var identifier = (CSSyntax.ClassDeclarationSyntax)context.Node;
            AnalyzeIdentifier(context, mappings, identifier.Identifier.ValueText);
        }

        /// <summary>
        /// Analyzes an identifier syntax node to determine if it likely represents any of the types present
        /// in <see cref="IdentifierMappings"/>.
        /// </summary>
        /// <param name="context">The syntax node analysis context including the identifier node to analyze.</param>
        /// <param name="mappings">Type mappings to use when upgrading types.</param>
        /// <param name="simpleName">The simple name of the identifier being analyzed.</param>
        private static void AnalyzeIdentifier(SyntaxNodeAnalysisContext context, IEnumerable<TypeMapping> mappings, string simpleName)
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

            foreach (var attib in classDirective.AttributeLists)
            {
                if (attib.Attributes[0].Name.ToString() == "TemplateDescriptor")
                {
                    foreach (var arg in attib.Attributes[0].ArgumentList.Arguments)
                    {
                        if (arg.NameEquals.Name.Identifier.Text == "Default")
                        {
                            var diagnostic = Diagnostic.Create(Rule, classDirective.GetLocation(), attib.Attributes[0].Name.ToString());
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }
    }
}
