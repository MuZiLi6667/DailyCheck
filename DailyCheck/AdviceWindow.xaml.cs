using System.Windows;

namespace DailyCheck;

public partial class AdviceWindow : Window
{
    /// <summary>用户是否点了「重新分析」</summary>
    public bool WantReanalyze { get; private set; }

    public AdviceWindow(string content, string meta)
    {
        InitializeComponent();
        ContentBox.Text = content;
        MetaText.Text = meta;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(ContentBox.Text); }
        catch { /* 剪贴板被别的程序占用，忽略 */ }
    }

    private void Reanalyze_Click(object sender, RoutedEventArgs e)
    {
        WantReanalyze = true;
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
