using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using DailyCheck.Models;

namespace DailyCheck.Services;

/// <summary>本地 JSON 读写。数据放在 exe 同级的 data 目录，方便整体备份</summary>
public class DataService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // 让中文按原样写入，不转成 \uXXXX
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // 兼容老版本把月/周目标存成纯字符串的格式
        Converters = { new PeriodGoalConverter() },
    };

    public string DataDir { get; }
    public string DataFile { get; }

    public DataService()
    {
        DataDir = Path.Combine(AppContext.BaseDirectory, "data");
        DataFile = Path.Combine(DataDir, "dailycheck.json");
    }

    public AppData Load()
    {
        if (File.Exists(DataFile))
        {
            try
            {
                var json = File.ReadAllText(DataFile);
                var data = JsonSerializer.Deserialize<AppData>(json, Options);
                if (data != null)
                {
                    Normalize(data);
                    return data;
                }
            }
            catch (Exception ex)
            {
                // 数据损坏时先备份再重来，避免直接丢失
                try { File.Copy(DataFile, DataFile + ".bad", true); } catch { /* 备份失败就算了 */ }
                MessageBox.Show(
                    $"数据文件读取失败，原文件已备份为 dailycheck.json.bad。\n\n{ex.Message}",
                    "每日打卡", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        return new AppData();
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(DataDir);
        var json = JsonSerializer.Serialize(data, Options);
        // 先写临时文件再替换，避免写一半断电把数据写坏
        var tmp = DataFile + ".tmp";
        File.WriteAllText(tmp, json);
        File.Copy(tmp, DataFile, true);
        File.Delete(tmp);
    }

    /// <summary>加载后校准字段、补全老数据缺的东西</summary>
    private static void Normalize(AppData data)
    {
        var today = DateTime.Today;

        // 第一次使用的日期：老数据没有，用能找到的最早记录兜底
        if (string.IsNullOrEmpty(data.FirstUseDate))
        {
            var earliest = data.Days.Keys
                .Where(k => !string.IsNullOrEmpty(k))
                .OrderBy(k => k, StringComparer.Ordinal)
                .FirstOrDefault();
            data.FirstUseDate = earliest ?? today.ToString(Stats.DateFormat);
        }

        // 字典 key 才是权威，把 Date 字段对齐
        foreach (var kv in data.Days)
            kv.Value.Date = kv.Key;

        // 每日任务补上统计起点。不补的话，老任务会把建立之前的日子
        // 全部算成「没打卡」，完成率被无端拉低。
        var todayKey = today.ToString(Stats.DateFormat);
        FillStartDates(data.Overall, todayKey);
        foreach (var g in data.MonthGoals.Values) FillStartDates(g, todayKey);
        foreach (var g in data.WeekGoals.Values) FillStartDates(g, todayKey);
        foreach (var d in data.Days.Values) FillStartDates(d, todayKey);
    }

    private static void FillStartDates(GoalContainer container, string todayKey)
    {
        foreach (var t in container.Tasks)
        {
            if (t.Daily && string.IsNullOrEmpty(t.StartDate))
                t.StartDate = todayKey;
        }
        foreach (var c in container.Children)
            FillStartDates(c, todayKey);
    }
}
