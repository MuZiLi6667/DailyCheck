using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DailyCheck.Models;
using DailyCheck.Services;

namespace DailyCheck;

public partial class MainWindow : Window
{
    private readonly DataService _svc = new();
    private AppData _data = new();
    private bool _ready;
    private string _currentDay = "";

    /// <summary>给递归的 GoalView 用：它要靠这份数据反查任务挂在哪个目标下</summary>
    public static AppData? CurrentData { get; private set; }

    /// <summary>每分钟看一眼日期有没有翻篇。程序开机自启后常驻，跨天要自动切到新的一天</summary>
    private readonly DispatcherTimer _dayWatcher = new() { Interval = TimeSpan.FromMinutes(1) };

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    // ==================== 生命周期 ====================

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _data = _svc.Load();
        CurrentData = _data;

        Width = Math.Max(MinWidth, _data.WindowWidth);
        Height = Math.Max(MinHeight, _data.WindowHeight);

        if (string.IsNullOrEmpty(_data.FirstUseDate)) _data.FirstUseDate = Stats.TodayKey;

        AutoStartCheck.IsChecked = _data.AutoStart;
        CountdownList.ItemsSource = _data.Countdowns;
        GoalView.DataChanged += OnGoalChanged;

        _currentDay = Stats.TodayKey;
        RefreshAll();
        _ready = true;

        // 把注册表里的自启项对齐到当前 exe 路径。
        // 程序目录搬过家、或者换了发布版本时，旧路径会失效，这里自动纠正。
        if (_data.AutoStart) AutoStartService.Set(true);

        _dayWatcher.Tick += (_, _) =>
        {
            if (_currentDay != Stats.TodayKey)
            {
                _currentDay = Stats.TodayKey;
                RefreshAll();
            }
        };
        _dayWatcher.Start();

        // 每次启动顺手刷新一次快照，这样在 Claude Code 里随时能读到最新数据
        try { new AiBridgeService(_svc).WriteSnapshot(_data); } catch { /* 写不了就算了 */ }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        GoalView.DataChanged -= OnGoalChanged;
        if (!_ready) return;

        _data.WindowWidth = Width;
        _data.WindowHeight = Height;
        try
        {
            _svc.Save(_data);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "每日打卡",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnGoalChanged()
    {
        RefreshStats();
        Save();
    }

    // ==================== 刷新 ====================

    private void RefreshAll()
    {
        var today = DateTime.Today;

        // 四个层级各自绑定到当前周期的节点
        OverallView.DataContext = _data.Overall;
        MonthView.DataContext = Stats.GetOrCreateMonth(_data, today);
        WeekView.DataContext = Stats.GetOrCreateWeek(_data, today);
        DayView.DataContext = Stats.GetOrCreateDay(_data, today);

        var wFrom = Stats.WeekStart(today);
        MonthHeader.Text = $"📅 本月 · {today:yyyy年M月}";
        WeekHeader.Text = $"🗓 本周 · {wFrom:M月d日} ~ {wFrom.AddDays(6):M月d日}";
        DayHeader.Text = $"☀ 今天 · {today:yyyy年M月d日} {Stats.WeekName(today)}";

        NoCountdownHint.Visibility = _data.Countdowns.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;

        RefreshStats();
    }

    /// <summary>
    /// 重算四层的进度。注意统计区间的差别——这就是「天数」参与计算的地方：
    /// 月目标从本月 1 号算到今天、周目标从本周一算到今天，
    /// 区间里每过一天，「每日」任务就多一份「应完成」，没打卡就会拉低完成率。
    /// </summary>
    private void RefreshStats()
    {
        var today = DateTime.Today;

        _data.Overall.ApplyStat(Stats.OverallStart(_data), today);
        Stats.GetOrCreateMonth(_data, today).ApplyStat(new DateTime(today.Year, today.Month, 1), today);
        Stats.GetOrCreateWeek(_data, today).ApplyStat(Stats.WeekStart(today), today);
        Stats.GetOrCreateDay(_data, today).ApplyStat(today, today);

        RefreshDailyPanel();
    }

    /// <summary>「今日必做」区：收集上层目标里所有每日任务，今天统一打勾</summary>
    private void RefreshDailyPanel()
    {
        var today = DateTime.Today;
        var wFrom = Stats.WeekStart(today);

        var list = new List<TaskItem>();
        CollectDaily(_data.Overall, list);
        CollectDaily(Stats.GetOrCreateMonth(_data, today), list);
        CollectDaily(Stats.GetOrCreateWeek(_data, today), list);

        foreach (var t in list)
        {
            var s = Stats.CountTask(t, wFrom, today);
            t.DailyCountText = s.Total == 0 ? "" : $"本周 {s.Done}/{s.Total} 天";
        }

        DailyList.ItemsSource = list;
        NoDailyHint.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void CollectDaily(GoalContainer container, List<TaskItem> list)
    {
        foreach (var t in container.Tasks)
            if (t.Daily) list.Add(t);
        foreach (var child in container.Children)
            CollectDaily(child, list);
    }

    private void Daily_Click(object sender, RoutedEventArgs e) => RefreshStats();

    // ==================== 底部按钮 ====================

    private void EditCountdown_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_data) { Owner = this };
        if (win.ShowDialog() == true)
        {
            CountdownList.ItemsSource = null;
            CountdownList.ItemsSource = _data.Countdowns;
            NoCountdownHint.Visibility = _data.Countdowns.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
            RefreshStats();
            Save();
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(_svc.DataDir);
            System.Diagnostics.Process.Start("explorer.exe", _svc.DataDir);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打不开目录：{ex.Message}", "每日打卡",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Ai_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        var ai = new AiBridgeService(_svc);

        // 已经有上次的建议，先让用户看，看完可以选择重新分析
        var existing = ai.ReadAdvice();
        if (existing is { } prev)
        {
            var view = new AdviceWindow(prev.Text, $"生成于 {prev.Time} · 点「重新分析」可重新生成") { Owner = this };
            view.ShowDialog();
            if (!view.WantReanalyze) return;
        }

        var confirm = MessageBox.Show(
            "将通过命令行调用 Claude 分析你的打卡数据。\n\n" +
            "· 大约需要 30 到 90 秒\n" +
            "· 会消耗你的 Claude Code 额度\n" +
            "· 数据只在本机处理\n\n是否继续？",
            "AI 分析", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        btn.IsEnabled = false;
        btn.Content = "分析中…";
        Cursor = Cursors.Wait;

        try
        {
            // 顺手把快照写到 data 目录，用户也可以在 Claude Code 里直接让我读这个文件
            ai.WriteSnapshot(_data);
            var prompt = ai.BuildPrompt(ai.BuildSnapshot(_data));

            var result = await AiBridgeService.AskClaudeAsync(prompt);
            ai.WriteAdvice(result);

            var win = new AdviceWindow(result, $"刚刚生成 · {DateTime.Now:yyyy-MM-dd HH:mm}") { Owner = this };
            win.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"分析失败：\n\n{ex.Message}", "AI 分析",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            btn.IsEnabled = true;
            btn.Content = "AI 分析";
            Cursor = Cursors.Arrow;
        }
    }

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var want = AutoStartCheck.IsChecked == true;
        _data.AutoStart = want;

        if (!AutoStartService.Set(want))
        {
            MessageBox.Show("设置开机自启失败，可能是注册表被安全软件拦截。", "每日打卡",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Save();
    }

    private void Save()
    {
        _data.WindowWidth = Width;
        _data.WindowHeight = Height;
        try { _svc.Save(_data); }
        catch { /* 保存失败不打断使用，关窗口时还会再存一次 */ }
    }
}
