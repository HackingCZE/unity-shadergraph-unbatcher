# ShaderGraph Unbatcher for Unity

A lightweight editor utility to export Unity Shader Graph files as standalone shaders with **SRP Batcher disabled**.

## Why?

The **SRP Batcher** is great for performance, but sometimes you _need_ to break it. By default, Shader Graph forces shaders to be SRP Batcher compatible. This tool is useful when:

- You need to use **GPU Instancing** on shaders that SRP Batcher would otherwise handle differently.
- You are implementing custom **low-level rendering hacks** that require individual constant buffer updates.
- You need to force **Draw Call separation** for specific materials.

## Features

- ⚡ **One-Click Export**: Adds a button directly to the Shader Graph inspector.
- 🛠️ **Automatic Code Injection**: Injects `DisableBatching` tags and breaks the `UnityPerMaterial` buffer automatically.
- 🔍 **Safe Reflection**: Accesses internal Unity Shader Graph generator without modifying your project's installation.
- 📂 **Non-Destructive**: Generates a new `.shader` file next to your graph, leaving the original intact.

## Installation

### Via Git URL

1. Open the **Package Manager** in Unity (`Window` > `Package Manager`).
2. Click the `+` button in the top left corner.
3. Select **Add package from git URL...**.
4. Paste the following URL:
   `https://github.com/HackingCZE/unity-shadergraph-unbatcher.git`

## How to Use

1. Select any `.shadergraph` asset in your **Project** window.
2. In the **Inspector**, you will see a new section called **Tools**.
3. Click the **Export without SRP_Batcher** button.
4. A new file named `YourShader_NoBatching.shader` will be generated in the same folder.
5. Use this generated shader in your materials.

## Technical Details

The tool performs the following modifications during export:

1. **Tags**: Adds `"DisableBatching" = "True"` to the shader tags.
2. **CBuffer Break**: Injects a dummy property `_SBPBreaker` into the `Properties` and `HLSLPROGRAM` sections.
3. **Calculation Interference**: Subtly injects the breaker variable into the color output calculation to ensure the SRP Batcher cannot group the materials.
4. **Renaming**: Automatically renames the shader path to `Shader "Exported/DisabledBatching_..."` for easy identification in the shader selection menu.

## Compatibility

- **Unity**: 2021.3 LTS or newer.
- **Render Pipelines**: URP & HDRP.
- **Dependencies**: Requires the `Shader Graph` package to be installed in your project.

## License

This project is licensed under the **MIT License** - see the LICENSE file for details.

---

_Created with ❤️ for the Unity Community._
