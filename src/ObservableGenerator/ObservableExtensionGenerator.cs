// File: ObservableExtensionGenerator.cs (Roslyn Source Generator)

using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace ObservableGenerator
{
    [Generator]
    public class ObservableExtensionGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // Optional: Add logging here
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var compilation = context.Compilation;
            var interfaces = compilation
                .GlobalNamespace
                .GetNamespaceMembers()
                .SelectMany(ns => ns.GetTypeMembers())
                .Where(t => t.TypeKind == TypeKind.Interface && t.Name.StartsWith("I"))
                .ToList();

            foreach (var iface in interfaces)
            {
                var declaredEvents = iface.GetMembers().OfType<IEventSymbol>()
                    .Where(e => e.ContainingType.Equals(iface))
                    .ToList();

                if (!declaredEvents.Any())
                {
                    continue;
                }

                var sb = new StringBuilder();
                var ifaceName = iface.Name;
                var shortName = ifaceName.StartsWith("I") ? ifaceName.Substring(1) : ifaceName;

                sb.AppendLine("using System;");
                sb.AppendLine("using System.Reactive;");
                sb.AppendLine("using System.Reactive.Linq;");
                sb.AppendLine("using System.Threading;");
                sb.AppendLine($"public static class {shortName}Extensions");
                sb.AppendLine("{");

                foreach (var ev in declaredEvents)
                {
                    var handlerType = ev.Type;
                    var invokeMethod = handlerType.DelegateInvokeMethod;
                    var parameters = invokeMethod?.Parameters;

                    string returnType;
                    string fromEvent;

                    if (parameters is { Length: 1 })
                    {
                        returnType = $"Observable<{parameters[0].Type.ToDisplayString()}>";
                        fromEvent =
                            $"Observable.FromEvent<{handlerType.ToDisplayString()}, {parameters[0].Type.ToDisplayString()}>(\n" +
                            $"        h => new {handlerType.Name}(h),\n" +
                            $"        h => self.{ev.Name} += h,\n" +
                            $"        h => self.{ev.Name} -= h,\n" +
                            $"        cancellationToken\n    )";
                    }
                    else if (parameters is { Length: > 1 })
                    {
                        var tupleType = string.Join(", ", parameters.Select(p => p.Type.ToDisplayString()));
                        var args = string.Join(", ", parameters.Select(p => p.Name));
                        returnType = $"Observable<({tupleType})>";

                        fromEvent =
                            $"Observable.FromEvent<{handlerType.ToDisplayString()}, ({tupleType})>(\n" +
                            $"        h => new {handlerType.Name}(({args}) => h(({args}))),\n" +
                            $"        h => self.{ev.Name} += h,\n" +
                            $"        h => self.{ev.Name} -= h,\n" +
                            $"        cancellationToken\n    )";
                    }
                    else
                    {
                        returnType = "Observable<Unit>";
                        fromEvent =
                            $"Observable.FromEvent(\n" +
                            $"        h => self.{ev.Name} += h,\n" +
                            $"        h => self.{ev.Name} -= h,\n" +
                            $"        cancellationToken\n    )";
                    }

                    sb.AppendLine($"    public static {returnType} On{ev.Name}AsObservable(this {ifaceName} self, CancellationToken cancellationToken = default) =>");
                    sb.AppendLine($"        {fromEvent};");
                }

                sb.AppendLine("}");

                context.AddSource($"{shortName}Extensions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
            }
        }
    }
}
