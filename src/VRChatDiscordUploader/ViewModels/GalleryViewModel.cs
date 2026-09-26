using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VRChatDiscordUploader.Services;

namespace VRChatDiscordUploader.ViewModels;

public partial class GalleryPhotoItem : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public string FormattedDate => CapturedAt.ToString("yyyy/MM/dd HH:mm:ss");

    [ObservableProperty]
    private bool _isSelected;
}

public partial class GalleryViewModel : ObservableObject
{
    private readonly ConfigurationService _configService;
    private readonly PhotoBatchService _photoBatch;

    [ObservableProperty]
    private ObservableCollection<string> _availableMonths = new();

    [ObservableProperty]
    private string? _selectedMonth;

    [ObservableProperty]
    private ObservableCollection<GalleryPhotoItem> _photos = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isUploading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public int SelectedPhotosCount => Photos.Count(p => p.IsSelected);

    public GalleryViewModel(ConfigurationService configService, PhotoBatchService photoBatch)
    {
        _configService = configService;
        _photoBatch = photoBatch;
        LoadAvailableMonths();
    }

    public void LoadAvailableMonths()
    {
        var watchDir = _configService.GetEffectiveWatchDirectory();
        AvailableMonths.Clear();

        if (Directory.Exists(watchDir))
        {
            var dirs = Directory.GetDirectories(watchDir)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrEmpty(name))
                .OrderByDescending(name => name)
                .ToList();

            foreach (var d in dirs)
            {
                if (d != null)
                {
                    AvailableMonths.Add(d);
                }
            }

            if (AvailableMonths.Count > 0 && SelectedMonth == null)
            {
                SelectedMonth = AvailableMonths[0];
            }
        }
    }

    partial void OnSelectedMonthChanged(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = LoadPhotosForMonthAsync(value);
        }
    }

    public async Task LoadPhotosForMonthAsync(string month)
    {
        var watchDir = _configService.GetEffectiveWatchDirectory();
        var targetDir = Path.Combine(watchDir, month);

        if (!Directory.Exists(targetDir)) return;

        IsLoading = true;
        StatusMessage = "写真を読み込み中...";
        Photos.Clear();

        await Task.Run(() =>
        {
            var files = Directory.GetFiles(targetDir, "*.*")
                .Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext is ".png" or ".jpg" or ".jpeg";
                })
                .OrderByDescending(f => File.GetCreationTime(f))
                .ToList();

            var items = files.Select(f =>
            {
                var fi = new FileInfo(f);
                var item = new GalleryPhotoItem
                {
                    FilePath = f,
                    FileName = fi.Name,
                    CapturedAt = fi.CreationTime,
                    IsSelected = false
                };
                item.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(GalleryPhotoItem.IsSelected))
                    {
                        OnPropertyChanged(nameof(SelectedPhotosCount));
                    }
                };
                return item;
            }).ToList();

            return items;
        }).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
            {
                foreach (var item in t.Result)
                {
                    Photos.Add(item);
                }
                StatusMessage = $"{Photos.Count}枚の写真を読み込みました。";
            }
            IsLoading = false;
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    [RelayCommand]
    public void SelectAll()
    {
        foreach (var p in Photos)
        {
            p.IsSelected = true;
        }
        OnPropertyChanged(nameof(SelectedPhotosCount));
    }

    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var p in Photos)
        {
            p.IsSelected = false;
        }
        OnPropertyChanged(nameof(SelectedPhotosCount));
    }

    [RelayCommand]
    public async Task UploadSelectedPhotosAsync()
    {
        var selected = Photos.Where(p => p.IsSelected).Select(p => p.FilePath).ToList();
        if (selected.Count == 0)
        {
            StatusMessage = "写真が選択されていません。";
            return;
        }

        IsUploading = true;
        StatusMessage = $"{selected.Count}枚の写真を送信中...";

        try
        {
            await _photoBatch.EnqueueManualPhotosAsync(selected);
            StatusMessage = $"{selected.Count}枚の写真を送信キューへ投入しました。";
            DeselectAll();
        }
        catch (Exception ex)
        {
            StatusMessage = $"エラーが発生しました: {ex.Message}";
        }
        finally
        {
            IsUploading = false;
        }
    }
}
