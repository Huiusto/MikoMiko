using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MikoMiko.Models;
using MikoMiko.Services;
using Button = System.Windows.Controls.Button;
using Cursors = System.Windows.Input.Cursors;
using Brushes = System.Windows.Media.Brushes;

namespace MikoMiko.Views;

/// <summary>每日任务：支持多个米游社账号，逐账号完成米游币任务与游戏角色签到。</summary>
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

    private sealed class AccountRow
    {
        public required HoyoAccount Account;
        public List<SignRole> Roles = new();
        public required Border RowBorder;
        public required Border Avatar;
        public required TextBlock Name;
        public required TextBlock Meta;
        public required TextBlock Status;
        public required Button Run;
        public string? Error;
        public int RoleCount;
        public int SignedCount;
        public int Points;
        public bool Busy;
    }

    private sealed record AccountLoadResult(HoyoAccount Account, List<SignRole> Roles, string? Error, bool LoginExpired);

    private static readonly Dictionary<string, Color> GameColors = new()
    {
        ["hk4e"] = Color.FromRgb(0x4E, 0xA4, 0xDD),
        ["hkrpg"] = Color.FromRgb(0x8A, 0x6D, 0xF0),
        ["nap"] = Color.FromRgb(0xFF, 0x8A, 0x3D),
        ["bh3"] = Color.FromRgb(0xD9, 0x6B, 0xC7),
    };

    private readonly ObservableCollection<SignLogEntry> _logs = new();
    private readonly List<AccountRow> _accountRows = new();
    private readonly List<RoleRow> _roleRows = new();
    private HoyoAccount? _account;
    private bool _loadedOnce;
    private bool _busy;
    private bool _runningAll;

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
            AppendLog(new SignLogEntry { Text = "正在读取米游社多账号…", Level = "INFO" });
            await RefreshAllAsync();
            return;
        }
        if (_accountRows.Count == 0) await RefreshAllAsync();
    }

    private async Task RefreshAllAsync()
    {
        if (_busy) return;
        _busy = true;
        BtnRefresh.IsEnabled = false;
        BtnRunAll.IsEnabled = false;
        try
        {
            HoyoAccountService.Reload();
            var accounts = HoyoAccountService.All.ToList();
            TxtAccountBadge.Text = $"{accounts.Count} 个账号";
            if (accounts.Count == 0)
            {
                AccountPanel.Children.Clear();
                _accountRows.Clear();
                _account = null;
                ShowRolesEmpty("还没有添加米游社账号，请点击右上角「添加账号」。");
                ShowMissionsEmpty("未登录，无法读取米游社任务。", "需要先登录");
                TxtSubtitle.Text = "米游社每日任务 · 请先添加账号";
                TxtCurrent.Text = "";
                BtnRunAll.IsEnabled = false;
                AppendLog(new SignLogEntry
                {
                    Text = "没有可用的米游社账号，请点击右上角「添加账号」扫码或粘贴 Cookie。",
                    Level = "ERROR",
                });
                return;
            }

            AppendLog(new SignLogEntry { Text = $"读取到 {accounts.Count} 个账号，正在并行拉取角色…", Level = "INFO" });
            var results = await Task.WhenAll(accounts.Select(LoadAccountRolesAsync));
            BuildAccountRows(results.ToList());

            var active = HoyoAccountService.Active ?? accounts[0];
            var activeRow = _accountRows.FirstOrDefault(r => r.Account.Uid == active.Uid) ?? _accountRows[0];
            await SelectAccountAsync(activeRow, false);
            BtnRunAll.IsEnabled = true;
        }
        catch (Exception ex)
        {
            AppendLog(new SignLogEntry { Text = "刷新失败：" + ex.Message, Level = "ERROR" });
        }
        finally
        {
            _busy = false;
            BtnRefresh.IsEnabled = true;
            if (_accountRows.Count > 0) BtnRunAll.IsEnabled = true;
        }
    }

    private static async Task<AccountLoadResult> LoadAccountRolesAsync(HoyoAccount account)
    {
        if (string.IsNullOrWhiteSpace(account.Cookie))
            return new AccountLoadResult(account, new List<SignRole>(), "Cookie 缺失，请重新添加账号。", true);
        try
        {
            var result = await SignService.GetAllRolesAsync(account.Cookie);
            if (result.Error is not null || result.LoginExpired)
                return new AccountLoadResult(account, result.Roles,
                    result.Error ?? "登录状态已失效，请重新扫码或更新 Cookie。", result.LoginExpired);
            return new AccountLoadResult(account, result.Roles, null, false);
        }
        catch (Exception ex)
        {
            return new AccountLoadResult(account, new List<SignRole>(), "读取角色失败：" + ex.Message, false);
        }
    }

    private void BuildAccountRows(List<AccountLoadResult> results)
    {
        AccountPanel.Children.Clear();
        _accountRows.Clear();
        foreach (var result in results)
        {
            var account = result.Account;
            var row = new Border
            {
                CornerRadius = new CornerRadius(11),
                Background = (Brush)FindResource("Brush.Surface"),
                BorderBrush = (Brush)FindResource("Brush.Border"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(11, 9, 10, 9),
                Margin = new Thickness(0, 0, 0, 7),
                Cursor = Cursors.Hand,
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var avatarText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(account.Nickname) ? "米" : account.Nickname[..1],
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Brush.Accent"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var avatarGrid = new Grid();
            avatarGrid.Children.Add(avatarText);
            if (!string.IsNullOrWhiteSpace(account.AvatarUrl))
            {
                var avatarImage = new Image
                {
                    Stretch = System.Windows.Media.Stretch.UniformToFill,
                    Clip = new System.Windows.Media.EllipseGeometry(
                        new System.Windows.Point(17, 17), 17, 17),
                };
                MikoMiko.Controls.AsyncImage.SetUrl(avatarImage, account.AvatarUrl);
                avatarGrid.Children.Add(avatarImage);
            }
            var avatar = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = new SolidColorBrush(Color.FromRgb(0xE3, 0xF4, 0xFE)),
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                Child = avatarGrid,
            };
            Grid.SetColumn(avatar, 0);
            grid.Children.Add(avatar);

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var name = new TextBlock
            {
                Text = account.DisplayName,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("Brush.TextPrimary"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(name, 0);
            top.Children.Add(name);
            var status = new TextBlock
            {
                Text = "就绪",
                FontSize = 10.5,
                Foreground = (Brush)FindResource("Brush.Success"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
            };
            Grid.SetColumn(status, 1);
            top.Children.Add(status);
            info.Children.Add(top);

            var meta = new TextBlock
            {
                Text = result.Error is null ? $"{result.Roles.Count} 个角色 · 等待执行" : result.Error,
                FontSize = 10,
                Foreground = result.Error is null
                    ? (Brush)FindResource("Brush.TextTertiary")
                    : (Brush)FindResource("Brush.Danger"),
                Margin = new Thickness(0, 3, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = result.Error,
            };
            info.Children.Add(meta);
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);

            var run = new Button
            {
                Content = "执行",
                Style = (Style)FindResource("TaskButton"),
                Padding = new Thickness(13, 5, 13, 5),
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = result.Error is null || !result.LoginExpired,
            };
            Grid.SetColumn(run, 2);
            grid.Children.Add(run);

            row.Child = grid;
            var accountRow = new AccountRow
            {
                Account = account,
                Roles = result.Roles,
                RowBorder = row,
                Avatar = (Border)grid.Children[0],
                Name = name,
                Meta = meta,
                Status = status,
                Run = run,
                Error = result.Error,
                RoleCount = result.Roles.Count,
            };
            if (result.Error is not null)
            {
                status.Text = "登录失效";
                status.Foreground = (Brush)FindResource("Brush.Danger");
            }
            else if (result.LoginExpired)
            {
                status.Text = "需重新登录";
                status.Foreground = (Brush)FindResource("Brush.Danger");
            }

            run.Click += async (_, _) => await ExecuteAccountAsync(accountRow);
            row.MouseLeftButtonUp += async (_, _) =>
            {
                if (accountRow.Busy) return;
                await SelectAccountAsync(accountRow, true);
            };

            _accountRows.Add(accountRow);
            AccountPanel.Children.Add(row);
        }
    }

    private async Task SelectAccountAsync(AccountRow row, bool reloadMissions)
    {
        HoyoAccountService.SetActive(row.Account.Uid);
        _account = HoyoAccountService.Active ?? row.Account;
        TxtCurrent.Text = $"当前：{_account.Nickname}";
        TxtMissionOwner.Text = $"当前账号：{_account.DisplayName}";
        TxtSubtitle.Text = $"已登录 {_accountRows.Count} 个账号 · 当前 {_account.DisplayName}";

        foreach (var item in _accountRows)
        {
            var active = item.Account.Uid == _account.Uid;
            item.RowBorder.Background = new SolidColorBrush(active
                ? Color.FromRgb(0xEC, 0xF7, 0xFE)
                : ((SolidColorBrush)FindResource("Brush.Surface")).Color);
            item.RowBorder.BorderBrush = active
                ? (Brush)FindResource("Brush.Accent")
                : (Brush)FindResource("Brush.Border");
            item.RowBorder.BorderThickness = new Thickness(active ? 1.5 : 1);
        }

        BuildRoles(row.Roles);
        foreach (var roleRow in _roleRows.Take(30))
            _ = LoadRoleStateAsync(roleRow);
        if (reloadMissions || MissionPanel.Children.Count == 0)
            await RefreshMissionsInternalAsync();
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
        var activeRow = _accountRows.FirstOrDefault(r => r.Account.Uid == _account?.Uid);
        if (activeRow is not null)
        {
            activeRow.Points = snapshot.UserPoints;
            UpdateAccountMeta(activeRow);
        }

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
                Background = item.Done ? (Brush)FindResource("Brush.Success") : (Brush)FindResource("Brush.Surface"),
                BorderBrush = item.Done ? Brushes.Transparent : (Brush)FindResource("Brush.Border"),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = item.Done ? "✓" : "",
                    FontSize = 10,
                    Foreground = Brushes.White,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            Grid.SetColumn(mark, 0);
            grid.Children.Add(mark);

            var name = new TextBlock
            {
                Text = item.Name,
                FontSize = 12,
                Foreground = item.Done ? (Brush)FindResource("Brush.TextTertiary") : (Brush)FindResource("Brush.TextPrimary"),
                VerticalAlignment = VerticalAlignment.Center,
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
                VerticalAlignment = VerticalAlignment.Center,
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
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            };
            Grid.SetColumn(reward, 3);
            grid.Children.Add(reward);

            MissionPanel.Children.Add(grid);
        }
    }

    private void BuildRoles(List<SignRole> roles)
    {
        RolePanel.Children.Clear();
        _roleRows.Clear();
        TxtRoleBadge.Text = $"{roles.Count} 个角色";
        if (roles.Count == 0)
        {
            ShowRolesEmpty("当前账号下没有读取到游戏角色，请确认账号已绑定游戏角色。");
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
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(role.Nickname) ? "角" : role.Nickname[..1],
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(color),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            Grid.SetColumn(avatar, 0);
            grid.Children.Add(avatar);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
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
                VerticalAlignment = VerticalAlignment.Center,
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
                VerticalAlignment = VerticalAlignment.Center,
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
    }

    private async Task<SignService.ActionResult> SignRoleWithVerifyAsync(HoyoAccount account,
        SignService.GameCheckin game, SignRole role)
    {
        var result = await SignService.SignRoleAsync(account.Cookie, game, role);
        if (result.Retcode != 1034) return result;

        AppendLog(new SignLogEntry { Text = $"{game.Name} 触发验证码，已打开软件内安全验证", Level = "WARN" });
        var completed = VerifyDialog.Show(Window.GetWindow(this), account, result.Aigis);
        if (!completed)
        {
            AppendLog(new SignLogEntry { Text = "已取消安全验证，本次签到未完成", Level = "WARN" });
            return result;
        }

        AppendLog(new SignLogEntry { Text = "验证完成，正在重试签到…", Level = "INFO" });
        return await SignService.SignRoleAsync(account.Cookie, game, role);
    }

    private async Task<SignService.ActionResult> BbsCheckInWithVerifyAsync(HoyoAccount account, string cookie)
    {
        var result = await SignService.BbsCheckInAsync(cookie);
        if (result.Retcode != 1034) return result;

        AppendLog(new SignLogEntry { Text = "米游社打卡触发验证码，已打开软件内安全验证", Level = "WARN" });
        var completed = VerifyDialog.Show(Window.GetWindow(this), account, result.Aigis);
        if (!completed)
        {
            AppendLog(new SignLogEntry { Text = "已取消安全验证，本次打卡未完成", Level = "WARN" });
            return result;
        }

        AppendLog(new SignLogEntry { Text = "验证完成，正在重试米游社打卡…", Level = "INFO" });
        return await SignService.BbsCheckInAsync(cookie);
    }

    private async Task SignOneAsync(RoleRow row)
    {
        if (_account is null || row.Game is null || row.Busy) return;
        row.Busy = true;
        row.Action.IsEnabled = false;
        row.Action.Content = "签到中";
        try
        {
            var result = await SignRoleWithVerifyAsync(_account, row.Game, row.Role);
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

    private async Task ExecuteAccountAsync(AccountRow row, bool fromAll = false)
    {
        if ((_runningAll && !fromAll) || row.Busy) return;
        row.Busy = true;
        row.Run.IsEnabled = false;
        row.Run.Content = "执行中";
        row.Status.Text = "执行中…";
        row.Status.Foreground = (Brush)FindResource("Brush.Accent");
        try
        {
            AppendLog(new SignLogEntry { Text = $"===== {row.Account.DisplayName} =====", Level = "INFO" });
            var cookie = row.Account.Cookie;
            if (string.IsNullOrWhiteSpace(cookie))
            {
                row.Error = "Cookie 缺失";
                row.Status.Text = "Cookie 缺失";
                row.Status.Foreground = (Brush)FindResource("Brush.Danger");
                return;
            }

            var check = await BbsCheckInWithVerifyAsync(row.Account, cookie);
            foreach (var line in check.Log)
                AppendLog(new SignLogEntry { Text = line.Text, Level = check.Ok ? "OK" : line.Level });

            var actions = await SignService.DoCommunityActionsAsync(cookie);
            foreach (var line in actions)
                AppendLog(new SignLogEntry { Text = line.Text, Level = line.Level });

            var roles = row.Roles;
            if (roles.Count == 0)
            {
                var roleResult = await SignService.GetAllRolesAsync(cookie);
                roles = roleResult.Roles;
                row.Roles = roles;
                row.RoleCount = roles.Count;
            }

            var success = 0;
            var attempted = 0;
            foreach (var role in roles)
            {
                var game = SignService.GetByBiz(role.GameBiz);
                if (game is not { CheckinEnabled: true }) continue;
                attempted++;
                var result = await SignRoleWithVerifyAsync(row.Account, game, role);
                if (result.Ok) success++;
                foreach (var line in result.Log)
                    AppendLog(new SignLogEntry { Text = line.Text, Level = line.Level });
                await Task.Delay(220);
            }
            row.SignedCount = success;
            row.RoleCount = roles.Count;
            var snapshot = await SignService.GetMissionsAsync(cookie);
            if (snapshot.snapshot is not null) row.Points = snapshot.snapshot.UserPoints;
            row.Status.Text = attempted == 0 ? "任务完成" : $"完成 {success}/{attempted}";
            row.Status.Foreground = attempted > 0 && success < attempted
                ? (Brush)FindResource("Brush.Danger")
                : (Brush)FindResource("Brush.Success");
            AppendLog(new SignLogEntry
            {
                Text = $"{row.Account.Nickname} 完成：打卡/浏览已执行，角色签到 {success}/{attempted}",
                Level = success == attempted ? "OK" : "WARN",
            });

            if (_account?.Uid == row.Account.Uid)
            {
                BuildRoles(roles);
                await RefreshMissionsInternalAsync();
            }
            UpdateAccountMeta(row);
        }
        catch (Exception ex)
        {
            row.Status.Text = "执行失败";
            row.Status.Foreground = (Brush)FindResource("Brush.Danger");
            AppendLog(new SignLogEntry { Text = "执行账号任务失败：" + ex.Message, Level = "ERROR" });
        }
        finally
        {
            row.Busy = false;
            row.Run.IsEnabled = true;
            row.Run.Content = "执行";
        }
    }

    private void UpdateAccountMeta(AccountRow row)
    {
        var parts = new List<string> { $"{row.RoleCount} 个角色" };
        if (row.Points > 0) parts.Add($"米游币 {row.Points}");
        if (row.SignedCount > 0) parts.Add($"签到 {row.SignedCount}");
        if (row.Error is not null) parts.Add(row.Error);
        row.Meta.Text = string.Join(" · ", parts);
        row.Meta.Foreground = row.Error is null
            ? (Brush)FindResource("Brush.TextTertiary")
            : (Brush)FindResource("Brush.Danger");
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
        while (_logs.Count > 260) _logs.RemoveAt(0);
        TxtLogCount.Text = _logs.Count.ToString();
        Dispatcher.BeginInvoke(new Action(() => LogScroll.ScrollToEnd()), DispatcherPriority.Background);
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        AppendLog(new SignLogEntry { Text = "开始刷新账号与任务状态…", Level = "INFO" });
        await RefreshAllAsync();
    }

    private async void BtnAddAccount_Click(object sender, RoutedEventArgs e)
    {
        var window = new AddAccountWindow { Owner = Window.GetWindow(this) };
        if (window.ShowDialog() == true)
        {
            AppendLog(new SignLogEntry { Text = "账号已添加，正在刷新多账号列表…", Level = "OK" });
            await RefreshAllAsync();
        }
    }

    private async void BtnMissionReload_Click(object sender, RoutedEventArgs e)
    {
        if (_account is null) return;
        BtnMissionReload.IsEnabled = false;
        try
        {
            AppendLog(new SignLogEntry { Text = "正在刷新当前账号的米游社任务…", Level = "INFO" });
            await RefreshMissionsInternalAsync();
        }
        finally
        {
            BtnMissionReload.IsEnabled = true;
        }
    }

    private async void BtnRunAll_Click(object sender, RoutedEventArgs e)
    {
        if (_runningAll || _accountRows.Count == 0) return;
        _runningAll = true;
        BtnRunAll.IsEnabled = false;
        BtnAddAccount.IsEnabled = false;
        BtnRefresh.IsEnabled = false;
        foreach (var row in _accountRows) row.Run.IsEnabled = false;
        try
        {
            AppendLog(new SignLogEntry
            {
                Text = $"开始一键执行 { _accountRows.Count } 个账号的每日任务…",
                Level = "INFO",
            });
            foreach (var row in _accountRows.ToList())
            {
                if (row.Busy) continue;
                await ExecuteAccountAsync(row, true);
                await Task.Delay(250);
            }
            AppendLog(new SignLogEntry { Text = "所有账号执行完成。", Level = "OK" });
        }
        finally
        {
            _runningAll = false;
            BtnRunAll.IsEnabled = true;
            BtnAddAccount.IsEnabled = true;
            BtnRefresh.IsEnabled = true;
            foreach (var row in _accountRows) row.Run.IsEnabled = true;
        }
    }

    private async void BtnSignAll_Click(object sender, RoutedEventArgs e)
    {
        if (_account is null || _busy) return;
        BtnSignAll.IsEnabled = false;
        try
        {
            AppendLog(new SignLogEntry { Text = "开始一键签到当前账号…", Level = "INFO" });
            foreach (var row in _roleRows.Where(r => !r.Signed && !r.Busy && r.Game is not null))
            {
                await SignOneAsync(row);
                await Task.Delay(250);
            }
            var activeRow = _accountRows.FirstOrDefault(r => r.Account.Uid == _account.Uid);
            if (activeRow is not null)
            {
                activeRow.SignedCount = _roleRows.Count(r => r.Signed);
                UpdateAccountMeta(activeRow);
            }
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
