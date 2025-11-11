using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using MaterialDesignThemes.Wpf;
using PyExecutor.Settings;

namespace PyExecutor;

[ComponentInfo(
        "C49A9149-0E2D-4B85-9BA3-96C861999F50",
        "PyExecutor",
        PackIconKind.CodeTags,
        "运行自定义 Python 代码并显示输出。"
    )]
public partial class HitokotoControl : ComponentBase<PythonComponentSettings>
{
    private readonly DispatcherTimer _refreshTimer = new();
    private readonly SemaphoreSlim _executionLock = new(1, 1);

    public HitokotoControl()
    {
        InitializeComponent();
        _refreshTimer.Tick += RefreshTimerOnTick;
        Loaded += HitokotoControl_Loaded;
        Unloaded += HitokotoControl_Unloaded;
    }

    private void HitokotoControl_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HitokotoControl_Loaded;
        PythonComponentInstanceRegistry.Register(this);
        AttachSettingsListener();
        EnsureSettingsDefaults();
        ApplyTimerSettings();
        _ = RunPythonScriptAsync();
    }

    private void HitokotoControl_Unloaded(object sender, RoutedEventArgs e)
    {
        Loaded += HitokotoControl_Loaded;
        if (Settings != null)
        {
            Settings.PropertyChanged -= SettingsOnPropertyChanged;
        }

        _refreshTimer.Stop();
    }

    private void AttachSettingsListener()
    {
        if (Settings == null)
        {
            return;
        }

        Settings.PropertyChanged += SettingsOnPropertyChanged;
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PythonComponentSettings.RefreshIntervalSeconds)
            or nameof(PythonComponentSettings.AutoRefreshEnabled))
        {
            ApplyTimerSettings();
        }

        if (e.PropertyName is nameof(PythonComponentSettings.Script)
            or nameof(PythonComponentSettings.PythonExecutable))
        {
            _ = RunPythonScriptAsync();
        }
    }

    private void RefreshTimerOnTick(object? sender, EventArgs e)
    {
        _ = RunPythonScriptAsync();
    }

    private void ApplyTimerSettings()
    {
        if (Settings == null)
        {
            return;
        }

        var intervalSeconds = Math.Max(1, Settings.RefreshIntervalSeconds);
        _refreshTimer.Interval = TimeSpan.FromSeconds(intervalSeconds);

        if (Settings.AutoRefreshEnabled)
        {
            if (!_refreshTimer.IsEnabled)
            {
                _refreshTimer.Start();
            }
        }
        else
        {
            _refreshTimer.Stop();
        }
    }

    private void EnsureSettingsDefaults()
    {
        if (Settings == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.Script))
        {
            Settings.Script = PythonComponentSettings.DefaultScript;
        }

        if (string.IsNullOrWhiteSpace(Settings.PythonExecutable))
        {
            Settings.PythonExecutable = PythonComponentSettings.DefaultPythonExecutable;
        }

        if (Settings.RefreshIntervalSeconds <= 0)
        {
            Settings.RefreshIntervalSeconds = PythonComponentSettings.DefaultRefreshIntervalSeconds;
        }
    }

    private async Task RunPythonScriptAsync()
    {
        if (Settings == null)
        {
            return;
        }

        if (!_executionLock.Wait(0))
        {
            return;
        }

        try
        {
            var interpreter = string.IsNullOrWhiteSpace(Settings.PythonExecutable)
                ? PythonComponentSettings.DefaultPythonExecutable
                : Settings.PythonExecutable;

            var script = string.IsNullOrWhiteSpace(Settings.Script)
                ? PythonComponentSettings.DefaultScript
                : Settings.Script;

            var result = await PythonScriptRunner.RunAsync(script, interpreter);
            ApplyExternalRunResult(result);
        }
        catch (Exception ex)
        {
            UpdateText("Error");
        }
        finally
        {
            _executionLock.Release();
        }
    }

    internal void ApplyExternalRunResult(PythonScriptResult result)
    {
        if (result.Success)
        {
            UpdateText(result.Message);
        }
        else
        {
            UpdateText("Error");
        }
    }

    private void UpdateText(string text)
    {
        Dispatcher.Invoke(() => HitokotoText.Text = text);
    }

    private async void RunNowMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        await RunPythonScriptAsync();
    }
}
