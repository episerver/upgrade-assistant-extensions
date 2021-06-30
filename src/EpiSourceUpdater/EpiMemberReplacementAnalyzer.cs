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
    public class EpiMemberReplacementAnalyzer : DiagnosticAnalyzer
    {
        private readonly ObsoletePropertyOptions? _options;

        public EpiMemberReplacementAnalyzer(IOptions<ObsoletePropertyOptions> obsoletePropertyOptions)
        {
            _options = obsoletePropertyOptions?.Value;
        }

        /// <summary>
        /// The diagnostic ID for diagnostics produced by this analyzer.
        /// </summary>
        public const string DiagnosticId = "EP0003";

        /// <summary>
        /// Key name for the diagnostic property containing the full name of the type
        /// the code fix provider should use to replace the syntax node identified
        /// in the diagnostic.
        /// </summary>
        public const string NewIdentifierKey = "EpiMemberReplacement";

        /// <summary>
        /// The diagnsotic category for diagnostics produced by this analyzer.
        /// </summary>
        private const string Category = "Upgrade";

        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.EpiMemberReplacementTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.EpiMemberReplacementMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.EpiMemberReplacementDescription), Resources.ResourceManager, typeof(Resources));

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
            SyntaxKind[] parms = {SyntaxKind.ClassDeclaration,SyntaxKind.ConstructorDeclaration,SyntaxKind.FieldDeclaration, SyntaxKind.CastExpression};
            context.RegisterSyntaxNodeAction(AnalyzeUsingDirectives, parms);
        }

        /// <summary>
        /// Analyze usings on a class to determine if they need to be removed.
        /// </summary>
        /// <param name="context">The syntax node analysis context including the identifier node to analyze.</param>
        private void AnalyzeUsingDirectives(SyntaxNodeAnalysisContext context)
        {
            if (context.Node.Kind() == SyntaxKind.ClassDeclaration)
            {
                var constructSymbol = context.SemanticModel.GetDeclaredSymbol(context.Node);
  
                if (context.Node.ChildNodes().Count() > 0)
                {
                    var constructor = context.Node.ChildNodes().FirstOrDefault(n => n.Kind() == SyntaxKind.ConstructorDeclaration);
                    if (constructor != null)
                    {

                    var parameters = ((CSSyntax.ConstructorDeclarationSyntax)constructor).ParameterList
                    .ChildNodes()
                    .Cast<CSSyntax.ParameterSyntax>()
                    .Where(node => node.Type.ToString() == "IFindUIConfiguration"
                    );

                        if (parameters.Count() > 0)
                        {
                            foreach (var param in parameters)
                            {
                                var diagnostic = Diagnostic.Create(Rule, constructor.GetLocation(), parameters.First().Type.ToString());
                                context.ReportDiagnostic(diagnostic);
                            }
                        }
                  }
                }
            }

            if (context.Node.Kind() == SyntaxKind.FieldDeclaration)
            {
                 if (context.Node.ChildNodes().Count() > 0)
                {
                    var fielddeclaration = context.Node.ChildNodes().FirstOrDefault(n => n.Kind() == SyntaxKind.VariableDeclaration);
                    if (fielddeclaration != null)
                    {
                        var field = ((CSSyntax.VariableDeclarationSyntax)fielddeclaration).Type.ToString();
                        if (!string.IsNullOrEmpty(field) && field == "IFindUIConfiguration")
                        {
                            var diagnostic = Diagnostic.Create(Rule, fielddeclaration.GetLocation(), field);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }
    }
}
