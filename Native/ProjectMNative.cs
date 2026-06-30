using System;
using System.IO;
using System.Runtime.InteropServices;

namespace JukeboxVisualizations.Native;

public enum ProjectMChannels
{
    Mono = 1,
    Stereo = 2
}

public static class ProjectMNative
{
    private static IntPtr _libraryHandle = IntPtr.Zero;
    private static IntPtr _glewHandle = IntPtr.Zero;

    static ProjectMNative()
    {
        try
        {
            string libraryPath = GetLibraryPath();
            if (File.Exists(libraryPath))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string dir = Path.GetDirectoryName(libraryPath) ?? "";
                    string glewPath = Path.Combine(dir, "glew32.dll");
                    if (File.Exists(glewPath))
                    {
                        try
                        {
                            _glewHandle = NativeLibrary.Load(glewPath, typeof(ProjectMNative).Assembly, DllImportSearchPath.SafeDirectories);
                            if (NativeLibrary.TryGetExport(_glewHandle, "glewExperimental", out IntPtr glewExpAddr))
                            {
                                Marshal.WriteByte(glewExpAddr, 1);
                                Console.WriteLine("[ProjectM] Set glewExperimental = true successfully.");
                            }
                            if (NativeLibrary.TryGetExport(_glewHandle, "glewInit", out IntPtr glewInitAddr))
                            {
                                glewInit = Marshal.GetDelegateForFunctionPointer<glewInit_delegate>(glewInitAddr);
                                Console.WriteLine("[ProjectM] Loaded glewInit delegate successfully.");
                            }
                        }
                        catch (Exception glewEx)
                        {
                            Console.WriteLine($"[ProjectM] Failed to set glewExperimental: {glewEx.Message}");
                        }
                    }
                }

                // Crucial: we MUST tell the loader to look in the same directory as the DLL for dependencies!
                // (e.g. libprojectM.dll needs glew32.dll on Windows — both sit in lib/ together.)
                _libraryHandle = NativeLibrary.Load(libraryPath, typeof(ProjectMNative).Assembly, DllImportSearchPath.UseDllDirectoryForDependencies | DllImportSearchPath.SafeDirectories);
                LoadDelegates();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load libprojectM: {ex}");
        }
    }

    /// <summary>
    /// Resolves the path to the native libprojectM binary.
    ///
    /// <para>
    /// Loaded from <c>&lt;appdir&gt;/lib/</c> — flat layout, no per-platform
    /// subfolders. Windows <c>.dll</c> and Linux <c>.so</c> files coexist by
    /// extension; we pick the right filename per OS at runtime.
    /// </para>
    /// </summary>
    private static string GetLibraryPath()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib");

        string fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "libprojectM.dll"
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? "libprojectM.dylib"
                : "libprojectM.so.4";

        return Path.Combine(dir, fileName);
    }

    private static T GetDelegate<T>(string name, bool throwOnError = true) where T : Delegate
    {
        if (_libraryHandle == IntPtr.Zero) return null!;
        if (NativeLibrary.TryGetExport(_libraryHandle, name, out IntPtr address))
        {
            try
            {
                return Marshal.GetDelegateForFunctionPointer<T>(address);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ProjectM] ERROR parsing delegate {name}: {ex}");
                return null!;
            }
        }
        Console.WriteLine($"[ProjectM] TryGetExport failed for {name}");
        if (throwOnError)
            throw new Exception($"Export {name} not found in libprojectM.");
        return null!;
    }

    private static void LoadDelegates()
    {
        projectm_create = GetDelegate<projectm_create_delegate>("projectm_create", false);
        projectm_destroy = GetDelegate<projectm_destroy_delegate>("projectm_destroy", false);
        projectm_set_window_size = GetDelegate<projectm_set_window_size_delegate>("projectm_set_window_size", false);
        projectm_set_fps = GetDelegate<projectm_set_fps_delegate>("projectm_set_fps", false);
        projectm_pcm_add_float = GetDelegate<projectm_pcm_add_float_delegate>("projectm_pcm_add_float", false);
        projectm_pcm_add_int16 = GetDelegate<projectm_pcm_add_int16_delegate>("projectm_pcm_add_int16", false);
        projectm_opengl_render_frame = GetDelegate<projectm_opengl_render_frame_delegate>("projectm_opengl_render_frame", false);
        projectm_set_texture_search_paths = GetDelegate<projectm_set_texture_search_paths_delegate>("projectm_set_texture_search_paths", false);
        projectm_set_log_callback = GetDelegate<projectm_set_log_callback_delegate>("projectm_set_log_callback", false);
        projectm_set_log_level = GetDelegate<projectm_set_log_level_delegate>("projectm_set_log_level", false);
        projectm_load_preset_file = GetDelegate<projectm_load_preset_file_delegate>("projectm_load_preset_file", false);
        projectm_load_preset_data = GetDelegate<projectm_load_preset_data_delegate>("projectm_load_preset_data", false);
        projectm_write_debug_image_on_next_frame = GetDelegate<projectm_write_debug_image_on_next_frame_delegate>("projectm_write_debug_image_on_next_frame", false);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr projectm_create_delegate();
    public static projectm_create_delegate projectm_create { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_destroy_delegate(IntPtr instance);
    public static projectm_destroy_delegate projectm_destroy { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_set_window_size_delegate(IntPtr handle, nuint width, nuint height);
    public static projectm_set_window_size_delegate projectm_set_window_size { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_set_texture_search_paths_delegate(
        IntPtr handle,
        IntPtr[] paths,
        nuint count);
    public static projectm_set_texture_search_paths_delegate projectm_set_texture_search_paths { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int glewInit_delegate();
    public static glewInit_delegate? glewInit { get; private set; }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_log_callback(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string message,
        int level,
        IntPtr user_data);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_set_log_callback_delegate(
        projectm_log_callback callback,
        [MarshalAs(UnmanagedType.I1)] bool unused,
        IntPtr user_data);
    public static projectm_set_log_callback_delegate projectm_set_log_callback { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_set_log_level_delegate(int level, [MarshalAs(UnmanagedType.I1)] bool unused);
    public static projectm_set_log_level_delegate projectm_set_log_level { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_set_fps_delegate(IntPtr instance, int fps);
    public static projectm_set_fps_delegate projectm_set_fps { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_pcm_add_float_delegate(IntPtr instance, float[] samples, uint count, ProjectMChannels channels);
    public static projectm_pcm_add_float_delegate projectm_pcm_add_float { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_pcm_add_int16_delegate(IntPtr instance, short[] samples, uint count, ProjectMChannels channels);
    public static projectm_pcm_add_int16_delegate projectm_pcm_add_int16 { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_opengl_render_frame_delegate(IntPtr instance);
    public static projectm_opengl_render_frame_delegate projectm_opengl_render_frame { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int projectm_load_preset_file_delegate(IntPtr handle, [MarshalAs(UnmanagedType.LPUTF8Str)] string filename, [MarshalAs(UnmanagedType.I1)] bool smooth_transition);
    public static projectm_load_preset_file_delegate projectm_load_preset_file { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_load_preset_data_delegate(IntPtr handle, IntPtr data, nuint length, byte smooth_transition);
    public static projectm_load_preset_data_delegate projectm_load_preset_data { get; private set; } = null!;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void projectm_write_debug_image_on_next_frame_delegate(IntPtr handle);
    public static projectm_write_debug_image_on_next_frame_delegate? projectm_write_debug_image_on_next_frame { get; private set; }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void BindFramebufferDelegate(uint target, uint framebuffer);

    public static BindFramebufferDelegate? OriginalBindFramebuffer;
    private static readonly BindFramebufferDelegate _hookedBindFramebufferDelegate = HookedBindFramebuffer;
    public static int TargetFbo = 0;
    private static bool _hooksInstalled = false;

    private static void HookedBindFramebuffer(uint target, uint framebuffer)
    {
        if (framebuffer == 0 && TargetFbo != 0)
        {
            framebuffer = (uint)TargetFbo;
        }
        OriginalBindFramebuffer?.Invoke(target, framebuffer);
    }

    public static void InstallGlewHooks()
    {
        if (_hooksInstalled || _glewHandle == IntPtr.Zero) return;
        try
        {
            if (NativeLibrary.TryGetExport(_glewHandle, "__glewBindFramebuffer", out IntPtr addr))
            {
                IntPtr originalFuncPtr = Marshal.ReadIntPtr(addr);
                if (originalFuncPtr != IntPtr.Zero)
                {
                    OriginalBindFramebuffer = Marshal.GetDelegateForFunctionPointer<BindFramebufferDelegate>(originalFuncPtr);
                    IntPtr hookedFuncPtr = Marshal.GetFunctionPointerForDelegate(_hookedBindFramebufferDelegate);
                    Marshal.WriteIntPtr(addr, hookedFuncPtr);
                    Console.WriteLine("[ProjectM] Hooked __glewBindFramebuffer successfully.");
                    _hooksInstalled = true;
                }
                else
                {
                    Console.WriteLine("[ProjectM] __glewBindFramebuffer function pointer is currently NULL (glewInit not run yet?)");
                }
            }
            else
            {
                Console.WriteLine("[ProjectM] __glewBindFramebuffer export not found in glew32.dll");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ProjectM] Failed to install GLEW hooks: {ex.Message}");
        }
    }
}
