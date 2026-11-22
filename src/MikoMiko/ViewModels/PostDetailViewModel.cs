using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MikoMiko.Models;
using MikoMiko.Services;
using System.Diagnostics;

namespace MikoMiko.ViewModels;

/// <summary>帖子详情（独立窗口）。</summary>
public sealed partial class PostDetailViewModel : ObservableObject
{
    private readonly ContentService _content;

    public PostDetailViewModel(FeedItem item, ContentService content)
    {
        _content = content;
        Detail = item;
    }

    [ObservableProperty] private FeedItem _detail;
    [ObservableProperty] private bool _isLoading;

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            Detail = await _content.GetDetailAsync(Detail);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (string.IsNullOrWhiteSpace(Detail.WebUrl)) return;
        try
        {
            Process.Start(new ProcessStartInfo(Detail.WebUrl) { UseShellExecute = true });
        }
        catch
        {
            // 忽略
        }
    }
}
