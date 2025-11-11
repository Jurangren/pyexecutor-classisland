using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PyExecutor.Settings;

/// <summary>
/// Stores user-configurable options for the Hitokoto component.
/// </summary>
public class PythonComponentSettings : INotifyPropertyChanged
{
    public const string DefaultPythonExecutable = "python";
    public const int DefaultRefreshIntervalSeconds = 5;

    public const string DefaultScript = """
def main():
    return "hello world!"
""";

    private string _pythonExecutable = DefaultPythonExecutable;
    private string _script = DefaultScript;
    private bool _autoRefreshEnabled = false;
    private int _refreshIntervalSeconds = DefaultRefreshIntervalSeconds;

    public string PythonExecutable
    {
        get => _pythonExecutable;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? DefaultPythonExecutable : value;
            if (newValue == _pythonExecutable)
            {
                return;
            }

            _pythonExecutable = newValue;
            OnPropertyChanged();
        }
    }

    public string Script
    {
        get => _script;
        set
        {
            var newValue = string.IsNullOrWhiteSpace(value) ? DefaultScript : value;
            if (newValue == _script)
            {
                return;
            }

            _script = newValue;
            OnPropertyChanged();
        }
    }

    public bool AutoRefreshEnabled
    {
        get => _autoRefreshEnabled;
        set
        {
            if (value == _autoRefreshEnabled)
            {
                return;
            }

            _autoRefreshEnabled = value;
            OnPropertyChanged();
        }
    }

    public int RefreshIntervalSeconds
    {
        get => _refreshIntervalSeconds;
        set
        {
            var normalized = value <= 0 ? DefaultRefreshIntervalSeconds : value;
            if (normalized == _refreshIntervalSeconds)
            {
                return;
            }

            _refreshIntervalSeconds = normalized;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
