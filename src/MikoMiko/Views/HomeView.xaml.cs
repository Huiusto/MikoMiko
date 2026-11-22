using MikoMiko.ViewModels;
using System.Windows.Controls;

namespace MikoMiko.Views;

public partial class HomeView : UserControl
{
    private readonly HomeViewModel _vm;

    public HomeView(HomeViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;
    }

    /// <summary>接近底部时再追加一批随机帖子（无限下滑）。</summary>
    private void FeedScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange <= 0) return;
        if (e.OriginalSource is not ScrollViewer scroll) return;
        if (scroll.VerticalOffset + scroll.ViewportHeight + 600 < scroll.ExtentHeight) return;

        _ = _vm.LoadMoreAsync();
    }
}
