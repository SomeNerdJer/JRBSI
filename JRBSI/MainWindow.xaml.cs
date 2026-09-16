using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using JRBSI.Models;
using JRBSI.Services;

namespace JRBSI;

public partial class MainWindow : Window
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private readonly InstallOrchestrator _orchestrator = new();
    private readonly ObservableCollection<InstallItem> _installItems = new();
    private bool _isInstalling;

    public MainWindow()
    {
        InitializeComponent();

        foreach (var item in _orchestrator.CreateInstallItems())
        {
            _installItems.Add(item);
        }

        PackagesItemsControl.ItemsSource = _installItems;
        SourceInitialized += (_, _) => ApplyTitleBarTheme();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        InstallButton.IsEnabled = false;

        await Task.Run(() => _orchestrator.ScanInstalledPackages(
            _installItems,
            action => Dispatcher.Invoke(action)));

        InstallButton.IsEnabled = true;
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        App.SetDarkTheme(!App.IsDarkTheme);
        UpdateThemeToggleVisuals();
        ApplyTitleBarTheme();
    }

    private void UpdateThemeToggleVisuals()
    {
        var dark = App.IsDarkTheme;
        SunIcon.Visibility = dark ? Visibility.Visible : Visibility.Collapsed;
        MoonIcon.Visibility = dark ? Visibility.Collapsed : Visibility.Visible;
        ThemeToggleButton.ToolTip = dark ? "Switch to light mode" : "Switch to dark mode";
    }

    private void ApplyTitleBarTheme()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var useDark = App.IsDarkTheme ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isInstalling)
        {
            return;
        }

        _isInstalling = true;
        InstallButton.IsEnabled = false;

        try
        {
            await _orchestrator.RunInstallAsync(
                _installItems,
                uiInvoker: action => Dispatcher.Invoke(action));

            MessageBox.Show(
                $"Don't forget to install WPILib{Environment.NewLine}{Environment.NewLine}{InstallOrchestrator.WpilibUrl}",
                "Don't forget WPILib",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Process.Start(new ProcessStartInfo(InstallOrchestrator.WpilibUrl)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Installation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isInstalling = false;
            InstallButton.IsEnabled = true;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
