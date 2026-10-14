using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DailyCheck.Models;

namespace DailyCheck;

/// <summary>
/// 一个目标节点（含它的任务和下级子目标）。
/// 子目标是靠这个控件递归渲染出来的，所以能无限嵌套。
/// </summary>
public partial class GoalView : UserControl
{
    /// <summary>数据被改动（加/删任务、加/删子目标、打勾），主窗口收到后刷新统计并存盘</summary>
    public static event Action? DataChanged;

    public static readonly DependencyProperty AccentProperty =
        DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(GoalView),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x4F, 0x8E, 0xF7))));

    /// <summary>本层的主题色，子目标会继承同一个颜色</summary>
    public Brush Accent
    {
        get => (Brush)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    public GoalView()
    {
        InitializeComponent();
    }

    // ==================== 任务 ====================

    private void Task_Click(object sender, RoutedEventArgs e) => DataChanged?.Invoke();

    private void DeleteTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: TaskItem item }
            && FindContainer(item) is { } owner)
        {
            owner.Tasks.Remove(item);
            DataChanged?.Invoke();
        }
    }

    private void AddTask_Click(object sender, RoutedEventArgs e) => AddTask(sender, daily: false);

    private void AddDaily_Click(object sender, RoutedEventArgs e) => AddTask(sender, daily: true);

    private void AddTask(object sender, bool daily)
    {
        if (sender is not FrameworkElement fe || GetContainer(fe) is not { } c) return;

        var text = Prompt.Show(
            daily ? "添加每日任务" : "添加任务",
            daily
                ? "每天都要做的事。没打卡的天会算作未完成，所以进度反映的是真实坚持情况。"
                : "写一件具体的事，做完打勾。");
        if (string.IsNullOrWhiteSpace(text)) return;

        var task = new TaskItem { Text = text, Daily = daily };
        if (daily) task.StartDate = Stats.TodayKey;
        c.Tasks.Add(task);
        DataChanged?.Invoke();
    }

    // ==================== 子目标 ====================

    private void AddChild_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || GetContainer(fe) is not { } c) return;

        var child = new SubGoal { Title = "新子目标" };
        c.Children.Add(child);
        DataChanged?.Invoke();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SubGoal sg }) return;

        var parent = FindParentOf(sg);
        if (parent == null) return;

        var answer = MessageBox.Show(
            $"删除子目标「{sg.Title}」？\n\n它下面的任务和子目标会一起删掉。",
            "删除", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.OK) return;

        parent.Children.Remove(sg);
        DataChanged?.Invoke();
    }

    // ==================== 找容器 ====================

    /// <summary>按钮所在的那个目标容器。递归出来的子控件 DataContext 就是它</summary>
    private static GoalContainer? GetContainer(FrameworkElement fe)
        => fe.DataContext as GoalContainer;

    /// <summary>反查某个任务挂在哪个容器下（任务本身不知道自己属于谁）</summary>
    private static GoalContainer? FindContainer(TaskItem item)
    {
        var data = MainWindow.CurrentData;
        if (data == null) return null;

        foreach (var root in AllRoots(data))
        {
            var found = FindContainerIn(root, item);
            if (found != null) return found;
        }
        return null;
    }

    private static GoalContainer? FindContainerIn(GoalContainer c, TaskItem item)
    {
        if (c.Tasks.Contains(item)) return c;
        foreach (var child in c.Children)
        {
            var found = FindContainerIn(child, item);
            if (found != null) return found;
        }
        return null;
    }

    private static GoalContainer? FindParentOf(SubGoal target)
    {
        var data = MainWindow.CurrentData;
        if (data == null) return null;

        foreach (var root in AllRoots(data))
        {
            var found = FindParentIn(root, target);
            if (found != null) return found;
        }
        return null;
    }

    private static GoalContainer? FindParentIn(GoalContainer c, SubGoal target)
    {
        if (c.Children.Contains(target)) return c;
        foreach (var child in c.Children)
        {
            var found = FindParentIn(child, target);
            if (found != null) return found;
        }
        return null;
    }

    private static IEnumerable<GoalContainer> AllRoots(AppData data)
    {
        yield return data.Overall;
        foreach (var g in data.MonthGoals.Values) yield return g;
        foreach (var g in data.WeekGoals.Values) yield return g;
        foreach (var d in data.Days.Values) yield return d;
    }
}
