using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace LokiCat.Chickensoft.GodotNodeInterfaces.R3.ObservableGenerator;

internal interface IEventWrapperGenerator
{
    public IEnumerable<string> BuildEventWrappers(INamedTypeSymbol iface, List<IEventSymbol> events);

    public string GetEventWrapper(IEventSymbol ev, INamedTypeSymbol iface);
}