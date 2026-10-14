using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DailyCheck.Models;

/// <summary>属性变更通知基类，界面靠它自动刷新</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}

// ======================================================================
//  任务
// ======================================================================

/// <summary>一条任务。分两类：一次性任务（打勾即完成）和每日任务（每天重新打卡）</summary>
public class TaskItem : ObservableObject
{
    private string _text = "";
    private bool _done;

    public string Id { get; set; } = NewId();
    public string Text { get => _text; set => Set(ref _text, value); }

    /// <summary>一次性任务的完成状态</summary>
    public bool Done
    {
        get => _done;
        set
        {
            if (Set(ref _done, value)) OnPropertyChanged(nameof(Checked));
        }
    }

    /// <summary>是不是「每天都要做」的任务。是的话进度按打卡天数算，没打卡的天也算进分母</summary>
    public bool Daily { get; set; }

    /// <summary>每日任务的打卡记录，key = "yyyy-MM-dd"，值为 true 表示那天打了卡</summary>
    public Dictionary<string, bool> History { get; set; } = new();

    /// <summary>每日任务从哪天开始算。空则从周期起点算，用于旧数据兜底</summary>
    public string? StartDate { get; set; }

    /// <summary>界面绑定的勾选状态。每日任务自动落到「今天」这一格</summary>
    [JsonIgnore]
    public bool Checked
    {
        get => Daily ? History.ContainsKey(Stats.TodayKey) : Done;
        set
        {
            if (Daily)
            {
                if (value) History[Stats.TodayKey] = true;
                else History.Remove(Stats.TodayKey);
                OnPropertyChanged(nameof(Checked));
            }
            else
            {
                Done = value;
            }
        }
    }

    /// <summary>今天打卡了没有（每日任务）</summary>
    [JsonIgnore]
    public bool TodayChecked => Daily && History.ContainsKey(Stats.TodayKey);

    /// <summary>指定的那一天算不算完成（一次性任务看 Done，每日任务查打卡记录）</summary>
    public bool CheckedOn(DateTime date)
        => Daily ? History.ContainsKey(date.ToString(Stats.DateFormat)) : Done;

    /// <summary>界面上显示的打卡情况，如「本周 3/6 天」，每次刷新时重算</summary>
    [JsonIgnore]
    public string DailyCountText
    {
        get => _dailyCountText;
        set => Set(ref _dailyCountText, value);
    }
    private string _dailyCountText = "";

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}

// ======================================================================
//  目标容器：自己可以挂任务，也可以挂下级目标
// ======================================================================

/// <summary>目标容器。月/周/天/自定义子目标都用它，进度自下而上汇总</summary>
public abstract class GoalContainer : ObservableObject
{
    private string _title = "";
    private bool _expanded = true;

    /// <summary>目标文字。界面上就地编辑</summary>
    public string Title { get => _title; set => Set(ref _title, value); }

    public ObservableCollection<TaskItem> Tasks { get; set; } = new();
    public ObservableCollection<SubGoal> Children { get; set; } = new();

    /// <summary>界面上是否展开</summary>
    public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }

    /// <summary>递归统计本节点（含自己的任务和所有子孙节点）在 [from, to] 区间的完成情况</summary>
    public Stat Compute(DateTime from, DateTime to)
    {
        int done = 0, total = 0;
        foreach (var t in Tasks)
        {
            var s = Stats.CountTask(t, from, to);
            done += s.Done;
            total += s.Total;
        }
        foreach (var c in Children)
        {
            var s = c.Compute(from, to);
            done += s.Done;
            total += s.Total;
        }
        return new Stat(done, total);
    }

    /// <summary>给界面用的进度，刷新时统一重算</summary>
    [JsonIgnore] public double Percent { get; set; }
    [JsonIgnore] public string PercentText { get; set; } = "—";
    [JsonIgnore] public string SummaryText { get; set; } = "";

    /// <summary>重算并广播给界面（递归）</summary>
    public void ApplyStat(DateTime from, DateTime to)
    {
        var s = Compute(from, to);
        Percent = s.Percent;
        PercentText = s.PercentText;
        SummaryText = s.SummaryText;
        OnPropertyChanged(nameof(Percent));
        OnPropertyChanged(nameof(PercentText));
        OnPropertyChanged(nameof(SummaryText));

        foreach (var c in Children) c.ApplyStat(from, to);
    }
}

/// <summary>自定义子目标，可以无限往下嵌套</summary>
public class SubGoal : GoalContainer
{
    public string Id { get; set; } = TaskItem.NewId();
}

/// <summary>总目标，长期不变的那一条</summary>
public class OverallGoal : GoalContainer
{
}

/// <summary>一个周期（月或周）的目标</summary>
public class PeriodGoal : GoalContainer
{
}

/// <summary>某一天的目标与任务</summary>
public class DayRecord : GoalContainer
{
    public string Date { get; set; } = "";
}

// ======================================================================
//  倒数日
// ======================================================================

public class Countdown : ObservableObject
{
    private string _title = "";
    private DateTime _targetDate = DateTime.Today.AddDays(30);
    private bool _yearly;

    public string Id { get; set; } = TaskItem.NewId();
    public string Title { get => _title; set => Set(ref _title, value); }

    public DateTime TargetDate
    {
        get => _targetDate;
        set { if (Set(ref _targetDate, value)) NotifyDays(); }
    }

    /// <summary>是否按年重复（生日、纪念日等）</summary>
    public bool Yearly
    {
        get => _yearly;
        set { if (Set(ref _yearly, value)) NotifyDays(); }
    }

    [JsonIgnore]
    public int DaysLeft
    {
        get
        {
            var today = DateTime.Today;
            var target = TargetDate.Date;
            if (Yearly)
            {
                // 闰年 2/29 之类的日子夹到当月最后一天，否则构造会抛异常
                int day = Math.Min(target.Day, DateTime.DaysInMonth(today.Year, target.Month));
                var next = new DateTime(today.Year, target.Month, day);
                if (next < today) next = next.AddYears(1);
                target = next;
            }
            return (int)(target - today).TotalDays;
        }
    }

    [JsonIgnore]
    public string DaysLeftText => DaysLeft switch
    {
        > 0 => $"还有 {DaysLeft} 天",
        0 => "就是今天 🎯",
        _ => $"已过去 {-DaysLeft} 天"
    };

    private void NotifyDays()
    {
        OnPropertyChanged(nameof(DaysLeft));
        OnPropertyChanged(nameof(DaysLeftText));
    }
}

// ======================================================================
//  根数据
// ======================================================================

public class AppData
{
    public ObservableCollection<Countdown> Countdowns { get; set; } = new();

    /// <summary>总目标（一条，长期）</summary>
    public OverallGoal Overall { get; set; } = new();

    /// <summary>月目标，key = "yyyy-MM"</summary>
    public Dictionary<string, PeriodGoal> MonthGoals { get; set; } = new();

    /// <summary>周目标，key = 该周周一的 "yyyy-MM-dd"</summary>
    public Dictionary<string, PeriodGoal> WeekGoals { get; set; } = new();

    /// <summary>每日记录，key = "yyyy-MM-dd"</summary>
    public Dictionary<string, DayRecord> Days { get; set; } = new();

    /// <summary>第一次使用的日期，作为总目标的统计起点</summary>
    public string FirstUseDate { get; set; } = "";

    public bool AutoStart { get; set; } = true;
    public double WindowWidth { get; set; } = 540;
    public double WindowHeight { get; set; } = 840;
}

// ======================================================================
//  统计
// ======================================================================

/// <summary>一个区间的完成情况</summary>
public record Stat(int Done, int Total)
{
    public double Percent => Total == 0 ? 0 : Done * 100.0 / Total;
    public string PercentText => Total == 0 ? "—" : $"{Percent:F1}%";
    public string SummaryText => Total == 0 ? "还没有安排任务" : $"完成 {Done} / {Total}";
}

public static class Stats
{
    public const string DateFormat = "yyyy-MM-dd";

    private static readonly string[] WeekNames = { "周日", "周一", "周二", "周三", "周四", "周五", "周六" };

    public static string WeekName(DateTime d) => WeekNames[(int)d.DayOfWeek];

    /// <summary>今天的日期串，每日任务靠它定位</summary>
    public static string TodayKey => DateTime.Today.ToString(DateFormat);

    /// <summary>取某天所在周的周一</summary>
    public static DateTime WeekStart(DateTime d)
        => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// <summary>
    /// 统计单条任务在 [from, to] 区间的完成情况。
    /// 一次性任务永远是 1/1 或 0/1；
    /// 每日任务按天展开——区间里每过一天算一个「应完成」，打了卡才算「已完成」，
    /// 所以没打卡的天会拉低完成率，这正是「打卡天数」起作用的地方。
    /// </summary>
    public static Stat CountTask(TaskItem t, DateTime from, DateTime to)
    {
        if (!t.Daily) return new Stat(t.Done ? 1 : 0, 1);

        var start = from.Date;
        if (!string.IsNullOrEmpty(t.StartDate)
            && DateTime.TryParse(t.StartDate, out var sd)
            && sd.Date > start)
        {
            start = sd.Date;
        }
        if (start > to.Date) return new Stat(0, 0);

        int total = 0, done = 0;
        for (var d = start; d <= to.Date; d = d.AddDays(1))
        {
            total++;
            if (t.History.ContainsKey(d.ToString(DateFormat))) done++;
        }
        return new Stat(done, total);
    }

    /// <summary>汇总整个数据库中所有任务的完成情况（用于总览）</summary>
    public static Stat Aggregate(AppData data, DateTime from, DateTime to)
    {
        int done = 0, total = 0;
        foreach (var rec in data.Days.Values)
        {
            var s = rec.Compute(from, to);
            done += s.Done;
            total += s.Total;
        }
        return new Stat(done, total);
    }

    /// <summary>取或创建某天的记录</summary>
    public static DayRecord GetOrCreateDay(AppData data, DateTime date)
    {
        var key = date.ToString(DateFormat);
        if (!data.Days.TryGetValue(key, out var rec))
        {
            rec = new DayRecord { Date = key };
            data.Days[key] = rec;
        }
        rec.Date = key;
        return rec;
    }

    /// <summary>取或创建某个月的目标</summary>
    public static PeriodGoal GetOrCreateMonth(AppData data, DateTime date)
    {
        var key = date.ToString("yyyy-MM");
        if (!data.MonthGoals.TryGetValue(key, out var g))
        {
            g = new PeriodGoal();
            data.MonthGoals[key] = g;
        }
        return g;
    }

    /// <summary>取或创建某个周的目标</summary>
    public static PeriodGoal GetOrCreateWeek(AppData data, DateTime date)
    {
        var key = WeekStart(date).ToString(DateFormat);
        if (!data.WeekGoals.TryGetValue(key, out var g))
        {
            g = new PeriodGoal();
            data.WeekGoals[key] = g;
        }
        return g;
    }

    /// <summary>总目标的统计起点：第一次使用的日期</summary>
    public static DateTime OverallStart(AppData data)
    {
        if (DateTime.TryParse(data.FirstUseDate, out var d)) return d.Date;
        return DateTime.Today;
    }
}

// ======================================================================
//  JSON 兼容：老版本把月/周目标存成纯字符串，这里兼容读进来
// ======================================================================

public class PeriodGoalConverter : JsonConverter<PeriodGoal>
{
    public override PeriodGoal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // 旧格式：直接是一个字符串
        if (reader.TokenType == JsonTokenType.String)
            return new PeriodGoal { Title = reader.GetString() ?? "" };

        if (reader.TokenType == JsonTokenType.Null) return new PeriodGoal();

        var goal = new PeriodGoal();
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        if (root.TryGetProperty("Title", out var ti))
            goal.Title = ti.GetString() ?? "";
        if (root.TryGetProperty("Tasks", out var ta))
            goal.Tasks = ta.Deserialize<ObservableCollection<TaskItem>>(options) ?? new();
        if (root.TryGetProperty("Children", out var ch))
            goal.Children = ch.Deserialize<ObservableCollection<SubGoal>>(options) ?? new();

        return goal;
    }

    public override void Write(Utf8JsonWriter writer, PeriodGoal value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Title", value.Title);
        writer.WritePropertyName("Tasks");
        JsonSerializer.Serialize(writer, value.Tasks, options);
        writer.WritePropertyName("Children");
        JsonSerializer.Serialize(writer, value.Children, options);
        writer.WriteEndObject();
    }
}
