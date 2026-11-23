using MikoMiko.ViewModels;
using System.Windows;
using System.Windows.Controls;

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

    private void BackToTop_Click(object sender, RoutedEventArgs e) => MomentScroll.ScrollToTop();

    private void FloatingRefresh_Click(object sender, RoutedEventArgs e)
    {
        MomentScroll.ScrollToTop();
        _vm.RefreshCommand.Execute(null);
    }
}
