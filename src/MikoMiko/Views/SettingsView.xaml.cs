using MikoMiko.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MikoMiko.Views;

public partial class SettingsView : UserControl
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>右侧目录按钮：平滑滚动到对应设置卡片。</summary>
    private void Section_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement button && button.Tag is string key)
        {
            if (DataContext is SettingsViewModel vm) vm.SelectSectionByKey(key);
            ScrollToSection(key);
        }
    }

    /// <summary>滚动到指定设置分区（也供截图模式使用）。</summary>
    public void ScrollToSection(string key)
    {
        foreach (var child in SectionsPanel.Children)
        {
            if (child is not FrameworkElement element || element.Tag is not string tag) continue;
            if (tag != key) continue;

            var offset = element.TransformToAncestor(ScrollHost).Transform(new Point(0, 0)).Y + ScrollHost.VerticalOffset;
            ScrollHost.ScrollToVerticalOffset(Math.Max(0, offset - 8));
            return;
        }
    }
}
