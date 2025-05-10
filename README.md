# LokiCat.GodotNodeInterfaces.Observables

> **Generate R3 Observables from Godot signals via Chickensoft.GodotNodeInterfaces**

![NuGet](https://img.shields.io/nuget/v/LokiCat.GodotNodeInterfaces.Observables?label=NuGet)
[![CI](https://github.com/fahall/LokiCat.GodotNodeInterfaces.Observables/actions/workflows/release.yml/badge.svg)](https://github.com/fahall/LokiCat.GodotNodeInterfaces.Observables/actions/workflows/release.yml)

---

## Overview

### Why bother?

We want:

1. ✅ To program to interfaces (via Chickensoft.GodotNodeInterfaces)
2. ✅ To use ReactiveX-style observables (via R3)
3. ❌ But we don't want to manually wire every Godot signal ourselves

This library gives us **declarative, strongly-typed observables** for signals across interfaces, Godot built-ins, and `[Signal]` delegates — all auto-generated.

#### Baseline: Manual event wiring

```csharp
public partial class MyMenu : Control, IControl {
  private IBaseButton button;

  public void OnReady() {
    button.Pressed += HandlePressed;
  }

  public void OnExitTree() {
    button.Pressed -= HandlePressed;
  }

  private void HandlePressed() => GD.Print("Pressed!");
}
```

* ❌ Must manage subscribe/unsubscribe manually
* ❌ Can’t compose with R3

#### Manual R3 Observable

```csharp
Observable.FromEvent<PressedEventHandler, Unit>(
  h => () => h(Unit.Default),
  h => button.Pressed += h,
  h => button.Pressed -= h
)
.Subscribe(_ => GD.Print("Pressed"))
.AddTo(this);
```

* ✅ Works
* ❌ Verbose
* ❌ Easy to miswire

#### ✅ With LokiCat.GodotNodeInterfaces.Observables

```csharp
button.OnPressedAsObservable()
  .Subscribe(_ => GD.Print("Pressed"))
  .AddTo(this);
```

* ✅ Clean
* ✅ Reactive
* ✅ Auto cleanup
* ✅ Interface- and class-based support
* ✅ Works for custom `[Signal]`s and built-ins

This project provides **Roslyn source generators** that create [R3](https://github.com/Cysharp/R3) observables for:

1. **All `event`s defined in `Chickensoft.GodotNodeInterfaces`** interfaces.
2. **All signals in built-in Godot classes** (e.g., `Button`, `Node2D`) if you define a `partial` class in your project.
3. **Any user-defined `[Signal]` delegate** in your own Godot partial classes.

These generators output strongly-typed, composable observables for signals using idiomatic R3 patterns.

### Why use this?

* ✅ Program to interfaces with `Chickensoft.GodotNodeInterfaces`
* ✅ Use ReactiveX (`R3`) for signal handling
* ✅ Avoid writing manual signal-to-observable plumbing
* ✅ Get compile-time safety and auto-cleanup via `.AddTo(this)`

---

## 🔍 Example

```csharp
public partial class MyMenu : Control, IControl {
  private IBaseButton doSomethingButton;

  public override void _Ready() {
    doSomethingButton.OnPressedAsObservable()
      .Subscribe(_ => GD.Print("Pressed!"))
      .AddTo(this);
  }
}
```

If you define your own `[Signal]`-annotated delegates:

```csharp
[Signal] private delegate void ToggledEventHandler(bool toggled);

public partial class ToggleThing : Node {
  private Subject<bool> _onToggled = new();
  public Observable<bool> OnToggled => _onToggled ??= ConnectToggled();
}
```

And for built-in classes:

```csharp
public partial class Button : Godot.Button { }
```

Triggers generation of:

```csharp
private Subject<Unit>? _onPressed;
public Observable<Unit> OnPressed => _onPressed ??= ConnectPressed();
```

---

## ✨ Features

* 🔧 **Zero config** — just mark your classes `partial` and you're done
* ⚙️ **Supports 0–5 signal parameters**
* 📡 **Built-in Godot signal support** via known interface mappings
* 🧠 **Handles custom signal delegates** with or without constructors
* 📎 **Avoids duplicating built-in signal wiring**
* 🧪 Thoroughly tested with diagnostic output

---

## 📦 Installation

Install via NuGet:

```bash
dotnet add package LokiCat.GodotNodeInterfaces.Observables
```

---

## 🧪 Tests

Covers:

* Signals with 0–5 parameters
* Interfaces with custom and standard delegate types
* Edge cases like non-`partial` classes or invalid delegate wiring

```bash
dotnet test
```

---

## 🧱 How It Works

* Scans `Chickensoft.GodotNodeInterfaces` for all `interface`s with `event`s.
* For each:

    * Emits extension method `On[EventName]AsObservable()`
* Separately, scans all `partial class`es in your project:

    * If it inherits a known Godot class (e.g., `Button`), emits `_onX` / `OnX` fields + observable logic for signals like `pressed`, `toggled`, etc.
    * If it defines a `[Signal]`-annotated delegate, generates a subject-backed observable for it.
* Supports 0–5 parameters; skips and warns if more.

---

## 🙏 Credits

* [Chickensoft](https://github.com/chickensoft-games) for `GodotNodeInterfaces`
* [Cysharp](https://github.com/Cysharp/R3) for `R3`
* [Godot C# community](https://github.com/godotengine/godot) for enabling typed signals in C#

---

## 📄 License

MIT
