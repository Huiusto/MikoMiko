using System.Windows;
using System.Windows.Input;

namespace MikoMiko.Views;

/// <summary>启动画面：白底银发少女 Logo，短暂停留后交给主窗口。</summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // 点击任意位置可提前结束等待，主窗口仍由启动计时器负责显示。
        Close();
    }
}
