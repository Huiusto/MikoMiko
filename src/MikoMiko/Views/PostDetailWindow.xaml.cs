using MikoMiko.Models;
using MikoMiko.Services;
using MikoMiko.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace MikoMiko.Views;

/// <summary>帖子 / 资讯详情窗口。</summary>
public partial class PostDetailWindow : Window
{
    private readonly PostDetailViewModel _viewModel;

    public PostDetailWindow(FeedItem item, ContentService content)
    {
        InitializeComponent();
        _viewModel = new PostDetailViewModel(item, content);
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadAsync();
    }

    /// <summary>打开详情窗口（非模态，同一时间只保留一个）。</summary>
    public static void Show(FeedItem item, ContentService content)
    {
        var window = new PostDetailWindow(item, content);
        var owner = Application.Current?.MainWindow;
        if (owner is not null && owner.IsVisible)
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        window.Show();
        window.Activate();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
