using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using VRChatDiscordUploader.Models;

namespace VRChatDiscordUploader.Services;

public class HistoryManagerService
{
    private static readonly string HistoryFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VRCDiscordUploader",
        "history.json"
    );

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly List<UploadHistoryItem> _historyItems = new();
    private readonly object _lock = new();

    public event EventHandler<UploadHistoryItem>? HistoryItemAdded;

    public HistoryManagerService()
    {
        LoadHistory();
    }

    public IReadOnlyList<UploadHistoryItem> GetHistory()
    {
        lock (_lock)
        {
            return _historyItems.ToArray();
        }
    }

    public void AddItem(UploadHistoryItem item)
    {
        lock (_lock)
        {
            _historyItems.Insert(0, item);
            // 最大200件まで保持
            if (_historyItems.Count > 200)
            {
                _historyItems.RemoveRange(200, _historyItems.Count - 200);
            }
            SaveHistory();
        }

        HistoryItemAdded?.Invoke(this, item);
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            _historyItems.Clear();
            SaveHistory();
        }
    }

    private void LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryFilePath))
            {
                var json = File.ReadAllText(HistoryFilePath, Encoding.UTF8);
                var items = JsonSerializer.Deserialize<List<UploadHistoryItem>>(json, JsonOptions);
                if (items != null)
                {
                    _historyItems.AddRange(items);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"履歴の読み込みに失敗しました: {ex.Message}");
        }
    }

    private void SaveHistory()
    {
        try
        {
            var dir = Path.GetDirectoryName(HistoryFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_historyItems, JsonOptions);
            File.WriteAllText(HistoryFilePath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"履歴の保存に失敗しました: {ex.Message}");
        }
    }
}
