using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Roslyn;

internal static class RoslynExtensions
{
    public static IEnumerable<INamedTypeSymbol> GetNamespaceTypesRecursive(this INamespaceSymbol namespaceSymbol)
    {
        foreach (var member in namespaceSymbol.GetMembers())
        {
            switch (member)
            {
                case INamespaceSymbol childNs:
                    foreach (var nested in childNs.GetNamespaceTypesRecursive())
                    {
                        yield return nested;
                    }

                    break;
                case INamedTypeSymbol typeSymbol:
                    yield return typeSymbol;
                    break;
            }
        }
    }
}