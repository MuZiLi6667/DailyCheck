using System.Threading;
using System.Windows;

namespace DailyCheck;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        // 防止启动出第二个实例：两个窗口各持一份内存里的数据，
        // 关闭时后关的那个会把先关的改动覆盖掉。
        _singleInstance = new Mutex(true, @"Local\DailyCheck_SingleInstance", out var isFirst);
        if (!isFirst)
        {
            MessageBox.Show("每日打卡已经在运行了，去任务栏找找。", "每日打卡",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}
