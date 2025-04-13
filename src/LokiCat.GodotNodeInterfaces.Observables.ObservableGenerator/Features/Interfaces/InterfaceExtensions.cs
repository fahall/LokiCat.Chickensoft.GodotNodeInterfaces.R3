using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Interfaces;

internal static class InterfaceExtensions
{
    public static string ShortName(this INamedTypeSymbol iface) =>
        iface.Name.StartsWith("I") && iface.Name.Length > 1 && char.IsUpper(iface.Name[1])
            ? iface.Name.Substring(1)
            : iface.Name;

    public static string EscapeIdentifier(this string name)
    {
        return SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? $"@{name}" : name;
    }

    public static string ToGeneratorTypeString(this ITypeSymbol iface)
    {
        return iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }
}