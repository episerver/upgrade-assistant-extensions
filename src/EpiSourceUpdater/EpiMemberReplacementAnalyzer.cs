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
            Compilation compilation = context.Compilation;

            INamedTypeSymbol myType = compilation.GetTypeByMetadataName("EPiServer.Find.UI.FindUIConfiguration");
            if (myType != null)
            {

                var members = myType.GetMembers();
                foreach(var memb in myType.GetMembers())
                {

                    var nam = memb.Name;

                }
            }
            //var found = myType.AllInterfaces.Any(i => myType.Equals(i));

            //var classDirective = (CSSyntax.ClassDeclarationSyntax)context.Node;
            //if (classDirective is null)
            //{
            //    return;
            //}

         
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
                            var diagnostic = Diagnostic.Create(Rule, constructor.GetLocation(), constructor);
                            context.ReportDiagnostic(diagnostic);
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
                            var diagnostic = Diagnostic.Create(Rule, fielddeclaration.GetLocation(), fielddeclaration);
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }



            //if (memberDeclaration is null)
            //{
            //    return;
            //}

            if (context.Node.Kind() == SyntaxKind.FieldDeclaration || context.Node.Kind() == SyntaxKind.ConstructorDeclaration)
            {

                var namen = context.Node;
            }

            return;



            bool Implements(INamedTypeSymbol symbol, ITypeSymbol type)
            {
                return symbol.AllInterfaces.Any(i => type.Equals(i));
            }

            SemanticModel semanticModel = context.SemanticModel;
            var typeSyntax = (CSSyntax.ClassDeclarationSyntax)context.Node; // ModuleStatementSyntxt; //ClassStatementSyntax, ModuleStatementSyntxt or NamespaceStatementSyntax
            string name = null;
            int startLine;
            int endLine;

            var info = semanticModel.GetSymbolInfo(typeSyntax);
            if (info.Symbol is INamespaceOrTypeSymbol typeSymbol)
            {
                name = typeSymbol.Name; // retrieve Name
                startLine = semanticModel.SyntaxTree.GetLineSpan(typeSymbol.DeclaringSyntaxReferences[0].Span).StartLinePosition.Line; //retrieve start line
                endLine = semanticModel.SyntaxTree.GetLineSpan(typeSymbol.DeclaringSyntaxReferences[0].Span).EndLinePosition.Line; //retrieve end line
                foreach (var item in typeSymbol.GetMembers())
                {
                    var nnn = item.Name;
                    // do the same logic for retrieving name and lines for all others members without calling GetMembers()
                }
            }
            else if (semanticModel.GetDeclaredSymbol(typeSyntax) is INamespaceOrTypeSymbol typeSymbol2)
            {
                name = typeSymbol2.Name; // retrieve Name
                startLine = semanticModel.SyntaxTree.GetLineSpan(typeSymbol2.DeclaringSyntaxReferences[0].Span).StartLinePosition.Line; //retrieve start line
                endLine = semanticModel.SyntaxTree.GetLineSpan(typeSymbol2.DeclaringSyntaxReferences[0].Span).EndLinePosition.Line; //retrieve end line

                foreach (var item in typeSymbol2.GetMembers())
                {
                    // do the same logic for retrieving name and lines for all others members without calling GetMembers()
                }
            }

  //          var filedDeclaration = (CSSyntax.FieldDeclarationSyntax)context.Node;


            //var constructorDeclaration = new CSSyntax.ConstructorDeclarationSyntax(  x(();
            //if (context.Node.IsKind(SyntaxKind.ConstructorDeclaration))
            //{
            //    var constructorDeclaration = (CSSyntax.ConstructorDeclarationSyntax)context.Node;

            //    if (constructorDeclaration is null)
            //    {
            //        return;
            //    }
            //}
            //var diagnosti = Diagnostic.Create(Rule, memberDeclaration.GetLocation(), memberDeclaration);
            //context.ReportDiagnostic(diagnosti);


            //diagnosti = Diagnostic.Create(Rule, constructorDeclaration.GetLocation(), constructorDeclaration);
            //context.ReportDiagnostic(diagnosti);
        }
    }
}
