# Jukebox-Visualizations

An Avalonia UI control library providing OpenGL-accelerated audio visualizations via libprojectM. 

This repository was extracted to keep complex graphical rendering and unmanaged code isolated from standard application logic. It serves as a modular plugin that can be referenced by the `Jukebox` or any other Avalonia application requiring high-performance audio visualizations.

## Architecture

The project consists of two primary layers:

1. **Native Interop (`ProjectMNative.cs`):** Contains the P/Invoke signatures required to interface directly with the `libprojectM` C API. It handles creating instances, managing rendering contexts, and passing audio pulse data to the visualizer engine.
2. **Avalonia Control (`ProjectMControl.cs`):** An Avalonia `OpenGlControlBase` implementation that provides a managed wrapper around the native visualizer. It handles the OpenGL lifecycle, context acquisition via Silk.NET, and rendering the visualizer frame directly onto the Avalonia composition surface.

## Dependencies

* .NET 10.0
* Avalonia UI (v12.x)
* Silk.NET.OpenGL (v2.x)
* Requires native `libprojectM` binaries to be available in the execution directory at runtime.

## Usage

Reference the compiled `JukeboxVisualizations.dll` in your Avalonia project, ensure your `AppBuilder` is configured to support the necessary rendering modes, and include the control in your XAML:

```xml
xmlns:vis="clr-namespace:JukeboxVisualizations.Controls;assembly=JukeboxVisualizations"

<vis:ProjectMControl Name="VisualizerControl" />
```
