using System.Windows;
using DailyCheck.Models;

namespace DailyCheck;

public partial class SettingsWindow : Window
{
    private readonly AppData _data;

    public SettingsWindow(AppData data)
    {
        InitializeComponent();
        _data = data;
        CdList.ItemsSource = data.Countdowns;
    }

    private void AddCd_Click(object sender, RoutedEventArgs e)
        => _data.Countdowns.Add(new Countdown
        {
            Title = "新的倒数日",
            TargetDate = DateTime.Today.AddDays(100),
        });

    private void DelCd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: Countdown cd })
            _data.Countdowns.Remove(cd);
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
