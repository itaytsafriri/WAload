using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace WAload;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static Mutex? _mutex = null;
    private const string AppName = "WAload_SingleInstance";

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private const int SW_RESTORE = 9;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Try to create a named mutex
        _mutex = new Mutex(true, AppName, out bool createdNew);

        if (!createdNew)
        {
            // Another instance is already running
            ShowSingleInstanceMessage();
            BringExistingInstanceToFront();
            // Exit this instance
            Current.Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Close();
        base.OnExit(e);
    }

    private static void ShowSingleInstanceMessage()
    {
        try
        {
            System.Windows.MessageBox.Show(
                "WhatsUPload is already running!\n\nOnly one instance of WhatsUPload is allowed at a time.\nThe existing window will be brought to the front.",
                "WhatsUPload - Already Running",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            // Log the exception if needed, but don't crash
            Debug.WriteLine($"Error showing single instance message: {ex.Message}");
        }
    }

    private static void BringExistingInstanceToFront()
    {
        try
        {
            // Find the existing WAload process
            Process current = Process.GetCurrentProcess();
            foreach (Process process in Process.GetProcessesByName(current.ProcessName))
            {
                // Ignore the current process
                if (process.Id != current.Id && process.MainWindowHandle != IntPtr.Zero)
                {
                    IntPtr hWnd = process.MainWindowHandle;
                    
                    // If the window is minimized, restore it
                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                    }
                    
                    // Bring the window to the foreground
                    SetForegroundWindow(hWnd);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            // Log the exception if needed, but don't crash
            Debug.WriteLine($"Error bringing existing instance to front: {ex.Message}");
        }
    }
}

