using FluentAssertions;
using LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Tests;

public class EventWrapperGenerationShould {
    
    private static CSharpCompilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create("TestAssembly",
                                        [CSharpSyntaxTree.ParseText(source)],
                                        [
                                            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                                            MetadataReference.CreateFromFile(
                                                typeof(CancellationToken).Assembly.Location),
                                            MetadataReference.CreateFromFile(
                                                typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly
                                                    .Location),
                                            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
                                        ],
                                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
    }

    [Fact]
    public void GenerateZeroParameterDelegates() {
        const string CODE = """
                            using System;
                            public interface IFoo {
                              event Action NoParams;
                            }
                            """;

        var wrapper = GenerateWrapper(CODE, "NoParams");
        wrapper.Should().Contain("Observable<Unit>");
        wrapper.Should().Contain("Observable.FromEvent");
        wrapper.Should().Contain("+= h");
    }

    [Fact]
    public void GenerateOneParameterDelegatesWithConstructors() {
        const string CODE = """
                            using System;
                            public delegate void CustomHandler(string value);
                            public interface IFoo {
                              event CustomHandler OneParam;
                            }
                            """;

        var wrapper = GenerateWrapper(CODE, "OneParam");
        wrapper.Should().Contain("Observable<string>");
        wrapper.Should().Contain("CustomHandler");
    }

    [Fact]
    public void GenerateOneParameterDelegatesWithoutConstructors() {
        const string CODE = """
                            using System;
                            public interface IFoo {
                              event Action<string> OneParam;
                            }
                            """;

        var wrapper = GenerateWrapper(CODE, "OneParam");
        wrapper.Should().Contain("Observable<string>");
        wrapper.Should().Contain("Action<string>");
        wrapper.Should().Contain("(obj) => h(obj)");
    }

    [Fact]
    public void GenerateMultipleParameterDelegates() {
        const string CODE = """
                            using System;
                            public delegate void MyEvent(string a, int b);
                            public interface IFoo {
                              event MyEvent Complex;
                            }
                            """;

        var wrapper = GenerateWrapper(CODE, "Complex");

        wrapper.Should().Contain("Observable<(string, int)>");
        wrapper.Should().Contain("(string a, int b)");
        wrapper.Should().Contain("h((a, b))");
    }

    [Fact]
    public void GenerateNothingWhenEventMissingAddOrRemove() {
        const string CODE = """
                            using System;
                            public interface IFoo {
                              event Action Broken {
                                add { }
                                // No remove
                              }
                            }
                            """;

        var wrapper = GenerateWrapper(CODE, "Broken");
        wrapper.Should().BeEmpty();
    }

    [Fact]
    public void GenerateNothingWhenEventDelegateInvalid() {
        const string CODE = """
                            using System;
                            public interface IFoo {
                              event int NotADelegate;
                            }
                            """;

        var wrapper = GenerateWrapper(CODE, "NotADelegate");
        wrapper.Should().BeEmpty();
    }

    private static string GenerateWrapper(string code, string eventName) {
        var compilation = CreateCompilation(code);
        var symbol = compilation.GetTypeByMetadataName("IFoo");

        symbol.Should().NotBeNull();

        var ev = symbol.GetMembers()
                       .OfType<IEventSymbol>()
                       .FirstOrDefault(e => e.Name == eventName);

        ev.Should().NotBeNull();

        return new EventWrapperGenerator().GetEventWrapper(ev!, symbol);
    }
}