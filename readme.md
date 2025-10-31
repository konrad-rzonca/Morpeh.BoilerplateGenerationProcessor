# Morpeh.BoilerplateGenerationProcessor

A Roslyn Source Generator for Unity that automatically adds performance-critical attributes (`[Serializable]`, `[Il2CppSetOption]`) to Morpeh components and systems. This reduces boilerplate and improves runtime performance in IL2CPP builds.

## Installation & Build

1.  **Build the DLL:**
    - Open a terminal in the generator's project root directory.
    - Run `dotnet build -c Release`.
    - The DLL is located in `bin/Release/netstandard2.0/Morpeh.BoilerplateGenerationProcessor.dll`.

2.  **Install in Unity:**
    - Copy the generated `.dll` into your Unity project (e.g., `Assets/Plugins/RoslynAnalyzers/`).
    - In the Unity Inspector for the `.dll`, set the platform to **Editor only** and add the Asset Label **`RoslynAnalyzer`**.
    - Click **Apply** and **restart the Unity Editor**.

## Usage Rules

The generator applies attributes to any non-static, `partial` type that implements one of the following Morpeh interfaces:
- `Scellecs.Morpeh.IComponent`
- `Scellecs.Morpeh.ISystem` (and related interfaces like `IInitializer`, `IFixedSystem`, etc.)


-   **Morpeh Components:** Must be `partial`.
    ```csharp
    // Before
    public struct MyComponent : IComponent { /* ... */ }
    
    // After applying fix (making it partial)
    public partial struct MyComponent : IComponent { /* ... */ }
    ```

-   **Morpeh Systems:** Must be `partial`.
    ```csharp
    // Before
    public sealed class MySystem : ISystem { /* ... */ }

    // After applying fix (making it partial)
    public sealed partial class MySystem : ISystem { /* ... */ }
    ```

### Limitations

-   **Static Classes:** **Must be annotated manually.** The generator cannot process `static` classes because they cannot be `partial`.
-   **Nested Types in Static Classes:** A Morpeh component or system **cannot** be nested inside a `static` class. This is because the generator needs to create a `partial` declaration for the parent class, which is not allowed for `static` classes.

### Diagnostics

-   **MORPEH001 (Warning):** If a Morpeh component or system is missing the `partial` keyword, the generator will produce a warning in the Unity Console.
-   **MORPEH002 (Error):** An internal generator error occurred during processing.
-   **MORPEH003 (Info):** A `partial struct` is found that does not implement `IComponent`, suggesting it might be a candidate for being a Morpeh component.
-   **MORPEH004 (Error):** A Morpeh type is nested inside a `static` class, which is not supported.