using System.Collections.Generic;
using System.Linq;
using LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Interfaces;
using Microsoft.CodeAnalysis;

namespace LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Generators;

internal class EventWrapperGenerator : IEventWrapperGenerator
{
    public IEnumerable<string> BuildEventWrappers(INamedTypeSymbol iface, List<IEventSymbol> events)
    {
        return events.Select(e => GetEventWrapper(e, iface));
    }

    public string GetEventWrapper(IEventSymbol ev, INamedTypeSymbol iface)
    {
        if (string.IsNullOrWhiteSpace(ev.Name) || ev.AddMethod == null || ev.RemoveMethod == null)
        {
            return "";
        }

        if (ev.Type is not INamedTypeSymbol { TypeKind: TypeKind.Delegate } handler)
        {
            return "";
        }

        var invoke = handler.DelegateInvokeMethod;

        if (invoke is null)
        {
            return "";
        }

        var parameters = invoke.Parameters;
        string returnType;
        string body;
        var handlerName = handler.ToGeneratorTypeString();
        var delegateName = handler.Name;

        // Detect if the delegate type has a usable constructor (e.g., public Foo(Action<X>))
        var hasConstructor = handler.Constructors.Any(c =>
                                                          c is { DeclaredAccessibility: Accessibility.Public, Parameters.Length: 1 } &&
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

        var prologue = $"public static {returnType} On{ev.Name}AsObservable(this {iface.ToGeneratorTypeString()} self, CancellationToken cancellationToken = default) =>";
        var txt = string.Join("\n",
                              prologue,
                              $"    {body};");

        return txt;
    }
}