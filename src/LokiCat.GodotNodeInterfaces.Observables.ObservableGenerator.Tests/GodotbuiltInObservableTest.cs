// File: GodotBuiltInObservableGeneratorTests.cs

using FluentAssertions;
using LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Features.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace LokiCat.GodotNodeInterfaces.Observables.ObservableGenerator.Tests;

public class GodotBuiltInObservableGeneratorTests {
  private static CSharpCompilation CreateCompilation(string source)
  {
    var r3Stub = @"
      namespace R3 {
        public readonly struct Unit {
          public static readonly Unit Default;
        }
      }
    ";

    var godotStub = @"
      namespace Godot {
        public class Node {
          public void EmitSignal(string name, params object[] args) {}
        }
      }
    ";

    return CSharpCompilation.Create("TestAssembly",
      new[] {
        CSharpSyntaxTree.ParseText(source),
        CSharpSyntaxTree.ParseText(r3Stub),
        CSharpSyntaxTree.ParseText(godotStub)
      },
      new[] {
        MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(System.Threading.CancellationToken).Assembly.Location)
      },
      new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
  }

  [Fact]
  public void GenerateZeroParamObservable() {
    const string input = """
                               using Godot;
                               using System;
                         
                               namespace Chickensoft.GodotNodeInterfaces {
                                 public interface IButton {
                                   event Action Pressed;
                                 }
                               }
                         
                               public partial class Button : Node {}
""";

    var output = RunGenerator(input);
    output.Should().Contain("Observable<Unit>");
    output.Should().Contain("EmitSignal(\"pressed\")");
  }

  [Fact]
  public void GenerateMultiParamObservable() {
    const string input = """
                               using Godot;
                               using System;
                         
                               public delegate void MySignal(string a, int b);
                               
                               namespace Chickensoft.GodotNodeInterfaces {
                                 public interface IMyNode {
                                   event MySignal Fired;
                                 }
                               }
                         
                               public partial class MyNode : Node {}
""";

    var output = RunGenerator(input);
    output.Should().Contain("Observable<(string, int)>");
    output.Should().Contain("EmitSignal(\"fired\", value.Item1, value.Item2)");
  }

  [Fact]
  public void SkipGenerationWhenTooManyParams() {
    const string input = """
                           using Godot;
                           using System;
                         
                           public delegate void BigSignal(string a, string b, string c, string d, string e, string f);
                           namespace Chickensoft.GodotNodeInterfaces {
                           public interface IBadNode {
                             event BigSignal Overload;
                           }
                         
                                 }
                         
                           public partial class BadNode : Node {}
                         }
                         """;

    var diagnostics = RunGeneratorAndGetDiagnostics(input);
    diagnostics.Should().ContainSingle(d => d.Id == "OBSBUILTIN002");
  }

  [Fact]
  public void SkipEventWithMissingInvokeMethod() {
    const string input = """
      using Godot;
      using System;

      public interface IWeird {
        event int Broken;
      }

      public partial class Weird : Node {}
    """;

    var output = RunGenerator(input);
    output.Should().NotContain("Observable<");
  }

  [Fact]
  public void SkipGenerationIfClassNotPartial() {
    const string input = """
      using Godot;
      using System;

      public interface IUnmodifiable {
        event Action Unpressable;
      }

      public class Unmodifiable : Node {}
    """;

    var output = RunGenerator(input);
    output.Should().BeNull();
  }

  private static string? RunGenerator(string code) {
    var compilation = CreateCompilation(code);
    var generator = new GodotBuiltInObservableGenerator();
    CSharpGeneratorDriver.Create(generator)
      .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

    return updated.SyntaxTrees
      .FirstOrDefault(t => t.FilePath.Contains(".BuiltinObservables.g.cs"))
      ?.ToString();
  }

  private static IReadOnlyList<Diagnostic> RunGeneratorAndGetDiagnostics(string code) {
    var compilation = CreateCompilation(code);
    var generator = new GodotBuiltInObservableGenerator();
    GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
    driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
    return diagnostics;
  }
}
