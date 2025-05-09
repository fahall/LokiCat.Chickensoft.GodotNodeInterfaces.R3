﻿// File: ObservableExtensionGenerator.cs

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
        context.AddSource("ObservableGeneratorCanary.g.cs", SourceText.From("// Observable generator ran\n", Encoding.UTF8));
        context.ReportDiagnostic(Diagnostic.Create(
                                     new DiagnosticDescriptor(
                                         id: "OBS000",
                                         title: "Observable Generator Running",
                                         messageFormat: "ObservableExtensionGenerator executed successfully.",
                                         category: "ObservableGenerator",
                                         defaultSeverity: DiagnosticSeverity.Info,
                                         isEnabledByDefault: true),
                                     Location.None));
        
        try {
            foreach (var iface in context.GetGodotNodeInterfaces()) {
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

        AddNamespace(nsSet, iface.ContainingNamespace);

        foreach (var ev in events)
        {
            if (ev.Type is not INamedTypeSymbol handler)
            {
                continue;
            }

            AddNamespace(nsSet, handler.ContainingNamespace);
            var invoke = handler.DelegateInvokeMethod;

            if (invoke == null)
            {
                continue;
            }

            ScanParameters(nsSet, invoke);
        }

        return nsSet.OrderBy(n => n);
        
    }
    
    private static void AddNamespace(ICollection<string> namespaceSet,  INamespaceSymbol? ns)
    {
        if (ns is { IsGlobalNamespace: false })
        {
            namespaceSet.Add(ns.ToDisplayString());
        }
    }
    private static void ScanParameters(ICollection<string> namespaceSet, IMethodSymbol invoke)
    {
        foreach (var p in invoke.Parameters)
        {
            AddNamespace(namespaceSet, p.Type.ContainingNamespace);

            if (p.Type is not INamedTypeSymbol { IsGenericType: true } gen)
            {
                continue;
            }

            foreach (var arg in gen.TypeArguments.OfType<INamedTypeSymbol>())
            {
                AddNamespace(namespaceSet, arg.ContainingNamespace);
            }
        }
    }
}