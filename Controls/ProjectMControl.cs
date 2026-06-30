using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using JukeboxVisualizations.Native;
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using static Avalonia.OpenGL.GlConsts;

namespace JukeboxVisualizations.Controls;

public class ProjectMControl : OpenGlControlBase
{
    #region Fields & Constants
    private IntPtr _projectMHandle;
    private readonly ConcurrentQueue<short[]> _pcmQueue = new();
    private readonly ConcurrentQueue<string> _presetQueue = new();
    private long _frameCount;
    private int _lastWidth;
    private int _lastHeight;
    private string _lastLog = "";
    private readonly ProjectMNative.projectm_log_callback _logCallback;
    private bool _engineRequested;
    #endregion

    #region Public Properties
    public bool IsHandleValid => _projectMHandle != IntPtr.Zero;
    #endregion

    #region Constructor
    public ProjectMControl()
    {
        _logCallback = (string message, int level, IntPtr userData) =>
        {
            if (level <= 2)
            {
                _lastLog = message;
                Console.WriteLine($"[ProjectM NATIVE] {message}");
            }
        };
    }
    #endregion

    #region Public Methods
    public void StartEngine()
    {
        _engineRequested = true;
    }

    public void FeedPcm(short[] samples)
    {
        _pcmQueue.Enqueue(samples);
        while (_pcmQueue.Count > 10)
        {
            _pcmQueue.TryDequeue(out _);
        }
    }

    public void LoadPreset(string path, bool smooth = true)
    {
        _presetQueue.Enqueue(path);
        while (_presetQueue.Count > 5)
        {
            _presetQueue.TryDequeue(out _);
        }
    }
    #endregion

    #region Protected Methods
    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        RequestNextFrameRendering();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        try
        {
            var renderScaling = Avalonia.Controls.TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
            int width = (int)Math.Max(1, Bounds.Width * renderScaling);
            int height = (int)Math.Max(1, Bounds.Height * renderScaling);

            gl.BindFramebuffer(GL_FRAMEBUFFER, fb);
            gl.Viewport(0, 0, width, height);
            gl.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            gl.Clear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);

            if (!_engineRequested)
            {
                RequestNextFrameRendering();
                return;
            }

            if (_projectMHandle == IntPtr.Zero)
            {
                InitializeProjectM();
                if (_projectMHandle == IntPtr.Zero)
                {
                    RequestNextFrameRendering();
                    return;
                }
            }

            if (_lastWidth != width || _lastHeight != height)
            {
                _lastWidth = width;
                _lastHeight = height;
                ProjectMNative.projectm_set_window_size(_projectMHandle, (nuint)width, (nuint)height);
            }

            while (_presetQueue.TryDequeue(out var presetPath))
            {
                if (ProjectMNative.projectm_load_preset_file != null)
                {
                    var normalizedPath = presetPath.Replace('\\', '/');
                    int loadRes = ProjectMNative.projectm_load_preset_file(_projectMHandle, normalizedPath, false);
                    if (loadRes != 1)
                    {
                        Console.WriteLine($"[ProjectM] ERROR: projectm_load_preset_file failed (returned {loadRes}) for: {normalizedPath}");
                    }
                }
                else
                {
                    Console.WriteLine($"[ProjectM] ERROR: load_preset_file delegate is NULL!");
                }
            }

            while (_pcmQueue.TryDequeue(out var samples))
            {
                ProjectMNative.projectm_pcm_add_int16(_projectMHandle, samples, (uint)samples.Length / 2, ProjectMChannels.Stereo);
            }

            ProjectMNative.TargetFbo = fb;
            ProjectMNative.projectm_opengl_render_frame(_projectMHandle);
            ProjectMNative.TargetFbo = 0;
            gl.BindFramebuffer(GL_FRAMEBUFFER, fb);

            _frameCount++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ProjectM Render Error: {ex}");
        }

        RequestNextFrameRendering();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        if (_projectMHandle != IntPtr.Zero)
        {
            ProjectMNative.projectm_destroy(_projectMHandle);
            _projectMHandle = IntPtr.Zero;
        }
        base.OnOpenGlDeinit(gl);
    }
    #endregion

    #region Private Methods
    private void InitializeProjectM()
    {
        try
        {
            if (ProjectMNative.projectm_set_log_level != null)
            {
                ProjectMNative.projectm_set_log_level(4, false);
                ProjectMNative.projectm_set_log_callback(_logCallback, false, IntPtr.Zero);
            }

            if (ProjectMNative.glewInit != null)
            {
                int glewRes = ProjectMNative.glewInit();
                if (glewRes != 0)
                {
                    Console.WriteLine($"[ProjectM] glewInit() failed with code: {glewRes}");
                }
                ProjectMNative.InstallGlewHooks();
            }

            if (ProjectMNative.projectm_create != null)
            {
                _projectMHandle = ProjectMNative.projectm_create();
                if (_projectMHandle == IntPtr.Zero)
                {
                    Console.WriteLine("[ProjectM] ERROR: projectm_create() returned NULL handle");
                }
                else
                {
                    ProjectMNative.projectm_set_fps(_projectMHandle, 60);
                    ProjectMNative.projectm_set_window_size(_projectMHandle, (nuint)Math.Max(1, Bounds.Width), (nuint)Math.Max(1, Bounds.Height));

                    if (ProjectMNative.projectm_set_texture_search_paths != null)
                    {
                        var texturesPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ProjectM", "textures");
                        if (System.IO.Directory.Exists(texturesPath))
                        {
                            IntPtr ptr = Marshal.StringToHGlobalAnsi(texturesPath);
                            ProjectMNative.projectm_set_texture_search_paths(_projectMHandle, new IntPtr[] { ptr }, 1);
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ProjectM Init Error: {ex}");
        }
    }
    #endregion
}
