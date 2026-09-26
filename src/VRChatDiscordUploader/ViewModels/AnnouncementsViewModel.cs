using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VRChatDiscordUploader.Models;
using VRChatDiscordUploader.Services;

namespace VRChatDiscordUploader.ViewModels;

public partial class AnnouncementsViewModel : ObservableObject
{
    private readonly AnnouncementService _announcementService;

    [ObservableProperty]
    private ObservableCollection<AnnouncementItem> _announcements = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private UpdateCheckResult? _updateResult;

    [ObservableProperty]
    private string _updateStatusText = "更新を確認していません";

    public AnnouncementsViewModel(AnnouncementService announcementService)
    {
        _announcementService = announcementService;
        _ = LoadAnnouncementsAsync();
        _ = CheckForUpdatesAsync();
    }

    [RelayCommand]
    public async Task LoadAnnouncementsAsync()
    {
        IsLoading = true;
        Announcements.Clear();

        try
        {
            var items = await _announcementService.GetAnnouncementsAsync();
            foreach (var item in items)
            {
                Announcements.Add(item);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        UpdateStatusText = "更新を確認中...";
        try
        {
            UpdateResult = await _announcementService.CheckForUpdatesAsync();
            if (UpdateResult.HasUpdate)
            {
                UpdateStatusText = $"新バージョン {UpdateResult.LatestVersion} が利用可能です。";
            }
            else
            {
                UpdateStatusText = "最新バージョンを利用中です。";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"確認エラー: {ex.Message}";
        }
    }

    [RelayCommand]
    public void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // URL起動失敗時は無視
        }
    }
}
