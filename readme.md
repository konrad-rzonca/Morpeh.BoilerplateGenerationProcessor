# Morpeh.BoilerplateGenerationProcessor

A Roslyn Source Generator for Unity that automatically adds performance-critical attributes (`[Serializable]`, `[Il2CppSetOption]`) to Morpeh components and systems. This reduces boilerplate and improves runtime performance in IL2CPP builds.

## 🚀 Installation (for Unity Users)

This is the simplest way to install the generator. It does **not** require any package manager setup or authentication tokens.

1.  Go to the **[Releases Page](https://github.com/konrad-rzonca/Morpeh.BoilerplateGenerationProcessor/releases)** for this repository.
    > **Note:** Be sure to update the `YOUR_USERNAME` part of the link to your own GitHub account.

2.  Download the `Morpeh.Boilerplate.SourceGenerator.zip` file from the latest release.

3.  Create a folder in your Unity project. The recommended location is `Assets/Plugins/RoslynAnalyzers`.

4.  **Unzip all the DLLs** from the downloaded file directly into that new folder (e.g., `Morpeh.BoilerplateGenerationProcessor.dll`, `Microsoft.CodeAnalysis.dll`, etc.).

5.  **Configure the DLLs in Unity.** This is the most important step.
    * In the Unity Editor, navigate to the folder where you unzipped the files.
    * Select **all** the DLLs you just unzipped.
    * In the Inspector, uncheck **Any Platform** and check **Editor** only.
    * Click **Apply**.

6.  **Tag the Analyzer.**
    * With all the DLLs still selected, deselect all of them **except** for `Morpeh.BoilerplateGenerationProcessor.dll`.
    * In the Inspector, find the "Asset Labels" section at the bottom.
    * Type **`RoslynAnalyzer`** into the box and press Enter.
    * Click **Apply** again.

7.  **Restart the Unity Editor.** The generator is now active and will automatically process your Morpeh files.

---

## 📝 Usage Rules

The generator applies attributes to any non-static, `partial` type that implements one of the following Morpeh interfaces:
* `Scellecs.Morpeh.IComponent`
* `Scellecs.Morpeh.ISystem` (and related interfaces like `IInitializer`, `IFixedSystem`, etc.)

---

### Morpeh Components
Must be `partial`.

```csharp
// Before
public struct MyComponent : IComponent { /* ... */ }
.
// After applying fix (making it partial)
public partial struct MyComponent : IComponent { /* ... */ }
```

---

### Morpeh Systems
Must be `partial`.

```csharp
// Before
public sealed class MySystem : ISystem { /* ... */ }

// After applying fix (making it partial)
public sealed partial class MySystem : ISystem { /* ... */ }
```

---

## ⚠️ Limitations

* **Static Classes:** **Must be annotated manually.** The generator cannot process `static` classes because they cannot be `partial`.
* **Nested Types in Static Classes:** A Morpeh component or system **cannot** be nested inside a `static` class. This is because the generator needs to create a `partial` declaration for the parent class, which is not allowed for `static` classes.

---

## 🔍 Diagnostics

* **MORPEH001 (Warning):** If a Morpeh component or system is missing the `partial` keyword, the generator will produce a warning in the Unity Console.
* **MORPEH002 (Error):** An internal generator error occurred during processing.
* **MORPEH003 (Info):** A `partial struct` is found that does not implement `IComponent`, suggesting it might be a candidate for being a Morpeh component.
* **MORPEH004 (Error):** A Morpeh type is nested inside a `static` class, which is not supported.

---

## 📦 How to Publish a New Version (for Developers)

This repository is configured with a GitHub Action (`.github/workflows/dotnet-release.yml`) that automatically builds, tests, and creates a release.

To publish a new version:
1.  Ensure your code changes are committed to the `main` branch.
2.  Create a new Git tag that starts with `v` (e.g., `v1.0.1`, `v1.1.0`).
    ```sh
    git tag v1.0.1
    ```
3.  Push the tag to GitHub.
    ```sh
    git push origin v1.0.1
    ```

The GitHub Action will detect this new tag, run the build and tests, and then automatically create a new GitHub Release. It will attach the properly-built `Morpeh.Boilerplate.SourceGenerator.zip` file to that release, making it available for users to download.