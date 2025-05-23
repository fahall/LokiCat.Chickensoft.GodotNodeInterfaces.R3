﻿// File: GodotBuiltInObservableGenerator.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Generators;

[Generator]
public sealed class GodotBuiltInObservableGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context) { }

    public void Execute(GeneratorExecutionContext context)
    {
        try
        {
            var interfaces = context.GetGodotNodeInterfaces();

            foreach (var iface in interfaces)
            {
                var godotType = iface.Name.StartsWith("I") ? iface.Name[1..] : null;

                if (godotType is null)
                {
                    continue;
                }

                var events = iface.GetMembers()
                                  .OfType<IEventSymbol>()
                                  .Where(e => e.ContainingType.Equals(iface, SymbolEqualityComparer.Default))
                                  .ToList();

                if (events.Count == 0)
                {
                    continue;
                }

                GeneratePartialForType(context, godotType, events);
            }
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                                         new DiagnosticDescriptor("OBSBUILTIN001", "Generation failed",
                                                                  $"Exception: {ex.Message}", "ObservableGenerator",
                                                                  DiagnosticSeverity.Error, true),
                                         Location.None));
        }
    }

    private static void GeneratePartialForType(
        GeneratorExecutionContext context,
        string className,
        List<IEventSymbol> events
    )
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine("using System;");
        sb.AppendLine("using R3;");
        sb.AppendLine("using Godot;");

        sb.AppendLine($"public partial class {className} {{");

        foreach (var ev in events)
        {
            var signal = ev.Name;
            var observableName = $"On{signal}";
            var fieldName = $"_on{signal}";
            var connectedFlag = $"_{char.ToLowerInvariant(signal[0])}{signal[1..]}Connected";

            var handler = ev.Type as INamedTypeSymbol;
            var invoke = handler?.DelegateInvokeMethod;
            var parameters = invoke?.Parameters ?? default;
            var paramCount = parameters.Length;

            var emitCall = paramCount switch
            {
                0 => $"EmitSignal(\"{signal.ToLowerInvariant()}\")",
                1 => $"EmitSignal(\"{signal.ToLowerInvariant()}\", value!)",
                _ =>
                    $"EmitSignal(\"{signal.ToLowerInvariant()}\", {string.Join(", ", Enumerable.Range(1, paramCount).Select(i => $"value.Item{i}"))})"
            };

            if (paramCount > 5)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                                             new DiagnosticDescriptor("OBSBUILTIN002", "Too many signal parameters",
                                                                      "Signal '{0}' on type '{1}' has more than 5 parameters and will be skipped.",
                                                                      "ObservableGenerator", DiagnosticSeverity.Warning,
                                                                      true),
                                             Location.None,
                                             signal, className));

                continue;
            }

            var observableType = paramCount switch
            {
                0 => "Unit",
                1 => parameters[0].Type.ToDisplayString(),
                _ => $"({string.Join(", ", parameters.Select(p => p.Type.ToDisplayString()))})"
            };
            sb.AppendLine($"  private bool {connectedFlag};");

            sb.AppendLine($"  public Observable<{observableType}> {observableName} =>");
            sb.AppendLine($"    {fieldName} ??= Connect{signal}();");

            sb.AppendLine($"  private Subject<{observableType}> Connect{signal}() {{");
            sb.AppendLine($"    if (!{connectedFlag}) {{");
            sb.AppendLine($"      {connectedFlag} = true;");
            sb.AppendLine($"      var subject = new Subject<{observableType}>();");
            sb.AppendLine($"      subject.Subscribe(value => {emitCall}).AddTo(this);");
            sb.AppendLine($"      return subject;");
            sb.AppendLine($"    }}");
            sb.AppendLine($"    return {fieldName}!;");
            sb.AppendLine($"  }}");
        }

        sb.AppendLine("}");

        var filename = $"{className}.BuiltinObservables.g.cs";
        context.AddSource(filename, SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}