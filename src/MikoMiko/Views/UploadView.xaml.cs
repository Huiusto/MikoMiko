using System.Windows.Controls;
using MikoMiko.ViewModels;

namespace MikoMiko.Views;

public partial class UploadView : UserControl
{
    public UploadView(UploadViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
