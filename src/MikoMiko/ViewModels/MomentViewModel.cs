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
    private List<MomentItem> _all = new();

    public MomentViewModel(ContentService content)
    {
        _content = content;
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
