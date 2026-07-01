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

    // GL function delegates resolved via GetProcAddress — Avalonia's
    // GlInterface only exposes a minimal subset; we need these extras
    // to save/restore GL state around ProjectM rendering.
    private GlGetIntegervDelegate? _glGetIntegerv;
    private GlIsEnabledDelegate? _glIsEnabled;
    private GlEnableDelegate? _glEnable;
    private GlDisableDelegate? _glDisable;
    private GlBlendFuncDelegate? _glBlendFunc;
    private GlUseProgramDelegate? _glUseProgram;
    private GlActiveTextureDelegate? _glActiveTexture;
    private GlBindTextureDelegate? _glBindTexture;
    private GlBindBufferDelegate? _glBindBuffer;
    private GlBindVertexArrayDelegate? _glBindVertexArray;
    private GlScissorDelegate? _glScissor;
    private GlViewportDelegate? _glViewport;
    private bool _glFunctionsResolved;

    // GL constants not exposed by Avalonia's GlConsts
    private const int GL_BLEND = 0x0BE2;
    private const int GL_BLEND_SRC_RGB = 0x80C9;
    private const int GL_BLEND_DST_RGB = 0x80C8;
    private const int GL_BLEND_SRC_ALPHA = 0x80CB;
    private const int GL_BLEND_DST_ALPHA = 0x80CA;
    private const int GL_CURRENT_PROGRAM = 0x8B8D;
    private const int GL_ARRAY_BUFFER_BINDING = 0x8894;
    private const int GL_VERTEX_ARRAY_BINDING = 0x85B5;
    private const int GL_VIEWPORT = 0x0BA2;
    private const int GL_SCISSOR_BOX = 0x0C10;
    private const int GL_ARRAY_BUFFER = 0x8892;

    // Unmanaged delegate types for GL functions
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlGetIntegervDelegate(int pname, int[] data);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate byte GlIsEnabledDelegate(int cap);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlEnableDelegate(int cap);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlDisableDelegate(int cap);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlBlendFuncDelegate(int sfactor, int dfactor);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlUseProgramDelegate(int program);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlActiveTextureDelegate(int texture);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlBindTextureDelegate(int target, int texture);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlBindBufferDelegate(int target, int buffer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlBindVertexArrayDelegate(int array);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlScissorDelegate(int x, int y, int width, int height);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void GlViewportDelegate(int x, int y, int width, int height);
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
        ResolveGlFunctions(gl);
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

            // Save GL state before ProjectM renders — it modifies
            // shaders, textures, blend modes, depth test, viewport, etc.
            // without restoring them, which corrupts Avalonia's Skia GL
            // state and causes the driver to leak orphaned resources.
            SaveGlState(out var savedState);

            ProjectMNative.TargetFbo = fb;
            ProjectMNative.projectm_opengl_render_frame(_projectMHandle);
            ProjectMNative.TargetFbo = 0;

            // Restore GL state so Avalonia's compositor can safely
            // composite this FBO without resource leaks.
            RestoreGlState(gl, fb, savedState);

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
    private void ResolveGlFunctions(GlInterface gl)
    {
        try
        {
            _glGetIntegerv = ResolveDelegate<GlGetIntegervDelegate>(gl, "glGetIntegerv");
            _glIsEnabled = ResolveDelegate<GlIsEnabledDelegate>(gl, "glIsEnabled");
            _glEnable = ResolveDelegate<GlEnableDelegate>(gl, "glEnable");
            _glDisable = ResolveDelegate<GlDisableDelegate>(gl, "glDisable");
            _glBlendFunc = ResolveDelegate<GlBlendFuncDelegate>(gl, "glBlendFunc");
            _glUseProgram = ResolveDelegate<GlUseProgramDelegate>(gl, "glUseProgram");
            _glActiveTexture = ResolveDelegate<GlActiveTextureDelegate>(gl, "glActiveTexture");
            _glBindTexture = ResolveDelegate<GlBindTextureDelegate>(gl, "glBindTexture");
            _glBindBuffer = ResolveDelegate<GlBindBufferDelegate>(gl, "glBindBuffer");
            _glBindVertexArray = ResolveDelegate<GlBindVertexArrayDelegate>(gl, "glBindVertexArray");
            _glScissor = ResolveDelegate<GlScissorDelegate>(gl, "glScissor");
            _glViewport = ResolveDelegate<GlViewportDelegate>(gl, "glViewport");

            _glFunctionsResolved = _glGetIntegerv != null && _glIsEnabled != null
                && _glEnable != null && _glDisable != null;

            if (_glFunctionsResolved)
                Console.WriteLine("[ProjectM] GL state save/restore functions resolved successfully.");
            else
                Console.WriteLine("[ProjectM] WARNING: Some GL functions could not be resolved — state save/restore disabled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProjectM] Failed to resolve GL functions: {ex.Message}");
            _glFunctionsResolved = false;
        }
    }

    private static T? ResolveDelegate<T>(GlInterface gl, string name) where T : Delegate
    {
        var ptr = gl.GetProcAddress(name);
        if (ptr == IntPtr.Zero) return null;
        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    private struct GlSavedState
    {
        public int CurrentProgram;
        public int ActiveTexture;
        public int TextureBinding2D;
        public int ArrayBufferBinding;
        public int VertexArrayBinding;
        public bool BlendEnabled;
        public int BlendSrcRgb;
        public int BlendDstRgb;
        public bool DepthTestEnabled;
        public bool ScissorTestEnabled;
        public int[] Viewport;     // [x, y, w, h]
        public int[] ScissorBox;   // [x, y, w, h]
    }

    private void SaveGlState(out GlSavedState state)
    {
        state = default;
        if (!_glFunctionsResolved) return;

        var buf = new int[1];

        _glGetIntegerv!(GL_CURRENT_PROGRAM, buf);
        state.CurrentProgram = buf[0];

        _glGetIntegerv(GL_ACTIVE_TEXTURE, buf);
        state.ActiveTexture = buf[0];

        _glGetIntegerv(GL_TEXTURE_BINDING_2D, buf);
        state.TextureBinding2D = buf[0];

        _glGetIntegerv(GL_ARRAY_BUFFER_BINDING, buf);
        state.ArrayBufferBinding = buf[0];

        _glGetIntegerv(GL_VERTEX_ARRAY_BINDING, buf);
        state.VertexArrayBinding = buf[0];

        _glGetIntegerv(GL_BLEND_SRC_RGB, buf);
        state.BlendSrcRgb = buf[0];

        _glGetIntegerv(GL_BLEND_DST_RGB, buf);
        state.BlendDstRgb = buf[0];

        state.BlendEnabled = _glIsEnabled!(GL_BLEND) != 0;
        state.DepthTestEnabled = _glIsEnabled(GL_DEPTH_TEST) != 0;
        state.ScissorTestEnabled = _glIsEnabled(GL_SCISSOR_TEST) != 0;

        state.Viewport = new int[4];
        _glGetIntegerv(GL_VIEWPORT, state.Viewport);

        state.ScissorBox = new int[4];
        _glGetIntegerv(GL_SCISSOR_BOX, state.ScissorBox);
    }

    private void RestoreGlState(GlInterface gl, int fb, in GlSavedState state)
    {
        if (!_glFunctionsResolved) return;

        gl.BindFramebuffer(GL_FRAMEBUFFER, fb);

        _glUseProgram?.Invoke(state.CurrentProgram);
        _glActiveTexture?.Invoke(state.ActiveTexture);
        _glBindTexture?.Invoke(GL_TEXTURE_2D, state.TextureBinding2D);
        _glBindBuffer?.Invoke(GL_ARRAY_BUFFER, state.ArrayBufferBinding);
        _glBindVertexArray?.Invoke(state.VertexArrayBinding);

        if (state.BlendEnabled) _glEnable!(GL_BLEND); else _glDisable!(GL_BLEND);
        _glBlendFunc?.Invoke(state.BlendSrcRgb, state.BlendDstRgb);

        if (state.DepthTestEnabled) _glEnable!(GL_DEPTH_TEST); else _glDisable!(GL_DEPTH_TEST);
        if (state.ScissorTestEnabled) _glEnable!(GL_SCISSOR_TEST); else _glDisable!(GL_SCISSOR_TEST);

        _glViewport?.Invoke(state.Viewport[0], state.Viewport[1], state.Viewport[2], state.Viewport[3]);
        _glScissor?.Invoke(state.ScissorBox[0], state.ScissorBox[1], state.ScissorBox[2], state.ScissorBox[3]);
    }

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
