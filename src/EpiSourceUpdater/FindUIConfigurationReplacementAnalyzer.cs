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
    /// Analyzer for identifying obsolete using references needed to be removed.
    /// <see href="https://github.com/episerver/upgrade-assistant-extensions/issues/6">Related issue</see>.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FindUIConfigurationReplacementAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// The diagnostic ID for diagnostics produced by this analyzer.
        /// </summary>
        public const string DiagnosticId = "EP0003";

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
            context.RegisterSyntaxNodeAction(AnalyzeInterfaceInstances, parms);
        }

        private void AnalyzeInterfaceInstances(SyntaxNodeAnalysisContext context)
        {
            if (context.Node.Kind() == SyntaxKind.ClassDeclaration)
            {
                var classSyntax = context.Node as ClassDeclarationSyntax;
                var model = context.SemanticModel.GetDeclaredSymbol(classSyntax);
                foreach(var constructor  in model.Constructors.Where(x => x.Parameters.Any(y => y.Type.ToString().Contains("IFindUIConfiguration"))))
                {
                    var parameter = constructor.Parameters.FirstOrDefault(y => y.Type.ToString().Contains("IFindUIConfiguration"));
                    if (parameter == null)
                    {
                        continue;
                    }
                    
                    context.ReportDiagnostic(Diagnostic.Create(Rule, parameter.Locations.FirstOrDefault(), parameter.Type.ToString()));

                }
            }

            if (context.Node.Kind() == SyntaxKind.FieldDeclaration)
            {
                var fieldSyntax = context.Node as FieldDeclarationSyntax;
                if (fieldSyntax.Declaration.Type.ToString().Contains("IFindUIConfiguration"))
                {
                    context.ReportDiagnostic(Diagnostic.Create(Rule, fieldSyntax.Declaration.GetLocation(), fieldSyntax.Declaration.Type.ToString()));
                }
            }
        }
    }
}
