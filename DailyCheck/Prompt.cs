using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DailyCheck;

/// <summary>一个轻量的输入对话框（WPF 没有内置 InputBox，这里用代码搭一个）</summary>
public static class Prompt
{
    public static string? Show(string title, string label, string defaultValue = "")
    {
        var window = new Window
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current?.MainWindow,
            ShowInTaskbar = false,
            Background = new SolidColorBrush(Color.FromRgb(0xF4, 0xF6, 0xFA)),
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI"),
        };

        var box = new TextBox
        {
            Text = defaultValue,
            FontSize = 13.5,
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 12, 0, 0),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xE4, 0xEF)),
            BorderThickness = new Thickness(1),
        };

        var okBtn = new Button { Content = "确定", Width = 76, Height = 30, IsDefault = true, Margin = new Thickness(8, 0, 0, 0) };
        var cancelBtn = new Button { Content = "取消", Width = 76, Height = 30, IsCancel = true };

        string? result = null;
        okBtn.Click += (_, _) =>
        {
            result = box.Text.Trim();
            if (result.Length == 0) { box.Focus(); return; }
            window.DialogResult = true;
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        buttons.Children.Add(cancelBtn);
        buttons.Children.Add(okBtn);

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0x6B, 0x88)),
        });
        panel.Children.Add(box);
        panel.Children.Add(buttons);

        window.Content = panel;
        window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };

        return window.ShowDialog() == true ? result : null;
    }
}
