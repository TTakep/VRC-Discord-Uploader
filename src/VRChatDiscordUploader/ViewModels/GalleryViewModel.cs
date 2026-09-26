using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SkiaSharp;
using VRChatDiscordUploader.Services;

namespace VRChatDiscordUploader.ViewModels;

public partial class GalleryPhotoItem : ObservableObject
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public string FormattedDate => CapturedAt.ToString("yyyy/MM/dd HH:mm:ss");
    public long FileSizeBytes { get; set; }

    public string FormattedSize => FileSizeBytes switch
    {
        >= 1024 * 1024 => $"{(double)FileSizeBytes / (1024 * 1024):F2} MB",
        >= 1024 => $"{(double)FileSizeBytes / 1024:F1} KB",
        _ => $"{FileSizeBytes} B"
    };

    [ObservableProperty]
    private string _dimensions = "読み込み中...";

    [ObservableProperty]
    private string _worldName = "ログ確認中...";

    [ObservableProperty]
    private string _worldId = string.Empty;

    public string WorldUrl => !string.IsNullOrWhiteSpace(WorldId)
        ? $"https://vrchat.com/home/world/{WorldId}"
        : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedPlayers))]
    private ObservableCollection<string> _players = new();

    public string FormattedPlayers => Players.Count > 0 ? string.Join(", ", Players) : "（記録なし）";

    public void SetPlayers(IEnumerable<string> players)
    {
        Players = new ObservableCollection<string>(players);
    }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isDetailsLoaded;

    [ObservableProperty]
    private bool _isLoadingDetails;
}

public partial class GalleryViewModel : ObservableObject
{
    private readonly ConfigurationService _configService;
    private readonly PhotoBatchService _photoBatch;
    private readonly VRCLogParserService _logParser;

    [ObservableProperty]
    private ObservableCollection<string> _availableMonths = new();

    [ObservableProperty]
    private string? _selectedMonth;

    [ObservableProperty]
    private ObservableCollection<GalleryPhotoItem> _photos = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUpload))]
    private bool _isUploading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _imageOnly;

    [ObservableProperty]
    private GalleryPhotoItem? _selectedPhotoForDetail;

    // キュー・進捗の可視化用プロパティ
    [ObservableProperty]
    private string _batchStatusText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BatchProgressVisibility))]
    [NotifyPropertyChangedFor(nameof(CanUpload))]
    private bool _isBatchActive;

    [ObservableProperty]
    private double _batchProgressValue;

    [ObservableProperty]
    private bool _batchProgressIsIndeterminate;

    public Microsoft.UI.Xaml.Visibility BatchProgressVisibility => IsBatchActive ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public bool CanUpload => !IsBatchActive && !IsUploading;

    public int SelectedPhotosCount => Photos.Count(p => p.IsSelected);

    public GalleryViewModel(
        ConfigurationService configService,
        PhotoBatchService photoBatch,
        VRCLogParserService logParser)
    {
        _configService = configService;
        _photoBatch = photoBatch;
        _logParser = logParser;
        _imageOnly = _configService.CurrentConfig.UploadBehavior.ImageOnly;

        _photoBatch.StatusChanged += (s, e) =>
        {
            App.CurrentWindowDispatcher?.TryEnqueue(() =>
            {
                IsBatchActive = e.IsActive;
                BatchStatusText = e.StatusText;
                BatchProgressValue = e.ProgressPercentage;
                BatchProgressIsIndeterminate = e.IsActive && e.TotalCount == 0;
                StatusMessage = e.StatusText;
            });
        };

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
        var loadedItems = await Task.Run(() =>
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
                    FileSizeBytes = fi.Exists ? fi.Length : 0,
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
        });

        foreach (var item in loadedItems)
        {
            Photos.Add(item);
        }
        StatusMessage = $"{Photos.Count}枚の写真を読み込みました。";
        IsLoading = false;
    }

    public async Task LoadPhotoDetailsAsync(GalleryPhotoItem item)
    {
        if (item.IsDetailsLoaded || item.IsLoadingDetails) return;

        item.IsLoadingDetails = true;
        try
        {
            var (dimensions, worldName, worldId, players) = await Task.Run(() =>
            {
                string dim = "不明";
                try
                {
                    using var codec = SKCodec.Create(item.FilePath);
                    if (codec != null)
                    {
                        dim = $"{codec.Info.Width} × {codec.Info.Height}";
                    }
                }
                catch { }

                string wName = "（記録なし）";
                string wId = string.Empty;
                List<string> pList = new();

                try
                {
                    var meta = _logParser.ExtractPhotoMetadata(item.FilePath, isRealtime: false);
                    if (!string.IsNullOrWhiteSpace(meta.WorldName))
                    {
                        wName = meta.WorldName;
                    }
                    wId = meta.WorldId;
                    pList = meta.PlayersInRoom;
                }
                catch { }

                return (dim, wName, wId, pList);
            });

            // UIスレッドで一括更新
            item.Dimensions = dimensions;
            item.WorldName = worldName;
            item.WorldId = worldId;
            item.SetPlayers(players);
            item.IsDetailsLoaded = true;
        }
        catch (Exception ex)
        {
            App.Log($"写真詳細の読み込みエラー: {ex.Message}");
        }
        finally
        {
            item.IsLoadingDetails = false;
        }
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
        StatusMessage = $"{selected.Count}枚の写真を送信キューへ投入中...";

        try
        {
            await _photoBatch.EnqueueManualPhotosAsync(selected, ImageOnly);
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

    [RelayCommand]
    public async Task UploadSinglePhotoAsync(GalleryPhotoItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.FilePath)) return;

        IsUploading = true;
        StatusMessage = $"「{item.FileName}」を送信中...";

        try
        {
            await _photoBatch.EnqueueManualPhotosAsync(new[] { item.FilePath }, ImageOnly);
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
