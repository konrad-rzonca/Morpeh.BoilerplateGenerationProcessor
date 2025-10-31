# Morpeh.BoilerplateGenerationProcessor

A Roslyn Source Generator for Unity that automatically adds performance-critical attributes (`[Serializable]`, `[Il2CppSetOption]`) to Morpeh components, systems, and other targeted types. This reduces boilerplate and improves runtime performance in IL2CPP builds.

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

The generator applies attributes to any non-static type marked with the `partial` keyword.

-   **Morpeh Components:** Must be `partial`.
    ```csharp
    public partial struct MyComponent : IComponent { /* ... */ }
    ```

-   **Morpeh Systems:** Must be `partial`.
    ```csharp
    public sealed partial class MySystem : ISystem { /* ... */ }
    ```

-   **Static Classes:** **Must be annotated manually.** The generator cannot process `static` classes as they cannot be `partial`.

### Diagnostics

If a Morpeh component or system is missing the `partial` keyword, the generator will produce a warning (`APC001`) in the Unity Console.