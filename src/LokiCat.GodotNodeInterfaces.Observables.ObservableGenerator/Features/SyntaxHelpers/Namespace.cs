using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.SyntaxHelpers;

public class Namespace
{
    public static string GetNamespace(SyntaxNode node) =>
        node.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString()
        ?? "Global";
}