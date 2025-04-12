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
    
    /// <summary>
    /// A source generator that generates extension methods for Godot Node interfaces provided by the Chickensoft.GodotNodeInterfaces package.
    /// </summary>
    [Generator]
    public class ObservableExtensionGenerator : ISourceGenerator
    {
        /// <summary>
        /// Initializes the generator. This method is called once per compilation.
        /// </summary>
        public void Initialize(GeneratorInitializationContext context)
        {
            // no-op for now
        }

        /// <summary>
        /// Run the generator. This method is called once per compilation.
        /// </summary>
        /// <param name="context"></param>
        public void Execute(GeneratorExecutionContext context)
        {
            foreach (var nextInterface in GetInterfaces(context))
            {
                ExtendInterface(context, nextInterface);
            }
        }

        private static List<INamedTypeSymbol> GetInterfaces(GeneratorExecutionContext context)
        {
            var godotInterfaces = context.Compilation
                                         .GetSymbolsWithName(name => name.StartsWith("I") && name.Contains("Godot"), SymbolFilter.Type)
                                         .OfType<INamedTypeSymbol>()
                                         .Where(t => t.TypeKind == TypeKind.Interface && t.ContainingNamespace.ToDisplayString().Contains("Chickensoft.GodotNodeInterfaces"))
                                         .ToList();

            return godotInterfaces;
        }

        private static void ExtendInterface(GeneratorExecutionContext context, INamedTypeSymbol anInterface)
        {
            var events = anInterface.GetMembers().OfType<IEventSymbol>()
                              .Where(ev => ev.ContainingType.Equals(anInterface, SymbolEqualityComparer.Default))
                              .ToList();

            if (events.Count == 0)
            {
                return;
            }

            var sb = new StringBuilder();
            var shortName = anInterface.Name.TrimStart('I');

            sb.AppendLine("using System;");
            sb.AppendLine("using System.Reactive;");
            sb.AppendLine("using System.Reactive.Linq;");
            sb.AppendLine("using System.Threading;");
            sb.AppendLine($"public static class {shortName}Extensions");
            sb.AppendLine("{");

            BuildEventWrappers(events, sb, anInterface);

            sb.AppendLine("}");

            context.AddSource($"{shortName}Extensions.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static void BuildEventWrappers(List<IEventSymbol> events, StringBuilder sb, INamedTypeSymbol iface)
        {
            foreach (var ev in events)
            {
                BuildEventWrapper(ev, sb, iface);
            }
        }

        private static void BuildEventWrapper(IEventSymbol ev, StringBuilder sb, INamedTypeSymbol iface)
        {
            if (ev.Type is not INamedTypeSymbol { TypeKind: TypeKind.Delegate } handler)
            {
                return;
            }

            var invoke = handler.DelegateInvokeMethod;
            if(invoke is null)
            {
                return;
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
}