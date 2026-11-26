using MikoMiko.Services;
using MikoMiko.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace MikoMiko.Views;

public partial class MineView : UserControl
{
    private readonly MineViewModel _vm;

    public MineView(MineViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;
    }

    private void SwitchAccount_Click(object sender, RoutedEventArgs e)
    {
        HoyoAccountService.Reload();
        var accounts = HoyoAccountService.All.ToList();
        if (accounts.Count == 0)
        {
            MessageBox.Show("还没有可切换的米游社账号，请先在「每日任务」页添加账号。", "MikoMiko",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var menu = new ContextMenu
        {
            PlacementTarget = BtnSwitchMineAccount,
            Placement = PlacementMode.Bottom,
        };
        var currentUid = HoyoAccountService.Active?.Uid;
        foreach (var account in accounts)
        {
            var active = account.Uid == currentUid;
            var item = new MenuItem
            {
                Header = $"{(active ? "● " : "○ ")}{account.DisplayName}",
                FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            };
            item.Click += async (_, _) => await _vm.SwitchAccountAsync(account);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }
}
