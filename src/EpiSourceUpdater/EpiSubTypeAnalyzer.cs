using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Epi.Source.Updater
{
    /// <summary>
    /// Base class for analyzers that acts on sub types to specified base type
    /// </summary>
    public abstract class EpiSubTypeAnalyzer : DiagnosticAnalyzer
    {
        private readonly string[] _baseTypes;
        private readonly bool _genericType;

        public EpiSubTypeAnalyzer(params string[] baseTypes)
        {
            _baseTypes = baseTypes;
        }

        protected bool IsSubType(ClassDeclarationSyntax classDirective)
        {
            if (classDirective is null || classDirective.BaseList is null)
            {
                return false;
            }

            foreach (var baseType in classDirective.BaseList.Types)
            {
                if (baseType.Type is SimpleNameSyntax nameSyntax)
                {
                    if (_baseTypes.Contains(nameSyntax.Identifier.Text, StringComparer.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        protected ClassDeclarationSyntax FindClassDeclaration(SyntaxNode syntaxNode)
        {
            var currentNode = syntaxNode;
            while (currentNode != null)
            {
                if (currentNode is ClassDeclarationSyntax)
                {
                    break;
                }
                currentNode = currentNode.Parent;
            }

            return currentNode as ClassDeclarationSyntax;
        }
    }
}
