#if BETA
using Microsoft.Win32;

namespace HomeCamMonitor;

// Window icons are a separate source from the package's qualified PNG assets.
// Keep the actual HWND transparent too, including after Explorer/Windows restarts.
internal sealed class WindowIconTheme : IDisposable
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, WindowIconTheme> bindings = new();
    internal static WindowIconTheme Bind(Form form)
    {
        var binding = bindings.GetValue(form, window => new WindowIconTheme(window, manageTaskbar: window is MonitorForm));
        binding.Refresh();
        return binding;
    }

    private static readonly Icon white = LogoVariants.LoadIcon(1);
    private static readonly Icon black = LogoVariants.LoadIcon(2);
    private readonly Form form;
    private readonly Func<bool> usesLightTaskbar;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1000 };
    private bool disposed;
    private readonly string? appId;
    private string? taskbarIconPath;
    private bool? appliedLight;
    private bool propertiesAssigned;
    private readonly TaskbarIconProperties.Lifetime propertyLifetime = new();

    internal WindowIconTheme(Form form, Func<bool>? usesLightTaskbar = null, string? appId = null, bool manageTaskbar = true, string? taskbarIconPath = null)
    {
        this.form = form;
        this.usesLightTaskbar = usesLightTaskbar ?? ReadLightTaskbar;
        this.appId = manageTaskbar ? appId ?? TaskbarIconProperties.CurrentAppId() : null;
        this.taskbarIconPath = taskbarIconPath;
        form.HandleCreated += OnHandleCreated;
        form.HandleDestroyed += OnHandleDestroyed;
        Refresh();
        // A UI timer also catches theme changes missed while the app was hidden
        // or the Windows shell was restarting. No static event retains the form.
        timer.Tick += (_, _) => Refresh();
        form.Disposed += OnFormDisposed;
        timer.Start();
    }

    internal static bool ReadLightTaskbar()
    {
        if (SystemInformation.HighContrast)
            return SystemColors.Window.GetBrightness() > 0.5f;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
        }
        catch (System.Security.SecurityException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (IOException) { return false; }
    }

    internal void Refresh()
    {
        if (disposed || form.IsDisposed) return;
        var light = usesLightTaskbar();
        var icon = light ? black : white;
        if (!ReferenceEquals(form.Icon, icon)) form.Icon = icon;
        if (appId is not null && form.IsHandleCreated)
        {
            if (propertyLifetime.Handle != form.Handle) propertyLifetime.AssignHandle(form.Handle);
            var path = Path.Combine(AppContext.BaseDirectory, light ? "HomeCam-Schwarz-transparent.ico" : "HomeCam-Weiss-transparent.ico");
            if (appliedLight != light || !propertiesAssigned || !File.Exists(taskbarIconPath))
            {
                try
                {
                    var cachePath = taskbarIconPath ??= TaskbarIconCache.DefaultPath;
                    var index = TaskbarIconCache.Write(path, cachePath);
                    if (!propertiesAssigned)
                    {
                        TaskbarIconProperties.Apply(form.Handle, appId, cachePath);
                        propertiesAssigned = true;
                    }
                    TaskbarIconCache.Refresh(cachePath, index);
                    appliedLight = light;
                }
                catch (System.Runtime.InteropServices.COMException) { /* Retry on the next UI tick. */ }
                catch (IOException) { /* Retry if Explorer briefly holds the icon file. */ }
                catch (UnauthorizedAccessException) { /* Retry without interrupting camera playback. */ }
            }
        }
    }

    private void OnHandleCreated(object? sender, EventArgs e) => Refresh();
    private void OnHandleDestroyed(object? sender, EventArgs e)
    {
        propertiesAssigned = false;
        propertyLifetime.ReleaseHandle();
    }
    private void OnFormDisposed(object? sender, EventArgs e) => Dispose();
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (propertyLifetime.Handle != IntPtr.Zero)
        {
            try { TaskbarIconProperties.Clear(propertyLifetime.Handle); }
            catch (System.Runtime.InteropServices.COMException) { }
            propertyLifetime.ReleaseHandle();
        }
        timer.Dispose();
        form.HandleCreated -= OnHandleCreated;
        form.HandleDestroyed -= OnHandleDestroyed;
        form.Disposed -= OnFormDisposed;
    }
}
#endif
