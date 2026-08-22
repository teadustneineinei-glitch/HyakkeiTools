using Hyakkei.Core;

namespace Hyakkei.Tool.AutoClicker;

/// <summary>连点器设置（持久化到配置 Tools.auto-clicker 节）。</summary>
public sealed class ClickerSettings
{
    /// <summary>MouseLeft | MouseRight | Key</summary>
    public string Target { get; set; } = "MouseLeft";

    public int KeyVk { get; set; } = 0x20; // Space

    public string KeyName { get; set; } = "Space";

    /// <summary>Click（连点）| Hold（长按）</summary>
    public string Mode { get; set; } = "Click";

    public int IntervalMs { get; set; } = 100;

    public ClickerSettings Clone() => new()
    {
        Target = Target,
        KeyVk = KeyVk,
        KeyName = KeyName,
        Mode = Mode,
        IntervalMs = IntervalMs,
    };
}

/// <summary>点击/按键引擎：连点走后台线程定时发送，长按 = 按下不放直到停止。</summary>
public sealed class ClickerEngine
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private ClickerSettings? _active;

    public bool IsRunning { get; private set; }

    /// <summary>运行状态变化（可能在任意线程触发，UI 侧自行调度）。</summary>
    public event Action<bool>? RunningChanged;

    public void Toggle(ClickerSettings settings)
    {
        if (IsRunning) Stop();
        else Start(settings);
    }

    public void Start(ClickerSettings settings)
    {
        lock (_gate)
        {
            if (IsRunning) return;
            _active = settings.Clone();
            _active.IntervalMs = Math.Max(10, _active.IntervalMs); // 下限保护
            IsRunning = true;

            if (_active.Mode == "Hold")
            {
                Press(_active, down: true);
            }
            else
            {
                _cts = new CancellationTokenSource();
                var token = _cts.Token;
                var cfg = _active;
                Task.Run(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        Fire(cfg);
                        Thread.Sleep(cfg.IntervalMs);
                    }
                });
            }
        }
        Log.Info($"连点器开始：{_active!.Target} {_active.Mode} {_active.IntervalMs}ms");
        RunningChanged?.Invoke(true);
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!IsRunning) return;
            _cts?.Cancel();
            _cts = null;
            if (_active?.Mode == "Hold")
                Press(_active, down: false); // 释放按住的键/鼠标
            _active = null;
            IsRunning = false;
        }
        Log.Info("连点器停止");
        RunningChanged?.Invoke(false);
    }

    private static void Fire(ClickerSettings s)
    {
        if (s.Target == "Key")
            InputSimulator.KeyPress((ushort)s.KeyVk);
        else
            InputSimulator.MouseClick(s.Target == "MouseRight" ? SimMouseButton.Right : SimMouseButton.Left);
    }

    private static void Press(ClickerSettings s, bool down)
    {
        if (s.Target == "Key")
        {
            if (down) InputSimulator.KeyDown((ushort)s.KeyVk);
            else InputSimulator.KeyUp((ushort)s.KeyVk);
        }
        else
        {
            var b = s.Target == "MouseRight" ? SimMouseButton.Right : SimMouseButton.Left;
            if (down) InputSimulator.MouseDown(b);
            else InputSimulator.MouseUp(b);
        }
    }
}
