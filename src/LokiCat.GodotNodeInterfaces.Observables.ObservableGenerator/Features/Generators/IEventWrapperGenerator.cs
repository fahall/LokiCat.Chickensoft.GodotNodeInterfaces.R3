using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Generators;

internal interface IEventWrapperGenerator
{
    public IEnumerable<string> BuildEventWrappers(INamedTypeSymbol iface, List<IEventSymbol> events);

    // ReSharper disable once UnusedMemberInSuper.Global
    public string GetEventWrapper(IEventSymbol ev, INamedTypeSymbol iface);
}