using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Roslyn;

public static class ContextExtensions
{
    public static List<INamedTypeSymbol> GetGodotNodeInterfaces(this GeneratorExecutionContext context)
    {
        var godotInterfaces = context.Compilation.GlobalNamespace
                                     .GetNamespaceMembers()
                                     .FirstOrDefault(n => n.Name == "Chickensoft")
                                     ?
                                     .GetNamespaceMembers()
                                     .FirstOrDefault(n => n.Name == "GodotNodeInterfaces")
                                     ?
                                     .GetNamespaceTypesRecursive()
                                     .Where(t => t.TypeKind == TypeKind.Interface)
                                     .ToList() ?? [];

        context.ReportDiagnostic(Diagnostic.Create(
                                     new DiagnosticDescriptor("OBS001", "Observe",
                                                              $"Found {godotInterfaces.Count} GodotNodeInterfaces interfaces",
                                                              "ObservableGenerator", DiagnosticSeverity.Info, true),
                                     Location.None
                                 ));

        return godotInterfaces;
    }
}