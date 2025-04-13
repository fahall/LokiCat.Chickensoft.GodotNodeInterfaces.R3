// File: ObservableExtensionGenerator.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace LokiCat.Chickensoft.GodotNodeInterfaces.R3.ObservableGenerator;

internal static class BuildGuard { }

[Generator]
public class ObservableExtensionGenerator : ISourceGenerator
{
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
                                     .FirstOrDefault(n => n.Name == "Chickensoft")
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

    private static void ExtendInterface(GeneratorExecutionContext context, INamedTypeSymbol iface)
    {
        var events = iface.GetMembers()
                          .OfType<IEventSymbol>()
                          .Where(e => e.ContainingType.Equals(iface, SymbolEqualityComparer.Default))
                          .ToList();

        if (events.Count == 0)
        {
            return;
        }

        var wrappers = BuildEventWrappers(iface, events).ToArray();

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

    private static IEnumerable<string> BuildEventWrappers(INamedTypeSymbol iface, List<IEventSymbol> events) =>
        events.SelectMany(e => GetEventWrapper(e, iface));

    private static string[] GetEventWrapper(IEventSymbol ev, INamedTypeSymbol iface)
    {
        if (string.IsNullOrWhiteSpace(ev.Name) || ev.AddMethod == null || ev.RemoveMethod == null)
        {
            return [];
        }

        if (ev.Type is not INamedTypeSymbol { TypeKind: TypeKind.Delegate } handler)
        {
            return [];
        }

        var invoke = handler.DelegateInvokeMethod;

        if (invoke is null)
        {
            return [];
        }

        var parameters = invoke.Parameters;
        string returnType;
        string body;
        string handlerName = handler.ToGeneratorTypeString();
        string delegateName = handler.Name;

        // Detect if the delegate type has a usable constructor (e.g., public Foo(Action<X>))
        bool hasConstructor = handler.Constructors.Any(c =>
                                                           c.DeclaredAccessibility == Accessibility.Public &&
                                                           c.Parameters.Length == 1 &&
                                                           c.Parameters[0].Type.TypeKind == TypeKind.Delegate
        );

        if (parameters.Length == 1)
        {
            var p = parameters[0];
            var paramType = p.Type.ToGeneratorTypeString();
            returnType = $"Observable<{paramType}>";

            var handlerExpression = hasConstructor
                ? $"new {delegateName}(h)"
                : $"({p.Name.EscapeIdentifier()}) => h({p.Name.EscapeIdentifier()})";

            body = $"""
                         Observable.FromEvent<{handlerName}, {paramType}>(
                             h => {handlerExpression},
                             h => self.{ev.Name} += h,
                             h => self.{ev.Name} -= h,
                             cancellationToken
                         )
                     """;
        }
        else if (parameters.Length > 1)
        {
            var tupleType = string.Join(", ", parameters.Select(p => p.Type.ToGeneratorTypeString()));
            var argList = string.Join(", ", parameters.Select(p => p.Name.EscapeIdentifier()));
            var paramList =
                string.Join(
                    ", ", parameters.Select(p => p.Type.ToGeneratorTypeString() + " " + p.Name.EscapeIdentifier()));
            returnType = $"Observable<({tupleType})>";

            var handlerExpression = hasConstructor
                ? $"new {delegateName}(({paramList}) => h(({argList})))"
                : $"({paramList}) => h(({argList}))";

            body = $"""
                         Observable.FromEvent<{handlerName}, ({tupleType})>(
                             h => {handlerExpression},
                             h => self.{ev.Name} += h,
                             h => self.{ev.Name} -= h,
                             cancellationToken
                         )
                     """;
        }
        else
        {
            returnType = "Observable<Unit>";
            body = $"""
                         Observable.FromEvent(
                             h => self.{ev.Name} += h,
                             h => self.{ev.Name} -= h,
                             cancellationToken
                         )
                     """;
        }

        return
        [
            $"public static {returnType} On{ev.Name}AsObservable(this {iface.ToGeneratorTypeString()} self, CancellationToken cancellationToken = default) =>",
            $"    {body};\n"
        ];
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

        void AddNamespace(INamespaceSymbol? ns)
        {
            if (ns != null && !ns.IsGlobalNamespace)
            {
                nsSet.Add(ns.ToDisplayString());
            }
        }

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
    }
}

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