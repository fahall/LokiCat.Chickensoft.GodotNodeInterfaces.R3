# LokiCat.GodotNodeInterfaces.Observables

> **Generate R3 Observables from Chickensoft.GodotNodeInterfaces signals**

![NuGet](https://img.shields.io/nuget/v/LokiCat.GodotNodeInterfaces.Observables?label=NuGet)
[![CI](https://github.com/fahall/LokiCat.GodotNodeInterfaces.Observables/actions/workflows/release.yml/badge.svg)](https://github.com/fahall/LokiCat.GodotNodeInterfaces.Observables/actions/workflows/release.yml)

---

## Overview

This project provides a **Roslyn source generator** that automatically creates [R3](https://github.com/vrld/R3) observable extensions for each signal exposed by the [`Chickensoft.GodotNodeInterfaces`](https://github.com/chickensoft-games/godot-node-interfaces) interfaces.

You get strongly typed, composable observables that make it easy to work with Godot signals in a reactive way using R3.

---

## ✨ Features

- 🔧 **Zero config**: Just install the package and observe away.
- ⚡ **Automatic discovery** of all signals defined in `Chickensoft.GodotNodeInterfaces`.
- 🧪 Fully tested generator logic.
- 🧵 Handles custom delegate signal types (like `ButtonPressedEventHandler`).
- 📦 Designed for `.NET 7+`, compatible with Godot 4.x C# projects.

---

## 📦 Installation

Install via NuGet:

```bash
dotnet add package LokiCat.GodotNodeInterfaces.Observables
```

---

## 🔌 Usage

No setup is required. Once the package is installed, the source generator will run during build and add extension methods for each interface's signals.

You can observe any Godot signal (from the Chickensoft interfaces) like so:

```csharp
public partial class MyNode : Node2D {
  public override void _Ready() {
    this.OnPressedAsObservable()
      .Subscribe(_ => GD.Print("Pressed!"));
  }
}
```

The method name pattern is:

```csharp
On[SignalName]AsObservable()
```

These methods are automatically added to the interface types like `IButton`, `IArea2D`, etc.

Each observable uses the signal's native delegate (including custom Godot signal delegate types), and returns `Observable<T>` where `T` is:

- The signal parameter type (if 1 parameter)
- A tuple `(T1, T2, ...)` (if multiple parameters)
- `Unit` (if no parameters)

---

## 🧪 Examples

### Observing `Pressed` from `IButton`

```csharp
button.OnPressedAsObservable()
  .Subscribe(_ => GD.Print("Button was pressed"));
```

### Observing `MouseEntered` from `IArea2D`

```csharp
area.OnMouseEnteredAsObservable()
  .Subscribe(_ => ShowTooltip());
```

### Observing complex signal with parameters

For a signal like:

```csharp
event InputEventEventHandler(Godot.Node viewport, Godot.InputEvent @event, long shapeIdx);
```

Generated observable extension:

```csharp
collision.OnInputEventAsObservable()
  .Subscribe(tuple => {
    var (viewport, evt, shapeIdx) = tuple;
    GD.Print($"Input received on shape {shapeIdx}");
  });
```

### Composing observables with R3

```csharp
button.OnPressedAsObservable()
  .ThrottleFirst(TimeSpan.FromMilliseconds(500))
  .Subscribe(_ => GD.Print("Throttled click!"));
```

---

## ⚙️ Dependencies

- [.NET 7+](https://dotnet.microsoft.com/en-us/download/dotnet/7.0)
- [R3](https://github.com/vrld/R3)
- [Chickensoft.GodotNodeInterfaces](https://github.com/chickensoft-games/godot-node-interfaces)

---

## 🧱 How It Works

- Uses Roslyn source generation to discover every `event` declared in `Chickensoft.GodotNodeInterfaces` interfaces.
- Ignores inherited events (to avoid duplicate generation).
- Generates extension methods per event with exact types.
- Handles custom Godot signal delegates with correct constructor/parameter logic.

All source is generated at compile time and added to your build transparently.

---

## 🧪 Tests

Tests cover:

- Events with 0, 1, or many parameters
- Delegates with and without constructors
- Namespace resolution
- Method name and return type formatting

You can run tests locally:

```bash
dotnet test
```

---

## 🛠 Development

To build from source:

```bash
dotnet build -c Release
```

To pack for NuGet:

```bash
dotnet pack -c Release
```

---

## 🙏 Credits

- [Chickensoft](https://github.com/chickensoft-games) for the base interfaces
- [R3](https://github.com/vrld/R3) for a clean observable abstraction
- [Godot C# community](https://github.com/godotengine/godot) for enabling signal-first development

---

## 📄 License

MIT
