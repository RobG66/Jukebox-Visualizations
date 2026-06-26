# Jukebox-Visualizations

**Jukebox-Visualizations** is a fully self-contained Avalonia UI control library that provides high-performance, OpenGL-accelerated audio visualizations via the [libprojectM](https://github.com/projectM-visualizer/projectm) engine.

It provides an out-of-the-box, plug-and-play solution for integrating stunning, music-reactive visuals into any Avalonia application. Included in this repository is the complete suite of native dependencies and a massive library of over 9,400+ `.milk` presets and textures, ensuring you have an endless variety of dynamic visuals instantly available.

> **Note:** As of the latest refactor, the Jukebox application no longer holds a compile-time reference to this assembly. Instead, the compiled `JukeboxVisualizations.dll` and its native dependencies are bundled as a drop-in alongside the Jukebox's other native runtimes. See the [Build](#-build) section below.

---

## ⚙️ How It Works

This project seamlessly bridges unmanaged native code with managed C# UI elements, operating across two primary layers:

1. **Native Interop (`Native/ProjectMNative.cs`)** — A comprehensive P/Invoke wrapper that securely interfaces with the C-based `libprojectM` API. It is responsible for managing the unmanaged lifecycle (creating/destroying instances), handling rendering contexts, loading `.milk` presets, and feeding raw PCM audio data directly into the visualization engine's beat detection algorithms. The native binary is loaded from `<appdir>/lib/` — flat, alongside all other native runtimes in the Jukebox deployment.

2. **Avalonia Control (`Controls/ProjectMControl.cs`)** — A custom Avalonia `OpenGlControlBase` component that drops directly into your UI. It takes complete ownership of acquiring an OpenGL hardware-accelerated surface via `Silk.NET.OpenGL` and delegates the actual drawing instructions directly to the unmanaged ProjectM engine, compositing the result beautifully into the Avalonia rendering pipeline.

---

## 🛠️ Dependencies

This library targets **.NET 10.0** and relies on the following core frameworks:
* **Avalonia UI** (v12.x) for the cross-platform rendering engine.
* **Silk.NET.OpenGL** (v2.x) for hardware-accelerated OpenGL context bindings.

The native `libprojectM` binaries (and the **GLEW** extension wrangler `glew32.dll` on Windows) are NOT shipped in this repo. They go in the `lib/` folder alongside the Jukebox's other native runtimes. See `lib/README.md` for the list of required files per platform.

---

## 📁 Project Layout

```
<jbvis-root>/
├── JukeboxVisualizations.csproj
├── Controls/
│   └── ProjectMControl.cs          # Avalonia OpenGlControlBase wrapper
├── Native/
│   └── ProjectMNative.cs           # P/Invoke declarations for libprojectM
├── lib/                            # ← native runtime drop-in (empty in repo)
│   ├── README.md                   # lists required files per platform
│   └── .gitignore                  # ignores binaries, keeps README
├── ProjectM/                       # ← preset data shipped with the repo
│   ├── Presets/
│   │   └── (... 9,400+ .milk files)
│   └── textures/
├── natives.json                    # manifest of native binary URLs + SHA-256s
├── fetch-natives.ps1               # Windows: download natives into lib/
├── fetch-natives.sh                # Linux/macOS: same
├── build.ps1                       # Windows build script (calls fetch-natives)
├── build.sh                        # Linux/macOS build script (calls fetch-natives)
├── THIRD_PARTY_LICENSES.md         # licensing for libprojectM, GLEW, etc.
├── .github/workflows/
│   └── build-natives.yml           # CI: builds libprojectM from source → GitHub release
└── README.md                       # this file
```

The `lib/` folder is intentionally empty — third-party native binaries are not shipped. Populate it by running the fetch-natives script (see [Build](#-build) below).

---

## 📥 Fetch native dependencies

The `lib/` folder must be populated with `libprojectM.dll` / `libprojectM.so.4` (+ `glew32.dll` on Windows) before the build can produce a working drop-in zip. Run:

```bash
# Windows
.\fetch-natives.ps1

# Linux / macOS
./fetch-natives.sh
```

The script reads `natives.json` (the manifest of URLs + SHA-256 checksums), downloads each asset for the current platform, verifies the checksum, and extracts into `lib/`. It's idempotent — safe to re-run; pass `-Force` / `--force` to re-download everything.

The native binaries are built from upstream projectM source by the `build-natives.yml` CI workflow (see [.github/workflows/build-natives.yml](.github/workflows/build-natives.yml)). To update libprojectM, edit `PROJECTM_REF` in that workflow, trigger a rebuild, then bump the URL + SHA-256 in `natives.json`. See [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md) for licensing.

---

## 🔨 Build

The repository ships two equivalent build scripts:

| Script | Platform | Description |
|--------|----------|-------------|
| `build.ps1` | Windows | PowerShell script — builds for win-x64 and linux-x64, stages into `publish/stage/`, zips for distribution. |
| `build.sh`  | Linux / macOS | Bash equivalent of `build.ps1`. |

Both scripts produce the same drop-in layout at `./publish/stage/`:

```text
publish/stage/
├── lib/                                ← ALL drop-in files, flat
│   ├── JukeboxVisualizations.dll       ← managed wrapper
│   ├── JukeboxVisualizations.deps.json ← dependency manifest for Assembly.LoadFrom
│   ├── libprojectM.dll                 (Windows — from source lib/)
│   ├── libprojectM.so.4                (Linux   — from source lib/)
│   └── glew32.dll                      (Windows — from source lib/)
└── ProjectM/                           ← preset data only
    ├── Presets/
    └── textures/
```

A `Jukebox-Visualizations-dropin.zip` is also produced for distribution.

### Build steps (Windows)

```powershell
.\build.ps1
```

### Build steps (Linux / macOS)

```bash
./build.sh
```

### Cross-platform note

The managed `JukeboxVisualizations.dll` is pure IL — identical for every platform. Only the native binaries differ. The build script runs `dotnet publish` for both `win-x64` and `linux-x64` RIDs (so the RID-specific `.deps.json` is generated), but only the managed DLL from one of them is staged — the native binaries are copied verbatim from the source `lib/` folder.

### How to install the drop-in

1. Run the build script — produces `publish/Jukebox-Visualizations-dropin.zip`.
2. Unzip the archive into your Jukebox build output directory (next to `Jukebox.exe`).
3. Also drop Jukebox's own native runtimes (`bass.dll`, `libmpv-2.dll` on Windows; `libbass.so`, `libmpv.so.2` on Linux) into the same `lib/` folder.
4. Restart Jukebox — the visualizer button appears in the transport bar.

The final runtime layout in the Jukebox directory will be:

```text
<appdir>/
├── Jukebox.exe
├── lib/                               ← ALL drop-in files, flat
│   ├── bass.dll                       (Jukebox's — Windows)
│   ├── libbass.so                     (Jukebox's — Linux)
│   ├── libmpv-2.dll                   (Jukebox's — Windows)
│   ├── libmpv.so.2                    (Jukebox's — Linux)
│   ├── JukeboxVisualizations.dll      (this repo — managed wrapper)
│   ├── JukeboxVisualizations.deps.json
│   ├── libprojectM.dll                (this repo — Windows)
│   ├── libprojectM.so.4               (this repo — Linux)
│   └── glew32.dll                     (this repo — Windows only)
└── ProjectM/                          ← preset data only
    ├── Presets/
    └── textures/
```

---

## 🚀 How to Use in Your Own Project

If you want to use `ProjectMControl` directly in your own Avalonia app (not via the Jukebox's reflection-based discovery):

### 1. Reference the Library
Add `JukeboxVisualizations` to your solution, or directly reference the compiled `JukeboxVisualizations.dll` in your project dependencies.

### 2. Add the XAML Namespace
At the top of your Avalonia Window or UserControl (`.axaml` file), include the visualizations namespace:

```xml
xmlns:vis="clr-namespace:JukeboxVisualizations.Controls;assembly=JukeboxVisualizations"
```

### 3. Drop in the Control
Place the `ProjectMControl` anywhere in your layout. It will automatically resize to fill its parent container:

```xml
<Grid Background="Black">
    <vis:ProjectMControl Name="MyVisualizer" />
</Grid>
```

### 4. Feed it Audio Data (C#)
Feed it raw PCM audio samples. The control exposes a `FeedPcm` method that accepts `short[]` mono or stereo samples:

```csharp
// Example: feed short[] PCM samples (mono interleaved, or stereo interleaved)
MyVisualizer.FeedPcm(pcmSamples);
```

### 5. Control the Flow (C#)
Load specific presets via `LoadPreset`:

```csharp
// Load a specific .milk preset file
MyVisualizer.LoadPreset(@"ProjectM\Presets\Some Awesome Preset.milk");
```

---

## 🔗 External Links & Requirements

* **[Avalonia UI](https://avaloniaui.net/)**: The cross-platform UI framework powering the visualizer control.
* **[Silk.NET](https://github.com/dotnet/Silk.NET)**: Provides the high-speed C# OpenGL bindings.
* **[ProjectM](https://github.com/projectM-visualizer/projectm)**: The native open-source music visualizer engine.
* **[GLEW](https://glew.sourceforge.net/)**: The OpenGL Extension Wrangler Library (Windows-only; lives next to `libprojectM.dll` in `lib/`).
