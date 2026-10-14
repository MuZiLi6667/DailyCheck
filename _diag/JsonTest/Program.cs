using System.Text.Json;
using System.Text.Json.Serialization;
using DailyCheck.Models;

var path = @"D:\打卡程序\DailyCheck\bin\Debug\net9.0-windows\data\dailycheck.json";
var json = File.ReadAllText(path);

var opts = new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

try
{
    var data = JsonSerializer.Deserialize<AppData>(json, opts)!;
    Console.WriteLine($"Countdowns = {data.Countdowns.Count}");
    Console.WriteLine($"MonthGoals = {data.MonthGoals.Count}");
    Console.WriteLine($"WeekGoals  = {data.WeekGoals.Count}");
    Console.WriteLine($"Days       = {data.Days.Count}");
    foreach (var kv in data.Days)
        Console.WriteLine($"  {kv.Key}: Tasks={kv.Value.Tasks.Count} Title={kv.Value.Title}");

    // 再序列化回去，看看 Days 会不会被写出来
    Console.WriteLine("--- round-trip ---");
    var back = JsonSerializer.Serialize(data, opts);
    Console.WriteLine($"回写后 JSON 长度 = {back.Length}");
    Console.WriteLine($"回写后还包含 Days 键 = {back.Contains("\"Days\"")}");
    Console.WriteLine($"回写后还包含 2026-09-07 = {back.Contains("2026-09-07")}");
}
catch (Exception ex)
{
    Console.WriteLine("EXCEPTION:");
    Console.WriteLine(ex.ToString());
}
