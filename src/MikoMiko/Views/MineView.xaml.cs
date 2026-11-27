using MikoMiko.Models;
using MikoMiko.Services;
using MikoMiko.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;

namespace MikoMiko.Views;

public partial class MineView : UserControl
{
    private readonly MineViewModel _vm;
    private Popup? _accountPopup;

    public MineView(MineViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;
    }

    private void Collection_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: UserCollectionItem item })
            _vm.OpenCollection(item);
    }

    private void SwitchAccount_Click(object sender, RoutedEventArgs e)
    {
        _accountPopup?.SetCurrentValue(Popup.IsOpenProperty, false);
        _accountPopup = BuildAccountPopup();
        _accountPopup.IsOpen = true;
    }

    /// <summary>切换账号浮层：顶部「添加新账号」，下面列出头像、用户名与米游社 UID。</summary>
    private Popup BuildAccountPopup()
    {
        HoyoAccountService.Reload();
        var accounts = HoyoAccountService.All.ToList();
        var active = HoyoAccountService.Active;
        var activeId = active?.Id;

        var stack = new StackPanel();

        var addButton = new Border
        {
            Height = 44,
            CornerRadius = new CornerRadius(11),
            Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF6, 0xFF)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xD4, 0xE7, 0xFA)),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new TextBlock
                    {
                        Text = "+",
                        FontSize = 19,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x2F, 0x86, 0xC9)),
                        Margin = new Thickness(0, 0, 7, 1),
                    },
                    new TextBlock
                    {
                        Text = "添加新账号",
                        FontSize = 13,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x2F, 0x86, 0xC9)),
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            },
        };
        addButton.MouseLeftButtonUp += (_, _) =>
        {
            _accountPopup?.SetCurrentValue(Popup.IsOpenProperty, false);
            var dialog = new AddAccountWindow { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() != true) return;
            HoyoAccountService.Reload();
            _accountPopup = BuildAccountPopup();
            _accountPopup.IsOpen = true;
        };
        stack.Children.Add(addButton);

        stack.Children.Add(new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xF8)),
            Margin = new Thickness(0, 10, 0, 6),
        });

        if (accounts.Count == 0)
        {
            stack.Children.Add(new TextBlock
            {
                Text = "还没有添加账号",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x9A, 0xAA)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 18, 0, 18),
            });
        }
        else
        {
            var rows = new StackPanel();
            foreach (var account in accounts)
            {
                rows.Children.Add(BuildAccountRow(account, account.Id == activeId));
            }
            stack.Children.Add(new ScrollViewer
            {
                MaxHeight = 330,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = rows,
            });
        }

        var border = new Border
        {
            Width = 336,
            Background = Brushes.White,
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(12),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xDF, 0xEB, 0xF6)),
            BorderThickness = new Thickness(1),
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0x2C, 0x58, 0x88),
                BlurRadius = 24,
                ShadowDepth = 5,
                Direction = 270,
                Opacity = 0.18,
            },
            Child = stack,
        };

        var popup = new Popup
        {
            PlacementTarget = BtnSwitchMineAccount,
            Placement = PlacementMode.Custom,
            AllowsTransparency = true,
            StaysOpen = false,
            PopupAnimation = PopupAnimation.Fade,
            Child = border,
            CustomPopupPlacementCallback = (popupSize, targetSize, _) =>
                new[] { new CustomPopupPlacement(
                    new Point(targetSize.Width + 10, 0),
                    PopupPrimaryAxis.Vertical) },
        };
        return popup;
    }

    private Border BuildAccountRow(HoyoAccount account, bool active)
    {
        var avatar = new Border
        {
            Width = 42,
            Height = 42,
            CornerRadius = new CornerRadius(21),
            Background = new SolidColorBrush(Color.FromRgb(0xEA, 0xF3, 0xFC)),
            ClipToBounds = true,
        };
        var avatarGrid = new Grid();
        var fallback = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(account.Nickname) ? "?" :
                   account.Nickname[..Math.Min(1, account.Nickname.Length)],
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x4B, 0x7B, 0xC8)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        avatarGrid.Children.Add(fallback);
        if (!string.IsNullOrWhiteSpace(account.AvatarUrl))
        {
            var image = new Image { Stretch = Stretch.UniformToFill };
            try
            {
                image.Source = new BitmapImage(new Uri(account.AvatarUrl, UriKind.Absolute));
            }
            catch
            {
                image.Source = null;
            }
            image.ImageFailed += (_, _) => image.Visibility = Visibility.Collapsed;
            avatarGrid.Children.Add(image);
        }
        avatar.Child = avatarGrid;

        var name = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(account.Nickname) ? "米游社账号" : account.Nickname,
            FontSize = 13.5,
            FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = new SolidColorBrush(Color.FromRgb(0x25, 0x35, 0x46)),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var uid = new TextBlock
        {
            Text = $"米游社 UID：{(string.IsNullOrWhiteSpace(account.Uid) ? "未获取" : account.Uid)}",
            FontSize = 10.5,
            Foreground = new SolidColorBrush(Color.FromRgb(0x84, 0x95, 0xA8)),
            Margin = new Thickness(0, 3, 0, 0),
        };
        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(11, 0, 8, 0),
            Children = { name, uid },
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(avatar, 0);
        Grid.SetColumn(text, 1);
        grid.Children.Add(avatar);
        grid.Children.Add(text);

        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        if (active)
        {
            right.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xFF)),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 3, 8, 3),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "当前",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x2F, 0x86, 0xC9)),
                },
            });
        }

        // 每个账号行右侧的删除图标：删掉该账号，不再需要到账号页单独退出。
        var delete = new System.Windows.Controls.Button
        {
            Width = 28,
            Height = 28,
            Margin = new Thickness(6, 0, 0, 0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "删除该账号",
            Focusable = false,
        };
        var deleteBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = "🗑",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA7, 0x64, 0x64)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        delete.Template = new ControlTemplate(typeof(System.Windows.Controls.Button))
        {
            VisualTree = new FrameworkElementFactory(typeof(ContentPresenter)),
        };
        delete.Content = deleteBorder;
        deleteBorder.MouseEnter += (_, _) =>
            deleteBorder.Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xEE, 0xEE));
        deleteBorder.MouseLeave += (_, _) => deleteBorder.Background = Brushes.Transparent;
        delete.PreviewMouseLeftButtonUp += async (_, e) =>
        {
            e.Handled = true;
            var confirm = MessageBox.Show(
                Window.GetWindow(this),
                $"确定删除账号「{(string.IsNullOrWhiteSpace(account.Nickname) ? account.Uid : account.Nickname)}」吗？\n删除后需要重新登录才能恢复该账号。",
                "删除账号", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.OK) return;

            _accountPopup?.SetCurrentValue(Popup.IsOpenProperty, false);
            await _vm.RemoveAccountAsync(account);
            HoyoAccountService.Reload();
            if (HoyoAccountService.All.Count > 0)
            {
                _accountPopup = BuildAccountPopup();
                _accountPopup.IsOpen = true;
            }
        };
        // 当前标记与删除按钮并排放在最右侧。
        right.Children.Add(delete);
        Grid.SetColumn(right, 2);
        grid.Children.Add(right);

        var row = new Border
        {
            Background = active
                ? new SolidColorBrush(Color.FromRgb(0xF3, 0xF9, 0xFF))
                : Brushes.Transparent,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(8, 8, 8, 8),
            Margin = new Thickness(0, 2, 0, 2),
            Cursor = Cursors.Hand,
            Child = grid,
        };
        row.MouseLeftButtonUp += async (_, _) =>
        {
            _accountPopup?.SetCurrentValue(Popup.IsOpenProperty, false);
            await _vm.SwitchAccountAsync(account);
        };
        return row;
    }
}
