using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using VRChatDiscordUploader.Models;
using VRChatDiscordUploader.Services;

namespace VRChatDiscordUploader.ViewModels;

public partial class QueueViewModel : ObservableObject
{
    private readonly PhotoBatchService _photoBatch;

    public ObservableCollection<QueueItem> QueueItems => _photoBatch.QueueItems;

    [ObservableProperty]
    private string _currentStatusText = "キュー待機中";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressVisibility))]
    private bool _isActive;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private bool _isIndeterminate;

    public Visibility ProgressVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EmptyStateVisibility => QueueItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ContentVisibility => QueueItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public int TotalCount => QueueItems.Count;
    public int ProcessingCount => QueueItems.Count(q => q.IsProcessing);
    public int QueuedCount => QueueItems.Count(q => q.Status == QueueItemStatus.Queued);
    public int CompletedCount => QueueItems.Count(q => q.IsCompleted);

    public QueueViewModel(PhotoBatchService photoBatch)
    {
        _photoBatch = photoBatch;

        _photoBatch.StatusChanged += (s, e) =>
        {
            App.CurrentWindowDispatcher?.TryEnqueue(() =>
            {
                IsActive = e.IsActive;
                CurrentStatusText = e.StatusText;
                ProgressValue = e.ProgressPercentage;
                IsIndeterminate = e.IsActive && e.TotalCount == 0;
                NotifyCounters();
            });
        };

        _photoBatch.QueueItems.CollectionChanged += (s, e) =>
        {
            App.CurrentWindowDispatcher?.TryEnqueue(() =>
            {
                NotifyCounters();
            });
        };
    }

    private void NotifyCounters()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(ProcessingCount));
        OnPropertyChanged(nameof(QueuedCount));
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ContentVisibility));
    }

    [RelayCommand]
    public void ClearCompleted()
    {
        _photoBatch.ClearCompletedQueue();
    }
}
