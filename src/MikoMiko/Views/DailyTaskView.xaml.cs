using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MikoMiko.Models;
using MikoMiko.Services;
using Button = System.Windows.Controls.Button;

namespace MikoMiko.Views;

/// <summary>每日任务：米游社米游币任务 + 账号下各游戏角色签到，复用 MikoNest 的签到接口实现。</summary>
public partial class DailyTaskView : UserControl
{
    private sealed class RoleRow
    {
        public SignRole Role = new();
        public SignService.GameCheckin? Game;
        public required Border RowBorder;
        public required TextBlock Status;
        public required Button Action;
        public bool Signed;
        public bool Busy;
    }

    private static readonly Dictionary<string, Color> GameColors = new()
    {
        ["hk4e"] = Color.FromRgb(0x4E, 0xA4, 0xDD),
        ["hkrpg"] = Color.FromRgb(0x8A, 0x6D, 0xF0),
        ["nap"] = Color.FromRgb(0xFF, 0x8A, 0x3D),
        ["bh3"] = Color.FromRgb(0xD9, 0x6B, 0xC7),
    };

    private readonly ObservableCollection<SignLogEntry> _logs = new();
    private readonly List<RoleRow> _roleRows = new();
    private MikoNestAccount? _account;
    private bool _loadedOnce;
    private bool _busy;
    private int _activeTasks;

    public DailyTaskView()
    {
        InitializeComponent();
        LogList.ItemsSource = _logs;
        Loaded += (_, _) => _ = LoadPageAsync();
    }

    private async Task LoadPageAsync()
    {
        if (_busy) return;
        if (!_loadedOnce)
        {
            _loadedOnce = true;
            AppendLog(new SignLogEntry { Text = "正在读取 MikoNest 账号…", Level = "INFO" });
            await RefreshAllAsync();
            return;
        }
        // 再次进入页面不重复拉取，除非角色列表为空
        if (_roleRows.Count == 0 && _account is not null) await RefreshAllAsync();
    }

    private async Task RefreshAllAsync()
    {
        _busy = true;
        BtnRefresh.IsEnabled = false;
        try
        {
            MikoNestBridge.Reload();
            _account = MikoNestBridge.ActiveAccount;
            UpdateAccountHeader();

            if (_account is null)
            {
                AppendLog(new SignLogEntry
                {
                    Text = "没有找到可用的米游社账号。请先在 MikoNest 登录一次，再回到这里刷新。",
                    Level = "ERROR",
                });
                ShowRolesEmpty("未找到 MikoNest 账号，登录后即可读取角色签到状态。");
                ShowMissionsEmpty("未登录，无法读取米游社任务。", "需要先登录");
                return;
            }

            AppendLog(new SignLogEntry { Text = $"当前账号：{_account.Nickname}（{_account.Uid}）", Level = "INFO" });
            var cookie = _account.Cookie;
            var rolesResult = await SignService.GetAllRolesAsync(cookie);
            if (rolesResult.LoginExpired || rolesResult.Error is not null)
            {
                AppendLog(new SignLogEntry
                {
                    Text = rolesResult.Error ?? "登录状态已失效，请到 MikoNest 的「账号」页重新登录。",
                    Level = "ERROR",
                });
                ShowRolesEmpty(rolesResult.Error ?? "登录状态已失效，请重新登录。");
            }
            else
            {
                BuildRoles(rolesResult.Roles);
                foreach (var row in _roleRows)
                    _ = LoadRoleStateAsync(row);
            }

            await RefreshMissionsInternalAsync();
        }
        catch (Exception ex)
        {
            AppendLog(new SignLogEntry { Text = "刷新失败：" + ex.Message, Level = "ERROR" });
        }
        finally
        {
            _busy = false;
            BtnRefresh.IsEnabled = true;
        }
    }

    private void UpdateAccountHeader()
    {
        if (_account is null)
        {
            TxtAvatar.Text = "!";
            TxtAccount.Text = "未登录";
            TxtAccountState.Text = "未找到账号";
            StatusDot.Fill = (Brush)FindResource("Brush.Danger");
            return;
        }
        TxtAvatar.Text = string.IsNullOrWhiteSpace(_account.Nickname) ? "米" : _account.Nickname[..1];
        TxtAccount.Text = _account.Nickname;
        TxtAccountState.Text = $"UID {_account.Uid} · 米游社已登录";
        StatusDot.Fill = (Brush)FindResource("Brush.Success");
    }

    private void BuildRoles(List<SignRole> roles)
    {
        RolePanel.Children.Clear();
        _roleRows.Clear();
        TxtRoleBadge.Text = $"{roles.Count} 个角色";
        if (roles.Count == 0)
        {
            ShowRolesEmpty("账号下没有读取到游戏角色。请确认账号已绑定游戏角色。");
            return;
        }

        foreach (var role in roles)
        {
            var game = SignService.GetByBiz(role.GameBiz);
            var gameName = game?.Name ?? role.GameBiz;
            var color = game is not null && GameColors.TryGetValue(game.Host, out var c)
                ? c
                : Color.FromRgb(0x8F, 0x98, 0xA3);

            var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatar = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(0x20, color.R, color.G, color.B)),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(role.Nickname) ? "角" : role.Nickname[..1],
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(color),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                },
            };
            Grid.SetColumn(avatar, 0);
            grid.Children.Add(avatar);

            var text = new StackPanel { VerticalAlignment = System.Windows.VerticalAlignment.Center };
            text.Children.Add(new TextBlock
            {
                Text = role.Nickname,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Brush.TextPrimary"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            text.Children.Add(new TextBlock
            {
                Text = $"{gameName} · {role.Uid} · {SignService.RegionLabel(role)}",
                FontSize = 10,
                Foreground = (Brush)FindResource("Brush.TextTertiary"),
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            var status = new TextBlock
            {
                Text = "查询中…",
                FontSize = 11,
                Foreground = (Brush)FindResource("Brush.TextTertiary"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0),
            };
            Grid.SetColumn(status, 2);
            grid.Children.Add(status);

            var action = new Button
            {
                Content = "签到",
                Style = (Style)FindResource("TaskButton"),
                Padding = new Thickness(14, 5, 14, 5),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Tag = role,
                IsEnabled = game is { CheckinEnabled: true },
            };
            var row = new RoleRow
            {
                Role = role,
                Game = game,
                RowBorder = new Border(),
                Status = status,
                Action = action,
            };
            action.Click += async (_, _) => await SignOneAsync(row);
            Grid.SetColumn(action, 3);
            grid.Children.Add(action);

            var border = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = (Brush)FindResource("Brush.Surface"),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 0, 4),
                Child = grid,
            };
            row.RowBorder = border;
            _roleRows.Add(row);
            RolePanel.Children.Add(border);

            if (game is null)
            {
                status.Text = "暂不支持";
                action.IsEnabled = false;
            }
        }
    }

    private async Task LoadRoleStateAsync(RoleRow row)
    {
        if (_account is null || row.Game is null) return;
        _activeTasks++;
        try
        {
            var state = await SignService.GetSignStateAsync(_account.Cookie, row.Game, row.Role);
            if (state.Signed == true)
            {
                row.Signed = true;
                row.Status.Text = "已签到";
                row.Status.Foreground = (Brush)FindResource("Brush.Success");
                row.Action.Content = "已签到";
                row.Action.IsEnabled = false;
                row.RowBorder.Background = new SolidColorBrush(Color.FromArgb(0x14, 0x39, 0xB8, 0x6A));
            }
            else if (state.Signed == false)
            {
                row.Status.Text = "未签到";
                row.Status.Foreground = (Brush)FindResource("Brush.Accent");
            }
            else
            {
                row.Status.Text = "状态未知";
                row.Status.Foreground = (Brush)FindResource("Brush.TextTertiary");
            }
        }
        catch
        {
            row.Status.Text = "状态未知";
        }
        finally
        {
            _activeTasks--;
        }
    }

    private async Task SignOneAsync(RoleRow row)
    {
        if (_account is null || row.Game is null || row.Busy) return;
        row.Busy = true;
        row.Action.IsEnabled = false;
        row.Action.Content = "签到中";
        try
        {
            var result = await SignService.SignRoleAsync(_account.Cookie, row.Game, row.Role);
            foreach (var line in result.Log)
                AppendLog(new SignLogEntry { Text = line.Text, Level = line.Level });
            if (result.Ok)
            {
                row.Signed = true;
                row.Status.Text = "已签到";
                row.Status.Foreground = (Brush)FindResource("Brush.Success");
                row.Action.Content = "已签到";
                row.RowBorder.Background = new SolidColorBrush(Color.FromArgb(0x14, 0x39, 0xB8, 0x6A));
            }
            else
            {
                row.Status.Text = result.Message;
                row.Status.Foreground = (Brush)FindResource("Brush.Danger");
                row.Action.Content = "重试";
            }
        }
        catch (Exception ex)
        {
            AppendLog(new SignLogEntry { Text = "签到失败：" + ex.Message, Level = "ERROR" });
            row.Action.Content = "重试";
        }
        finally
        {
            row.Busy = false;
            row.Action.IsEnabled = !row.Signed;
        }
    }

    private async Task RefreshMissionsInternalAsync()
    {
        if (_account is null) return;
        var result = await SignService.GetMissionsAsync(_account.Cookie);
        if (result.snapshot is null)
        {
            ShowMissionsEmpty(result.error ?? "米游社任务暂时无法读取", "获取失败");
            if (!string.IsNullOrWhiteSpace(result.error))
                AppendLog(new SignLogEntry { Text = result.error!, Level = "WARN" });
            return;
        }
        RenderMissions(result.snapshot);
    }

    private void RenderMissions(SignService.MissionSnapshot snapshot)
    {
        MissionPanel.Children.Clear();
        TxtPoints.Text = snapshot.UserPoints > 0 ? $"{snapshot.UserPoints:N0}" : "—";
        TxtMissionBadge.Text = $"{snapshot.TodayPoints}/{Math.Max(50, snapshot.TodayPoints)} 米游币";
        TxtMissionHint.Text = "任务状态来自米游社，完成后米游币自动发放。";

        if (snapshot.Items.Count == 0)
        {
            MissionPanel.Children.Add(new TextBlock
            {
                Text = "暂无米游币任务数据，稍后再刷新试试。",
                FontSize = 11.5,
                Foreground = (Brush)FindResource("Brush.TextTertiary"),
                Margin = new Thickness(0, 4, 0, 4),
            });
            return;
        }

        foreach (var item in snapshot.Items)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });

            var mark = new Border
            {
                Width = 17,
                Height = 17,
                CornerRadius = new CornerRadius(9),
                Background = item.Done
                    ? (Brush)FindResource("Brush.Success")
                    : (Brush)FindResource("Brush.Surface"),
                BorderBrush = item.Done ? System.Windows.Media.Brushes.Transparent : (Brush)FindResource("Brush.Border"),
                BorderThickness = new Thickness(1),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = item.Done ? "✓" : "",
                    FontSize = 10,
                    Foreground = System.Windows.Media.Brushes.White,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                },
            };
            Grid.SetColumn(mark, 0);
            grid.Children.Add(mark);

            var name = new TextBlock
            {
                Text = item.Name,
                FontSize = 12,
                Foreground = item.Done ? (Brush)FindResource("Brush.TextTertiary") : (Brush)FindResource("Brush.TextPrimary"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 8, 0),
            };
            Grid.SetColumn(name, 1);
            grid.Children.Add(name);

            var progress = new TextBlock
            {
                Text = $"{item.Process}/{item.Total}",
                FontSize = 10,
                Foreground = (Brush)FindResource("Brush.TextTertiary"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0),
            };
            Grid.SetColumn(progress, 2);
            grid.Children.Add(progress);

            var reward = new TextBlock
            {
                Text = item.Reward > 0 ? $"+{item.Reward}" : "",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Brush.Accent"),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            };
            Grid.SetColumn(reward, 3);
            grid.Children.Add(reward);

            MissionPanel.Children.Add(grid);
        }
    }

    private void ShowRolesEmpty(string text)
    {
        RolePanel.Children.Clear();
        TxtRoleBadge.Text = "0 个角色";
        RolePanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("Brush.TextTertiary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 6),
        });
    }

    private void ShowMissionsEmpty(string text, string badge)
    {
        MissionPanel.Children.Clear();
        TxtMissionBadge.Text = badge;
        MissionPanel.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 11.5,
            Foreground = (Brush)FindResource("Brush.TextTertiary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 6),
        });
    }

    private void AppendLog(SignLogEntry entry)
    {
        _logs.Add(entry);
        while (_logs.Count > 240) _logs.RemoveAt(0);
        TxtLogCount.Text = _logs.Count.ToString();
        Dispatcher.BeginInvoke(new Action(() => LogScroll.ScrollToEnd()), DispatcherPriority.Background);
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        AppendLog(new SignLogEntry { Text = "开始刷新账号与任务状态…", Level = "INFO" });
        await RefreshAllAsync();
    }

    private async void BtnMissionReload_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _account is null) return;
        BtnMissionReload.IsEnabled = false;
        try
        {
            AppendLog(new SignLogEntry { Text = "正在刷新米游社任务…", Level = "INFO" });
            await RefreshMissionsInternalAsync();
        }
        finally
        {
            BtnMissionReload.IsEnabled = true;
        }
    }

    private async void BtnMissionRun_Click(object sender, RoutedEventArgs e)
    {
        if (_account is null || _busy) return;
        BtnMissionRun.IsEnabled = false;
        try
        {
            AppendLog(new SignLogEntry { Text = "开始执行米游社每日任务…", Level = "INFO" });
            var check = await SignService.BbsCheckInAsync(_account.Cookie);
            foreach (var line in check.Log)
                AppendLog(new SignLogEntry { Text = line.Text, Level = check.Ok ? "OK" : line.Level });
            var actions = await SignService.DoCommunityActionsAsync(_account.Cookie);
            foreach (var line in actions)
                AppendLog(new SignLogEntry { Text = line.Text, Level = line.Level });
            await RefreshMissionsInternalAsync();
        }
        catch (Exception ex)
        {
            AppendLog(new SignLogEntry { Text = "执行任务失败：" + ex.Message, Level = "ERROR" });
        }
        finally
        {
            BtnMissionRun.IsEnabled = true;
        }
    }

    private async void BtnSignAll_Click(object sender, RoutedEventArgs e)
    {
        if (_account is null) return;
        BtnSignAll.IsEnabled = false;
        try
        {
            AppendLog(new SignLogEntry { Text = "开始一键签到当前账号…", Level = "INFO" });
            foreach (var row in _roleRows.Where(r => !r.Signed && !r.Busy && r.Game is not null))
            {
                await SignOneAsync(row);
                await Task.Delay(350);
            }
            await RefreshMissionsInternalAsync();
        }
        finally
        {
            BtnSignAll.IsEnabled = true;
        }
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        _logs.Clear();
        TxtLogCount.Text = "0";
    }
}


