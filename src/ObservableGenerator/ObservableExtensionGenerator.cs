// File: ObservableExtensionGenerator.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ObservableGenerator
{
    internal static class BuildGuard { }
    
    [Generator]
    public class ObservableExtensionGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // no-op for now
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var godotInterfaces = context.Compilation
                .GetSymbolsWithName(name => name.StartsWith("I") && name.Contains("Godot"), SymbolFilter.Type)
                .OfType<INamedTypeSymbol>()
                .Where(t => t.TypeKind == TypeKind.Interface && t.ContainingNamespace.ToDisplayString().Contains("Chickensoft.GodotNodeInterfaces"))
                .ToList();

            foreach (var iface in godotInterfaces)
            {
                var events = iface.GetMembers().OfType<IEventSymbol>()
                    .Where(ev => ev.ContainingType.Equals(iface, SymbolEqualityComparer.Default))
                    .ToList();

                if (events.Count == 0)
                    continue;

                var sb = new StringBuilder();
                var shortName = iface.Name.TrimStart('I');

                sb.AppendLine("using System;");
                sb.AppendLine("using System.Reactive;");
                sb.AppendLine("using System.Reactive.Linq;");
                sb.AppendLine("using System.Threading;");
                sb.AppendLine($"public static class {shortName}Extensions");
                sb.AppendLine("{");

                foreach (var ev in events)
                {
                    if (ev.Type is not INamedTypeSymbol { TypeKind: TypeKind.Delegate } handler)
                    {
                        continue;
                    }

                    {
                        var invoke = handler.DelegateInvokeMethod;
                        if(invoke is null)
                        {
                            continue;
                        }

                        var parameters = invoke.Parameters;

                        string returnType;
                        string body;

                        if (parameters is { Length: 1 })
                        {
                            returnType = $"Observable<{parameters[0].Type.ToDisplayString()}>";
                            body =
                                $"Observable.FromEvent<{handler.ToDisplayString()}, {parameters[0].Type.ToDisplayString()}>(\n" +
                                $"        h => new {handler.Name}(h),\n" +
                                $"        h => self.{ev.Name} += h,\n" +
                                $"        h => self.{ev.Name} -= h,\n" +
                                $"        cancellationToken\n    )";
                        }
                        else if (parameters is { Length: > 1 })
                        {
                            var tuple = string.Join(", ", parameters.Select(p => p.Type.ToDisplayString()));
                            var args = string.Join(", ", parameters.Select(p => p.Name));
                            body = $"Observable.FromEvent<{handler.ToDisplayString()}, ({tuple})>(\n" +
                                   $"        h => new {handler.Name}(({args}) => h(({args}))),\n" +
                                   $"        h => self.{ev.Name} += h,\n" +
                                   $"        h => self.{ev.Name} -= h,\n" +
                                   $"        cancellationToken\n    )";
                            returnType = $"Observable<({tuple})>";
                        }
                        else
                        {
                            returnType = "Observable<Unit>";
                            body = $"Observable.FromEvent(\n" +
                                   $"        h => self.{ev.Name} += h,\n" +
                                   $"        h => self.{ev.Name} -= h,\n" +
                                   $"        cancellationToken\n    )";
                        }

                        sb.AppendLine(
                            $"    public static {returnType} On{ev.Name}AsObservable(this {iface.Name} self, CancellationToken cancellationToken = default) =>");
                        sb.AppendLine($"        {body};\n");
                    }
                }

                sb.AppendLine("}");

                context.AddSource($"{shortName}Extensions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        }
    }
}