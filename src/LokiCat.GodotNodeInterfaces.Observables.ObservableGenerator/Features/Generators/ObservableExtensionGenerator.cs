// File: ObservableExtensionGenerator.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Interfaces;
using LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Generators;

[Generator]
public class ObservableExtensionGenerator : ISourceGenerator
{
    private readonly IEventWrapperGenerator _wrapperGenerator = new EventWrapperGenerator(); 
    public void Initialize(GeneratorInitializationContext context)
    {
        // no-op
    }

    public void Execute(GeneratorExecutionContext context)
    {
        try {
            foreach (var iface in GetInterfaces(context)) {
                ExtendInterface(context, iface);
            }
        }
        catch (Exception ex) {
            context.ReportDiagnostic(Diagnostic.Create(
                                         new DiagnosticDescriptor(
                                             "OBS999",
                                             "Observable generator failed",
                                             $"Exception: {ex}",
                                             "ObservableGenerator",
                                             DiagnosticSeverity.Error,
                                             isEnabledByDefault: true),
                                         Location.None
                                     ));
        }
    }

    private static List<INamedTypeSymbol> GetInterfaces(GeneratorExecutionContext context)
    {
        var godotInterfaces = context.Compilation.GlobalNamespace
                                     .GetNamespaceMembers()
                                     .FirstOrDefault(n => n.Name == "LokiCat.GodotNodeInterfaces.Observables")
                                     ?
                                     .GetNamespaceMembers()
                                     .FirstOrDefault(n => n.Name == "GodotNodeInterfaces")
                                     ?
                                     .GetNamespaceTypesRecursive()
                                     .Where(t => t.TypeKind == TypeKind.Interface)
                                     .ToList() ?? new();

        context.ReportDiagnostic(Diagnostic.Create(
                                     new DiagnosticDescriptor("OBS001", "Observe",
                                                              $"Found {godotInterfaces.Count} GodotNodeInterfaces interfaces",
                                                              "ObservableGenerator", DiagnosticSeverity.Info, true),
                                     Location.None
                                 ));

        return godotInterfaces;
    }

    private void ExtendInterface(GeneratorExecutionContext context, INamedTypeSymbol iface)
    {
        var events = iface.GetMembers()
                          .OfType<IEventSymbol>()
                          .Where(e => e.ContainingType.Equals(iface, SymbolEqualityComparer.Default))
                          .ToList();

        if (events.Count == 0)
        {
            return;
        }

        var wrappers = _wrapperGenerator.BuildEventWrappers(iface, events).ToArray();

        if (wrappers.Length == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine(BuildUsings(iface, events));
        sb.AppendLine(BuildExtensionClass(iface, wrappers));

        AddSource(context, $"{iface.ShortName()}Extensions.g.cs", sb.ToString());
    }


    private static void AddSource(GeneratorExecutionContext context, string filename, string body)
    {
        
        var tree = CSharpSyntaxTree.ParseText(body);
        var diagnostics = tree.GetDiagnostics().ToArray();
        if (diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)) {
            context.ReportDiagnostic(Diagnostic.Create(
                                         new DiagnosticDescriptor("OBS998", "Syntax Error", 
                                                                  $"Generated code invalid: {diagnostics[0]}", "ObservableGenerator", 
                                                                  DiagnosticSeverity.Error, true),
                                         Location.None
                                     ));
            return;
        }
        context.AddSource(filename, SourceText.From(body, Encoding.UTF8));
    }
    private static string BuildUsings(INamedTypeSymbol iface, List<IEventSymbol> events)
    {
        return string.Join("\n", GetRequiredNamespaces(iface, events).Select(ns => $"using {ns};"));
    }

    private static string BuildExtensionClass(INamedTypeSymbol iface, string[] eventWrappers)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"public static class {iface.ShortName()}ObservableExtensions");
        sb.AppendLine("{");

        foreach (var wrapper in eventWrappers)
        {
            sb.AppendLine(wrapper);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }



    private static IEnumerable<string> GetRequiredNamespaces(INamedTypeSymbol iface, IEnumerable<IEventSymbol> events)
    {
        var nsSet = new HashSet<string>
        {
            "System",
            "System.Threading",
            "R3",
            "Godot",
        };

        AddNamespace(iface.ContainingNamespace);

        foreach (var ev in events)
        {
            if (ev.Type is not INamedTypeSymbol handler)
            {
                continue;
            }

            AddNamespace(handler.ContainingNamespace);
            var invoke = handler.DelegateInvokeMethod;

            if (invoke == null)
            {
                continue;
            }

            foreach (var p in invoke.Parameters)
            {
                AddNamespace(p.Type.ContainingNamespace);

                if (p.Type is not INamedTypeSymbol gen || !gen.IsGenericType)
                {
                    continue;
                }

                foreach (var arg in gen.TypeArguments.OfType<INamedTypeSymbol>())
                {
                    AddNamespace(arg.ContainingNamespace);
                }
            }
        }

        return nsSet.OrderBy(n => n);

        void AddNamespace(INamespaceSymbol? ns)
        {
            if (ns != null && !ns.IsGlobalNamespace)
            {
                nsSet.Add(ns.ToDisplayString());
            }
        }
    }
}