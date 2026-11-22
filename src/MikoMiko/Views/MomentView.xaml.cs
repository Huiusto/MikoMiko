using MikoMiko.ViewModels;
using System.Windows.Controls;

namespace MikoMiko.Views;

public partial class MomentView : UserControl
{
    public MomentView(MomentViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
