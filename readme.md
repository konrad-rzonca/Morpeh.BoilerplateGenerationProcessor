# Morpeh.BoilerplateGenerationProcessor

A Roslyn Source Generator for Unity that automatically adds performance-critical attributes (`[Serializable]`,
`[Il2CppSetOption]`) to Morpeh components and systems. This reduces boilerplate and improves runtime performance in
IL2CPP builds.

## 🚀 Installation (for Unity Users)

This is the simplest way to install the generator. It does **not** require any package manager setup or authentication
tokens.

1. Go to the **[Releases Page](https://github.com/konrad-rzonca/Morpeh.BoilerplateGenerationProcessor/releases)** for
   this repository.

2. Download the `Morpeh.Boilerplate.SourceGenerator.zip` file from the latest release.

3. Create a folder in your Unity project. The recommended location is `Assets/Plugins/RoslynAnalyzers`.

4. **Unzip all the DLLs** from the downloaded file directly into that new folder (e.g.,
   `Morpeh.BoilerplateGenerationProcessor.dll`, `Microsoft.CodeAnalysis.dll`, etc.).

5. **Configure the DLLs in Unity.** This is the most important step.
    * In the Unity Editor, navigate to the folder where you unzipped the files.
    * Select **all** the DLLs you just unzipped.
    * In the Inspector, uncheck **Any Platform** and check **Editor** only.
    * Click **Apply**.

6. **Tag the Analyzer.**
    * Select `Morpeh.BoilerplateGenerationProcessor.dll` only.
    * In the Inspector, find the "Asset Labels" section at the bottom.
    * Type **`RoslynAnalyzer`** into the box and press
      Enter - [Unity guidelines](https://docs.unity3d.com/6000.2/Documentation/Manual/install-existing-analyzer.html).
    * Click **Apply** again.

7. **Restart the Unity Editor.** The generator is now active and will automatically process your Morpeh files.

---

## 📝 Usage Rules

To enable automatic attribute generation, declare your Morpeh types as `partial`.

The generator applies attributes to any non-static, `partial` type that implements one of the following Morpeh
interfaces:

* `Scellecs.Morpeh.IComponent`
* `Scellecs.Morpeh.ISystem` (and related interfaces like `IInitializer`, `IFixedSystem`, etc.)

---

### Morpeh Components

**Before:** Declare your component as a `partial struct`.

```csharp
// Before:
public partial struct MyComponent : IComponent 
{ 
    public int Value;
}
```

**After (generated):** The generator creates a separate partial definition with the `[Serializable]` attribute.

```csharp
// After (generated):
[global::System.Serializable]
public partial struct MyComponent
{
}
```

---

### Morpeh Systems

**Before:** Declare your system as a `partial class`.

```csharp
// Before:
public sealed partial class MySystem : ISystem 
{ 
    // ...
}
```

**After (generated):** The generator adds performance-related attributes.

```csharp
// After (generated):
[global::Unity.IL2CPP.CompilerServices.Il2CppSetOption(global::Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
[global::Unity.IL2CPP.CompilerServices.Il2CppSetOption(global::Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
public partial class MySystem
{
}
```

---

## ⚠️ Limitations

* **Static Classes:** **Must be annotated manually.** The generator cannot process `static` classes because they cannot
  be `partial`.
* **Nested Types in Static Classes:** A Morpeh component or system **cannot** be nested inside a `static` class. This is
  because the generator needs to create a `partial` declaration for the parent class, which is not allowed for `static`
  classes.

---

## 🔍 Diagnostics

* **BGP001 (Error):** An internal generator error occurred during processing.
* **BGP002 (Error):** A Morpeh type is nested inside a `static` class, which is not supported.

---

## 📦 How to Publish a New Version (for Developers)

This repository is configured with a GitHub Action (`.github/workflows/dotnet-publish.yml`) that automatically builds,
tests, and creates a release.

To publish a new version:

1. Ensure your code changes are committed to the `master` branch.
2. Create a new Git tag that starts with `v` (e.g., `v1.0.1`, `v1.1.0`).

       git tag v1.0.1

3. Push the tag to GitHub.

       git push origin v1.0.1

The GitHub Action will detect this new tag, run the build and tests, and then automatically create a new GitHub Release.
It will attach two files:

* `Morpeh.Boilerplate.SourceGenerator.zip`: For manual installation in Unity.
* `Morpeh.Boilerplate.SourceGenerator.[Version].nupkg`: For installation via NuGet or OpenUPM.