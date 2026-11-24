using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MikoMiko.ViewModels;

namespace MikoMiko.Views;

/// <summary>追番/影视页：模块下载引导、影视首页与查看更多。</summary>
public partial class AnimeView : UserControl
{
    private readonly AnimeViewModel _viewModel;

    public AnimeView(AnimeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => ScrollToTop();
        viewModel.PropertyChanged += OnViewModelChanged;
        Unloaded += (_, _) => viewModel.PropertyChanged -= OnViewModelChanged;
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AnimeViewModel.IsMorePage) or nameof(AnimeViewModel.IsInstalled)
            or nameof(AnimeViewModel.IsSearching) or nameof(AnimeViewModel.IsWatchlistPage))
            ScrollToTop();
    }

    private void AnimeScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.ExtentHeightChange > 0 && Math.Abs(e.VerticalChange) < 0.5) return;
        if (_viewModel.IsMorePage || _viewModel.IsWatchlistPage) return;
        if (AnimeScroll.ScrollableHeight - AnimeScroll.VerticalOffset < 420)
            _viewModel.LoadMoreHome();
    }

    private void Top_Click(object sender, System.Windows.RoutedEventArgs e)
        => ScrollToTop();

    public void ResetScroll()
    {
        Keyboard.ClearFocus();
        AnimeScroll.UpdateLayout();
        AnimeScroll.ScrollToVerticalOffset(0);
        Dispatcher.BeginInvoke(() => AnimeScroll.ScrollToVerticalOffset(0), DispatcherPriority.Loaded);
    }

    private void ScrollToTop()
    {
        Dispatcher.BeginInvoke(() =>
        {
            Keyboard.ClearFocus();
            AnimeScroll.ScrollToVerticalOffset(0);
        }, DispatcherPriority.ApplicationIdle);
    }

    private void AnimeSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if (_viewModel.SearchCommand.CanExecute(null)) _viewModel.SearchCommand.Execute(null);
        e.Handled = true;
    }
}
