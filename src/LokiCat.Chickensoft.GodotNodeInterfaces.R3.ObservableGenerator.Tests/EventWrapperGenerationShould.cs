using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LokiCat.Chickensoft.GodotNodeInterfaces.R3.ObservableGenerator.Tests;

public class EventWrapperGenerationShould {
    
    private static Compilation CreateCompilation(string source) =>
        CSharpCompilation.Create("TestAssembly",
                                 new[] { CSharpSyntaxTree.ParseText(source) },
                                 new[] {
                                     MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                                     MetadataReference.CreateFromFile(typeof(CancellationToken).Assembly.Location),
                                     MetadataReference.CreateFromFile(typeof(System.Runtime.CompilerServices.DynamicAttribute).Assembly.Location),
                                     MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location)
                                 },
                                 new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

    [Fact]
    public void GenerateZeroParameterDelegates() {
        const string code = """
                            using System;
                            public interface IFoo {
                              event Action NoParams;
                            }
                            """;

        var wrapper = GenerateWrapper(code, "NoParams");
        wrapper.Should().Contain("Observable<Unit>");
        wrapper.Should().Contain("Observable.FromEvent");
        wrapper.Should().Contain("+= h");
    }

    [Fact]
    public void GenerateOneParameterDelegatesWithConstructors() {
        const string code = """
                            using System;
                            public delegate void CustomHandler(string value);
                            public interface IFoo {
                              event CustomHandler OneParam;
                            }
                            """;

        var wrapper = GenerateWrapper(code, "OneParam");
        wrapper.Should().Contain("Observable<string>");
        wrapper.Should().Contain("CustomHandler");
    }

    [Fact]
    public void GenerateOneParameterDelegatesWithoutConstructors() {
        const string code = """
                            using System;
                            public interface IFoo {
                              event Action<string> OneParam;
                            }
                            """;

        var wrapper = GenerateWrapper(code, "OneParam");
        wrapper.Should().Contain("Observable<string>");
        wrapper.Should().Contain("Action<string>");
        wrapper.Should().Contain("(obj) => h(obj)");
    }

    [Fact]
    public void GenerateMultipleParameterDelegates() {
        const string code = """
                            using System;
                            public delegate void MyEvent(string a, int b);
                            public interface IFoo {
                              event MyEvent Complex;
                            }
                            """;

        var wrapper = GenerateWrapper(code, "Complex");

        wrapper.Should().Contain("Observable<(string, int)>");
        wrapper.Should().Contain("(string a, int b)");
        wrapper.Should().Contain("h((a, b))");
    }

    [Fact]
    public void GenerateNothingWhenEventMissingAddOrRemove() {
        const string code = """
                            using System;
                            public interface IFoo {
                              event Action Broken {
                                add { }
                                // No remove
                              }
                            }
                            """;

        var wrapper = GenerateWrapper(code, "Broken");
        wrapper.Should().BeEmpty();
    }

    [Fact]
    public void GenerateNothingWhenEventDelegateInvalid() {
        const string code = """
                            using System;
                            public interface IFoo {
                              event int NotADelegate;
                            }
                            """;

        var wrapper = GenerateWrapper(code, "NotADelegate");
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