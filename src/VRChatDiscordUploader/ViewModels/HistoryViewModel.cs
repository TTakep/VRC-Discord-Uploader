using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VRChatDiscordUploader.Models;
using VRChatDiscordUploader.Services;

namespace VRChatDiscordUploader.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly HistoryManagerService _historyManager;

    [ObservableProperty]
    private ObservableCollection<UploadHistoryItem> _historyItems = new();

    [ObservableProperty]
    private UploadHistoryItem? _selectedItem;

    public HistoryViewModel(HistoryManagerService historyManager)
    {
        _historyManager = historyManager;
        LoadHistory();

        _historyManager.HistoryItemAdded += (s, item) =>
        {
            App.CurrentWindowDispatcher?.TryEnqueue(() =>
            {
                HistoryItems.Insert(0, item);
            });
        };
    }

    private void LoadHistory()
    {
        HistoryItems.Clear();
        foreach (var item in _historyManager.GetHistory())
        {
            HistoryItems.Add(item);
        }
    }

    [RelayCommand]
    public void ClearHistory()
    {
        _historyManager.ClearHistory();
        HistoryItems.Clear();
        SelectedItem = null;
    }
}
