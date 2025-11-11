using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Shared;

namespace PyExecutor.Settings;

public partial class PythonComponentSettingsControl : ComponentBase<PythonComponentSettings>, INotifyPropertyChanged
{
    private readonly IComponentsService _componentsService;
    private readonly DispatcherTimer _saveDebounceTimer;
    private string _testStatus = string.Empty;

    public PythonComponentSettingsControl()
    {
        _componentsService = IAppHost.GetService<IComponentsService>()
                                ?? throw new InvalidOperationException("无法获取组件服务");

        _saveDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(600)
        };
        _saveDebounceTimer.Tick += SaveDebounceTimerOnTick;

        InitializeComponent();
        Loaded += ControlOnLoaded;
        Unloaded += ControlOnUnloaded;
    }

    private void ControlOnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ControlOnLoaded;
        AttachSettingsListeners();
    }

    private void ControlOnUnloaded(object sender, RoutedEventArgs e)
    {
        if (Settings != null)
        {
            Settings.PropertyChanged -= SettingsOnPropertyChanged;
        }

        _saveDebounceTimer.Stop();
    }

    private void AttachSettingsListeners()
    {
        if (Settings == null)
        {
            return;
        }

        Settings.PropertyChanged += SettingsOnPropertyChanged;
    }

    private void SettingsOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PythonComponentSettings.Script)
            or nameof(PythonComponentSettings.PythonExecutable)
            or nameof(PythonComponentSettings.AutoRefreshEnabled)
            or nameof(PythonComponentSettings.RefreshIntervalSeconds))
        {
            ScheduleConfigSave();
        }
    }

    private void ScheduleConfigSave()
    {
        _saveDebounceTimer.Stop();
        _saveDebounceTimer.Start();
    }

    private void SaveDebounceTimerOnTick(object? sender, EventArgs e)
    {
        _saveDebounceTimer.Stop();
        _componentsService.SaveConfig();
    }

    private async void RunNowButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Settings == null)
        {
            TestStatus = "设置尚未加载";
            return;
        }

        TestStatus = "正在执行脚本…";
        var interpreter = string.IsNullOrWhiteSpace(Settings.PythonExecutable)
            ? PythonComponentSettings.DefaultPythonExecutable
            : Settings.PythonExecutable;

        var result = await PythonScriptRunner.RunAsync(Settings.Script, interpreter);

        TestStatus = result.Success
            ? $"执行成功：{result.Message}"
            : $"{result.Message} —— {result.Details}";

        PropagateRunResult(result);
    }

    private void PropagateRunResult(PythonScriptResult result)
    {
        foreach (var control in PythonComponentInstanceRegistry.GetControls(Settings))
        {
            control.ApplyExternalRunResult(result);
        }
    }

    public string TestStatus
    {
        get => _testStatus;
        set
        {
            if (value == _testStatus)
            {
                return;
            }

            _testStatus = value;
            OnPropertyChanged(nameof(TestStatus));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
