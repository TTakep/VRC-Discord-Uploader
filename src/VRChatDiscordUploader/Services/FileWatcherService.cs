using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VRChatDiscordUploader.Services;

public class PhotoDetectedEventArgs : EventArgs
{
    public string FilePath { get; }

    public PhotoDetectedEventArgs(string filePath)
    {
        FilePath = filePath;
    }
}

public class FileWatcherService : IDisposable
{
    private FileSystemWatcher? _watcher;
    private readonly ConfigurationService _configService;
    private bool _isPaused = false;

    public event EventHandler<PhotoDetectedEventArgs>? PhotoDetected;

    public bool IsWatching => _watcher != null && _watcher.EnableRaisingEvents && !_isPaused;

    public FileWatcherService(ConfigurationService configService)
    {
        _configService = configService;
    }

    public void Start()
    {
        Stop();

        var watchPath = _configService.GetEffectiveWatchDirectory();
        if (!Directory.Exists(watchPath))
        {
            try
            {
                Directory.CreateDirectory(watchPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"監視対象フォルダの作成に失敗しました: {ex.Message}");
                return;
            }
        }

        try
        {
            _watcher = new FileSystemWatcher(watchPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                Filter = "*.*"
            };

            _watcher.Created += OnFileCreated;
            _watcher.EnableRaisingEvents = true;
            _isPaused = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"FileSystemWatcherの起動に失敗しました: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileCreated;
            _watcher.Dispose();
            _watcher = null;
        }
    }

    public void Pause()
    {
        _isPaused = true;
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
        }
    }

    public void Resume()
    {
        _isPaused = false;
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = true;
        }
        else
        {
            Start();
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (_isPaused) return;

        var ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
        if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
        {
            return;
        }

        // ファイル書き込み完了をバックグラウンドで待機
        _ = Task.Run(async () =>
        {
            if (await WaitForFileReadyAsync(e.FullPath, TimeSpan.FromSeconds(8)))
            {
                PhotoDetected?.Invoke(this, new PhotoDetectedEventArgs(e.FullPath));
            }
        });
    }

    /// <summary>
    /// VRChatによる書き込みが完了し、ファイルロックが解除されるまで待機します。
    /// </summary>
    private static async Task<bool> WaitForFileReadyAsync(string filePath, TimeSpan timeout)
    {
        var startTime = DateTime.UtcNow;
        long lastLength = -1;

        while (DateTime.UtcNow - startTime < timeout)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    long currentLength = fileInfo.Length;

                    // サイズが正の値で、前回チェックから増減が止まっていることを確認
                    if (currentLength > 0 && currentLength == lastLength)
                    {
                        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                        if (stream.Length > 0)
                        {
                            return true;
                        }
                    }

                    lastLength = currentLength;
                }
            }
            catch (IOException)
            {
                // ファイルが排他ロック中のため待機
            }
            catch
            {
                // その他の例外もリトライ
            }

            await Task.Delay(200);
        }

        return File.Exists(filePath);
    }

    public void Dispose()
    {
        Stop();
    }
}
