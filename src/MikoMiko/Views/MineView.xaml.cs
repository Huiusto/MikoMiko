using MikoMiko.ViewModels;
using System.Windows.Controls;

namespace MikoMiko.Views;

public partial class MineView : UserControl
{
    public MineView(MineViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
