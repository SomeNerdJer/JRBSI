using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace JRBSI.Models;

public enum InstallStatus
{
    Pending,
    Installing,
    Installed,
    Failed,
    AlreadyInstalled
}

public enum InstallMethod
{
    EmbeddedExe,
    Winget
}

public sealed class InstallItem : INotifyPropertyChanged
{
    private InstallStatus _status = InstallStatus.Pending;
    private bool _isIndeterminate;
    private double _progress;
    private string _detailMessage = string.Empty;

    public required string Name { get; init; }
    public required InstallMethod Method { get; init; }
    public string? EmbeddedResourceName { get; init; }
    public string? ExeFileName { get; init; }
    public string? Arguments { get; init; }
    public string? WingetPackageId { get; init; }
    public string? WingetPackageName { get; init; }

    public InstallStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusBrush));
            UpdateProgressForStatus();
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        set
        {
            if (_isIndeterminate == value)
            {
                return;
            }

            _isIndeterminate = value;
            OnPropertyChanged();
        }
    }

    public double Progress
    {
        get => _progress;
        set
        {
            if (Math.Abs(_progress - value) < 0.001)
            {
                return;
            }

            _progress = value;
            OnPropertyChanged();
        }
    }

    public string DetailMessage
    {
        get => _detailMessage;
        set
        {
            if (_detailMessage == value)
            {
                return;
            }

            _detailMessage = value;
            OnPropertyChanged();
        }
    }

    public string StatusText => Status switch
    {
        InstallStatus.Pending => "Pending",
        InstallStatus.Installing => "Installing...",
        InstallStatus.Installed => "Installed",
        InstallStatus.Failed => "Failed",
        InstallStatus.AlreadyInstalled => "Already Installed",
        _ => "Unknown"
    };

    public Brush StatusBrush => Status switch
    {
        InstallStatus.Pending => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
        InstallStatus.Installing => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
        InstallStatus.Installed => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
        InstallStatus.Failed => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
        InstallStatus.AlreadyInstalled => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
        _ => new SolidColorBrush(Colors.Gray)
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void UpdateProgressForStatus()
    {
        switch (Status)
        {
            case InstallStatus.Pending:
                Progress = 0;
                IsIndeterminate = false;
                break;
            case InstallStatus.Installing:
                Progress = 0;
                IsIndeterminate = true;
                break;
            case InstallStatus.Installed:
            case InstallStatus.AlreadyInstalled:
                Progress = 100;
                IsIndeterminate = false;
                break;
            case InstallStatus.Failed:
                Progress = 100;
                IsIndeterminate = false;
                break;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
