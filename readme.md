# Morpeh.BoilerplateGenerationProcessor

A Roslyn Source Generator for Unity that automatically adds performance-critical attributes (`[Serializable]`, `[Il2CppSetOption]`) to Morpeh components and systems. This reduces boilerplate and improves runtime performance in IL2CPP builds.

## Installation (from GitHub Packages)

This generator is hosted on GitHub Packages. You only need to build it if you are developing the generator itself.

### Step 1: Generate a Personal Access Token (PAT)

To access packages on GitHub, Unity needs a **Personal Access Token (PAT)**.

1.  Go to your GitHub **Developer Settings** > **Personal access tokens** > **Tokens (classic)**.
2.  Click **"Generate new token"**.
3.  Give it a **Note** (e.g., "Unity Package Manager").
4.  Set the **Expiration** (e.g., 90 days).
5.  Select the **`read:packages`** scope. This is the only permission needed.

6.  Click **"Generate token"** and **copy the token string** (e.g., `ghp_...`). You will not see it again.

### Step 2: Configure Unity's UPM for Authentication

You must tell Unity's Package Manager how to authenticate with GitHub. The safest way is to store this token in a config file *outside* your project.

1.  Find your global UPM config file.
    * **Windows:** `%APPDATA%\Unity\upmconfig.toml`
    * **macOS / Linux:** `~/Library/Unity/upmconfig.toml` (or `~/.config/Unity/upmconfig.toml` on some Linux distros)
2.  If this file doesn't exist, create it.
3.  Add the following lines, replacing `YOUR_PAT_HERE` with the token you just copied:

    ```toml
    [npmAuth."[https://nuget.pkg.github.com](https://nuget.pkg.github.com)"]
    token = "YOUR_PAT_HERE"
    email = "your-email@example.com"
    alwaysAuth = true
    ```

### Step 3: Add the GitHub Package Registry to Your Project

Now, tell your *specific* Unity project where to find the package.

1.  Open your project's `Packages/manifest.json` file.
2.  Add the following `scopedRegistries` section to it.
3.  **Crucially, change `YOUR_GITHUB_USERNAME`** to your GitHub username or organization name (the owner of the repository).

    ```json
    {
      "dependencies": {
        "com.unity.modules.physics": "1.0.0",
        // ... your other dependencies
      },
      "scopedRegistries": [
        {
          "name": "GitHub Packages",
          "url": "[https://nuget.pkg.github.com/YOUR_GITHUB_USERNAME](https://nuget.pkg.github.com/YOUR_GITHUB_USERNAME)",
          "scopes": [
            "morpeh.boilerplate"
          ]
        }
      ]
    }
    ```
    * The `scopes` array tells Unity to look for any package starting with `morpeh.boilerplate` at this new URL.

### Step 4: Install the Package

1.  **Restart the Unity Editor** to make sure it loads the new `upmconfig.toml` and `manifest.json` settings.
2.  Go to **Window > Package Manager**.
3.  Click the `+` button in the top-left and select **"Add package by name..."**.
4.  Enter the `PackageId` from your `.csproj`:
    `Morpeh.Boilerplate.SourceGenerator`
5.  Click **"Add"**.

The Package Manager will now authenticate with GitHub, download your package, and install it. The source generator will be active immediately.

## Managing Versions

To publish a new version:
1.  Change the `<Version>1.0.1</Version>` tag in `Morpeh.BoilerplateGenerationProcessor.csproj`.
2.  Commit and push this change to the `main` branch.
3.  The GitHub Action will automatically build and publish version `1.0.1`.
4.  You can now update the package in the Unity Package Manager.

## Usage Rules
(Keep the rest of your README the same: Usage Rules, Limitations, Diagnostics...)