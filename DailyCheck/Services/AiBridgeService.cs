using System.Diagnostics;
using System.IO;
using System.Text;
using DailyCheck.Models;

namespace DailyCheck.Services;

/// <summary>
/// 与 Claude 的连接桥。
/// 两条路：1) 把打卡数据导出成 ai-snapshot.md，用户在 Claude Code 里让 Claude 分析；
/// 2) 直接调用本机已安装的 claude 命令行做无头分析。
/// </summary>
public class AiBridgeService
{
    private readonly DataService _svc;

    public AiBridgeService(DataService svc) => _svc = svc;

    public string SnapshotFile => Path.Combine(_svc.DataDir, "ai-snapshot.md");
    public string AdviceFile => Path.Combine(_svc.DataDir, "ai-advice.md");

    // ==================== 快照 ====================

    /// <summary>把当前数据整理成便于 Claude 阅读的 Markdown</summary>
    public string BuildSnapshot(AppData data)
    {
        var sb = new StringBuilder();
        var today = DateTime.Today;

        sb.AppendLine("# 打卡数据快照");
        sb.AppendLine();
        sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        // ---- 倒数日 ----
        sb.AppendLine("## 倒数日");
        if (data.Countdowns.Count == 0)
        {
            sb.AppendLine("（未设置）");
        }
        else
        {
            sb.AppendLine("| 目标 | 日期 | 剩余 |");
            sb.AppendLine("|---|---|---|");
            foreach (var c in data.Countdowns)
            {
                var repeat = c.Yearly ? "（每年重复）" : "";
                sb.AppendLine($"| {c.Title} | {c.TargetDate:yyyy-MM-dd}{repeat} | {c.DaysLeftText} |");
            }
        }
        sb.AppendLine();

        // ---- 四层目标 ----
        var mFrom = new DateTime(today.Year, today.Month, 1);
        var wFrom = Stats.WeekStart(today);

        data.MonthGoals.TryGetValue(today.ToString("yyyy-MM"), out var monthGoal);
        data.WeekGoals.TryGetValue(wFrom.ToString(Stats.DateFormat), out var weekGoal);
        data.Days.TryGetValue(Stats.TodayKey, out var dayRecord);

        var overallStart = Stats.OverallStart(data);
        AppendGoal(sb, $"总目标（从 {overallStart:yyyy-MM-dd} 起）", data.Overall, overallStart, today);
        AppendGoal(sb, $"本月目标（{today:yyyy年M月}，统计 {mFrom:MM-dd} 至 {today:MM-dd}）", monthGoal, mFrom, today);
        AppendGoal(sb, $"本周目标（{wFrom:MM-dd} 至 {wFrom.AddDays(6):MM-dd}，统计到 {today:MM-dd}）", weekGoal, wFrom, today);
        AppendGoal(sb, $"今天（{today:yyyy-MM-dd} {Stats.WeekName(today)}）", dayRecord, today, today);

        // ---- 最近 14 天回顾 ----
        sb.AppendLine("## 最近 14 天回顾");
        var any = false;
        for (var i = 14; i >= 1; i--)
        {
            var d = today.AddDays(-i);
            if (!data.Days.TryGetValue(d.ToString(Stats.DateFormat), out var rec)) continue;

            var s = rec.Compute(d, d);
            if (s.Total == 0) continue;

            any = true;
            sb.AppendLine($"### {d:MM-dd} {Stats.WeekName(d)} — {s.PercentText}，完成 {s.Done}/{s.Total}");
            foreach (var t in rec.Tasks)
                sb.AppendLine($"  - [{(t.CheckedOn(d) ? "x" : " ")}] {t.Text}");
        }
        if (!any) sb.AppendLine("（近两周没有记录）");
        sb.AppendLine();

        // ---- 累计 ----
        var days = (today - overallStart).Days + 1;
        var overall = data.Overall.Compute(overallStart, today);
        sb.AppendLine("## 累计");
        sb.AppendLine($"- 开始使用：{overallStart:yyyy-MM-dd}，已是第 {days} 天");
        sb.AppendLine($"- 总目标进度：{overall.PercentText}，完成 {overall.Done} / {overall.Total}");

        return sb.ToString();
    }

    /// <summary>递归输出一个目标节点（含它的任务和子目标）</summary>
    private static void AppendGoal(StringBuilder sb, string header, GoalContainer? goal, DateTime from, DateTime to, int depth = 0)
    {
        var pad = new string(' ', depth);
        sb.AppendLine($"{pad}### {header}");

        if (goal == null)
        {
            sb.AppendLine($"{pad}- （未设置）");
            sb.AppendLine();
            return;
        }

        var s = goal.Compute(from, to);
        sb.AppendLine($"{pad}- 目标：{(string.IsNullOrWhiteSpace(goal.Title) ? "（未填写）" : goal.Title)}");
        sb.AppendLine($"{pad}- 进度：{s.PercentText}，完成 {s.Done} / {s.Total}");

        foreach (var t in goal.Tasks)
        {
            string mark;
            if (t.Daily)
            {
                var ts = Stats.CountTask(t, from, to);
                mark = $"每日任务，区间内打卡 {ts.Done}/{ts.Total} 天";
            }
            else
            {
                mark = t.Done ? "已完成" : "未完成";
            }
            sb.AppendLine($"{pad}  - [{mark}] {t.Text}");
        }

        foreach (var child in goal.Children)
            AppendGoal(sb, $"子目标：{child.Title}", child, from, to, depth + 2);

        sb.AppendLine();
    }

    public void WriteSnapshot(AppData data)
    {
        Directory.CreateDirectory(_svc.DataDir);
        // 不传 Encoding：.NET 默认写 UTF-8 且不带 BOM，文件更干净
        File.WriteAllText(SnapshotFile, BuildSnapshot(data));
    }

    /// <summary>组装给 Claude 的完整提示词</summary>
    public string BuildPrompt(string snapshot) =>
        $"""
         你是一个目标管理教练，正在帮用户复盘他的「每日打卡」记录。下面是他的完整数据快照。

         数据里有四层目标：总目标 → 月目标 → 周目标 → 今天，进度自下而上汇总。
         任务分两种：一次性任务（做完打勾），以及「每日任务」（每天重新打卡，
         没打卡的那天也算进分母，所以完成率反映的是真实坚持情况）。

         请完成三件事：
         1.【完成情况分析】用数据说话：哪些做得好、哪些明显有问题（例如连续中断、
            每日任务完成率持续偏低、目标写得太大无法执行、任务堆积等）。不要写空泛的鼓励。
         2.【目标建议】结合倒数日，给出接下来一周和一个月的具体目标，
            要可量化、能拆成每天能做完的任务。可以直接给出建议的目标文字和任务清单。
         3.【马上能做的改进】2 到 3 条具体动作。

         输出要求：Markdown 格式，中文，正文控制在 700 字以内，直接给结论。

         ---

         {snapshot}
         """;

    // ==================== 建议读写 ====================

    /// <summary>读取上次生成的建议，返回 (内容, 生成时间)</summary>
    public (string Text, string Time)? ReadAdvice()
    {
        if (!File.Exists(AdviceFile)) return null;
        try
        {
            var text = File.ReadAllText(AdviceFile).Trim();
            if (text.Length == 0) return null;
            var time = File.GetLastWriteTime(AdviceFile);
            return (text, time.ToString("yyyy-MM-dd HH:mm"));
        }
        catch
        {
            return null;
        }
    }

    public void WriteAdvice(string content)
    {
        Directory.CreateDirectory(_svc.DataDir);
        File.WriteAllText(AdviceFile, content);
    }

    // ==================== 调用 claude 命令行 ====================

    /// <summary>调用本机 claude 命令行做无头分析。数据走 stdin，避免命令行长度和转义问题</summary>
    public static async Task<string> AskClaudeAsync(string prompt, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            // 不带引号的简单参数，避免 cmd 的引号解析坑
            Arguments = "/c claude -p",
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetTempPath(),
        };

        // 如果 PATH 里找不到 claude，补上 npm / Node 的常见全局目录
        if (FindClaudeDir() is null && FindClaudeFallbackDir() is { } fallback)
        {
            var path = psi.EnvironmentVariables["PATH"]
                       ?? Environment.GetEnvironmentVariable("PATH") ?? "";
            psi.EnvironmentVariables["PATH"] = fallback + ";" + path;
        }

        using var proc = new Process { StartInfo = psi };
        proc.Start();

        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);

        await proc.StandardInput.WriteAsync(prompt.AsMemory(), ct);
        await proc.StandardInput.FlushAsync(ct);
        proc.StandardInput.Close();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        try
        {
            await proc.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            try { proc.Kill(entireProcessTree: true); } catch { /* 已经退出了 */ }
            throw new TimeoutException("分析超时（超过 5 分钟）。请检查网络或 claude 登录状态后重试。");
        }

        var output = CleanOutput(await stdoutTask);
        var error = (await stderrTask).Trim();

        if (output.Length == 0)
        {
            throw new Exception(
                $"claude 没有返回内容（退出码 {proc.ExitCode}）。\n\n" +
                (error.Length > 0 ? error : "请先在命令行里运行一次 claude，确认已登录。"));
        }
        return output;
    }

    /// <summary>去掉 claude 命令行混进正文的提示行</summary>
    private static string CleanOutput(string raw)
    {
        var kept = raw.Replace("\r\n", "\n")
                      .Split('\n')
                      .Where(l => !l.StartsWith("[claude-code:")
                               && !l.StartsWith("Shell cwd was reset"))
                      .ToArray();
        return string.Join("\n", kept).Trim();
    }

    private static string? FindClaudeDir()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var raw in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var dir = raw.Trim().Trim('"');
            if (dir.Length > 0 && HasClaude(dir)) return dir;
        }
        return null;
    }

    /// <summary>
    /// PATH 里没有 claude 时，到 npm / Node 的常见全局目录里碰碰运气。
    /// 这些位置随安装方式而变，所以逐个探测，不写死某一台机器的路径。
    /// </summary>
    private static string? FindClaudeFallbackDir()
    {
        var candidates = new List<string>();

        // npm 默认的全局目录：%APPDATA%\npm
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (appData.Length > 0) candidates.Add(Path.Combine(appData, "npm"));

        // 改过 npm prefix 的情况
        foreach (var name in new[] { "NPM_CONFIG_PREFIX", "NODE_HOME" })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value)) candidates.Add(value);
        }

        // Node.js 官方安装目录
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (programFiles.Length > 0) candidates.Add(Path.Combine(programFiles, "nodejs"));

        return candidates.FirstOrDefault(HasClaude);
    }

    /// <summary>目录里是否有 claude 可执行文件（路径非法时视为没有）</summary>
    private static bool HasClaude(string dir)
    {
        try
        {
            return File.Exists(Path.Combine(dir, "claude.cmd")) ||
                   File.Exists(Path.Combine(dir, "claude.exe"));
        }
        catch
        {
            return false;
        }
    }
}
