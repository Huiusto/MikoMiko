using MikoMiko.ViewModels;
using System.Windows.Controls;

namespace MikoMiko.Views;

public partial class UserProfileView : UserControl
{
    public UserProfileView(UserProfileViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
