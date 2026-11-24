using MikoMiko.Models;
using MikoMiko.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MikoMiko.Views;

public partial class MomentView : UserControl
{
    private readonly MomentViewModel _vm;

    public MomentView(MomentViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;
    }

    /// <summary>点击动态里的头像或昵称：右侧只看该用户发布的全部内容。</summary>
    private void MomentAuthor_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MomentItem moment })
        {
            _vm.SelectMomentAuthorCommand.Execute(moment);
            MomentScroll.ScrollToTop();
            e.Handled = true;
        }
    }

    private void BackToTop_Click(object sender, RoutedEventArgs e) => MomentScroll.ScrollToTop();

    private void FloatingRefresh_Click(object sender, RoutedEventArgs e)
    {
        MomentScroll.ScrollToTop();
        _vm.RefreshCommand.Execute(null);
    }
}
