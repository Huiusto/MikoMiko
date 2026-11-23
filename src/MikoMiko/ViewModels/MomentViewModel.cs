using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Collections.ObjectModel;

namespace MikoMiko.ViewModels;

/// <summary>动态页：关注的人发布的内容。</summary>
public sealed partial class MomentViewModel : ObservableObject
{
    private readonly ContentService _content;
    private readonly Action<MomentItem> _openMoment;
    private List<MomentItem> _all = new();

    public MomentViewModel(ContentService content, Action<MomentItem> openMoment)
    {
        _content = content;
        _openMoment = openMoment;
    }

    public ObservableCollection<MomentItem> Moments { get; } = new();

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isDemo;
    [ObservableProperty] private bool _onlyFollowing;
    [ObservableProperty] private string _filter = string.Empty;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _needsLogin;

    public bool ShowAll => !OnlyFollowing;

    partial void OnOnlyFollowingChanged(bool value) => OnPropertyChanged(nameof(ShowAll));

    public async Task LoadAsync()
    {
        IsLoading = true;
        NeedsLogin = !_content.Api.HasCookie;
        if (NeedsLogin)
        {
            _all.Clear();
            Moments.Clear();
            IsEmpty = false;
            IsLoading = false;
            return;
        }

        try
        {
            _all = (await _content.GetMomentsAsync()).ToList();
            IsDemo = _content.IsDemo;
            OnlyFollowing = true;
            Apply();
        }
        catch
        {
            IsEmpty = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    [RelayCommand]
    private void Open(MomentItem? item)
    {
        if (item is not null) _openMoment(item);
    }

    [RelayCommand]
    private async Task ToggleLikeAsync(MomentItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.PostId)) return;
        var target = !item.IsLiked;
        if (!await _content.Api.LikePostAsync(item.PostId)) return;
        item.IsLiked = target;
        item.LikeText = BumpCount(item.LikeText, target);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(MomentItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.PostId)) return;
        if (!await _content.Api.FavoritePostAsync(item.PostId)) return;
        item.IsFavorited = !item.IsFavorited;
    }

    private static string BumpCount(string text, bool up)
    {
        var number = double.TryParse((text ?? string.Empty).Trim(), out var n) ? n : 0;
        number = Math.Max(0, number + (up ? 1 : -1));
        return number <= 0 ? string.Empty : ((long)number).ToString();
    }

    [RelayCommand]
    private void ToggleOnlyFollowing()
    {
        OnlyFollowing = !OnlyFollowing;
        Apply();
    }

    public void ApplySearch(string text)
    {
        Filter = text ?? string.Empty;
        Apply();
    }

    private void Apply()
    {
        var keyword = Filter.Trim();
        Moments.Clear();
        var list = _all.Where(m => !OnlyFollowing || m.IsFollowing);
        if (!string.IsNullOrEmpty(keyword))
        {
            list = list.Where(m => m.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                                   || m.Author.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var item in list) Moments.Add(item);
        IsEmpty = Moments.Count == 0;
    }
}
