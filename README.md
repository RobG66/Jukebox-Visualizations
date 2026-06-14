# Jukebox-Visualizations

**Jukebox-Visualizations** is a fully self-contained Avalonia UI control library that provides high-performance, OpenGL-accelerated audio visualizations via the renowned [libprojectM](https://github.com/projectM-visualizer/projectm) engine. 

It provides an out-of-the-box, plug-and-play solution for integrating stunning, music-reactive visuals into any Avalonia application. Included in this repository is the complete suite of native C++ dependencies and a massive library of over 9,400+ hand-crafted `.milk` presets and textures, ensuring you have an endless variety of dynamic visuals instantly available.

---

## ⚙️ How It Works

This project seamlessly bridges unmanaged native code with managed C# UI elements, operating across two primary layers:

1. **Native Interop (`ProjectMNative.cs`)**  
   A comprehensive P/Invoke wrapper that securely interfaces with the C-based `libprojectM.dll` API. It is responsible for managing the unmanaged lifecycle (creating/destroying instances), handling rendering contexts, loading `.milk` presets, and feeding raw PCM audio data directly into the visualization engine's beat detection algorithms.

2. **Avalonia Control (`ProjectMControl.cs`)**  
   A custom Avalonia `OpenGlControlBase` component that drops directly into your UI. It takes complete ownership of acquiring an OpenGL hardware-accelerated surface via `Silk.NET.OpenGL` and delegates the actual drawing instructions directly to the unmanaged ProjectM engine, compositing the result beautifully into the Avalonia rendering pipeline.

---

## 🛠️ Dependencies

This library targets **.NET 10.0** and relies on the following core frameworks:
* [Avalonia UI](https://avaloniaui.net/) (v12.x) for the cross-platform rendering engine.
* [Silk.NET.OpenGL](https://github.com/dotnet/Silk.NET) (v2.x) for hardware-accelerated OpenGL context bindings.

*Note: The native `libprojectM` binaries and its required `ProjectM/` preset assets are bundled and automatically copied to the build output directory via `JukeboxVisualizations.csproj`.*

---

## 🚀 How to Use in Your Project

Adding rich, interactive music visualizations to your own Avalonia application is incredibly straightforward.

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
To make the visualizer react to music, you must feed it raw PCM audio samples (e.g., from an audio player like LibVLCSharp, NAudio, or Bass.NET). The control exposes a simple `AddAudioFloat` method that you can call inside your audio playback loop:

```csharp
// Example using LibVLCSharp audio callbacks or a standard audio buffer loop
public void OnAudioFrameReceived(float[] pcmSamples, uint sampleCount)
{
    // Pass the raw 32-bit float audio samples to the visualizer
    // projectM handles the beat detection and FFT analysis automatically!
    MyVisualizer.AddAudioFloat(pcmSamples, sampleCount, ProjectMChannels.Mono);
}
```

### 5. Control the Flow (C#)
You can command the visualizer to load new presets randomly or sequentially:

```csharp
// Automatically load a random .milk preset from the bundled ProjectM/ presets folder
MyVisualizer.LoadRandomPreset();

// Or load a specific preset file
MyVisualizer.LoadPreset(@"ProjectM\presets\Some Awesome Preset.milk");
```
