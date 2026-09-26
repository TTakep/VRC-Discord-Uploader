using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using VRChatDiscordUploader.Models;

namespace VRChatDiscordUploader.Services;

public class VRChatWorldState
{
    public string WorldName { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public HashSet<string> Players { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public class VRCLogParserService : IDisposable
{
    private static readonly string VRChatLogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
        "VRChat",
        "VRChat"
    );

    private readonly object _stateLock = new();
    private VRChatWorldState _currentState = new();
    private CancellationTokenSource? _cts;
    private Task? _tailTask;

    private static readonly Regex PhotoFileNameRegex = new(
        @"VRChat_(\d{4}-\d{2}-\d{2})_(\d{2}-\d{2}-\d{2}\.\d{3})",
        RegexOptions.Compiled
    );

    private static readonly Regex EnteringRoomRegex = new(
        @"\[Behaviour\] Entering Room:\s*(.+)$",
        RegexOptions.Compiled
    );

    private static readonly Regex JoiningWorldRegex = new(
        @"\[Behaviour\] Joining\s+(wrld_[^\s]+)",
        RegexOptions.Compiled
    );

    private static readonly Regex PlayerJoinedRegex = new(
        @"\[Behaviour\] OnPlayerJoined\s+(.+?)(?:\s+\([a-zA-Z0-9_-]+\))?$",
        RegexOptions.Compiled
    );

    private static readonly Regex PlayerLeftRegex = new(
        @"\[Behaviour\] OnPlayerLeft\s+(.+?)(?:\s+\([a-zA-Z0-9_-]+\))?$",
        RegexOptions.Compiled
    );

    private static readonly Regex LogTimestampRegex = new(
        @"^(\d{4}\.\d{2}\.\d{2}\s+\d{2}:\d{2}:\d{2})",
        RegexOptions.Compiled
    );

    public VRCLogParserService()
    {
    }

    public void StartLiveMonitoring()
    {
        StopLiveMonitoring();
        _cts = new CancellationTokenSource();
        _tailTask = Task.Run(() => TailActiveLogLoop(_cts.Token));
    }

    public void StopLiveMonitoring()
    {
        _cts?.Cancel();
        _tailTask?.Wait(1000);
        _cts?.Dispose();
        _cts = null;
        _tailTask = null;
    }

    public VRChatWorldState GetCurrentWorldState()
    {
        lock (_stateLock)
        {
            var copy = new VRChatWorldState
            {
                WorldName = _currentState.WorldName,
                WorldId = _currentState.WorldId
            };
            foreach (var p in _currentState.Players)
            {
                copy.Players.Add(p);
            }
            return copy;
        }
    }

    /// <summary>
    /// 写真ファイルからメタデータを特定します。
    /// リアルタイム撮影の場合は現在のワールド状態を、過去写真の場合はファイル名と過去ログから復元します。
    /// </summary>
    public VRCPhotoInfo ExtractPhotoMetadata(string filePath, bool isRealtime)
    {
        var fileInfo = new FileInfo(filePath);
        var fileName = Path.GetFileName(filePath);
        var capturedAt = fileInfo.CreationTime;

        // ファイル名から正確な撮影日時を抽出試行
        var match = PhotoFileNameRegex.Match(fileName);
        if (match.Success)
        {
            var dateStr = match.Groups[1].Value;
            var timeStr = match.Groups[2].Value.Replace('-', ':');
            if (DateTime.TryParse($"{dateStr} {timeStr}", out var parsedDate))
            {
                capturedAt = parsedDate;
            }
        }

        var photoInfo = new VRCPhotoInfo
        {
            FilePath = filePath,
            FileName = fileName,
            CapturedAt = capturedAt,
            FileSizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            IsManualUpload = !isRealtime
        };

        if (isRealtime)
        {
            var current = GetCurrentWorldState();
            photoInfo.WorldName = current.WorldName;
            photoInfo.WorldId = current.WorldId;
            photoInfo.PlayersInRoom = current.Players.OrderBy(p => p).ToList();
        }
        else
        {
            // 過去写真の場合: 過去ログから該当時刻の情報を検索
            var pastState = SearchPastLogForTimestamp(capturedAt);
            if (pastState != null)
            {
                photoInfo.WorldName = pastState.WorldName;
                photoInfo.WorldId = pastState.WorldId;
                photoInfo.PlayersInRoom = pastState.Players.OrderBy(p => p).ToList();
            }
        }

        return photoInfo;
    }

    private async Task TailActiveLogLoop(CancellationToken ct)
    {
        string? currentLogFile = null;
        FileStream? stream = null;
        StreamReader? reader = null;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var latestLog = GetLatestLogFile();
                if (latestLog != currentLogFile)
                {
                    reader?.Dispose();
                    stream?.Dispose();
                    currentLogFile = latestLog;

                    if (currentLogFile != null && File.Exists(currentLogFile))
                    {
                        stream = new FileStream(
                            currentLogFile,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite
                        );
                        reader = new StreamReader(stream);
                        // 初回は既存行を走査して現在のワールド状態を復元
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            ProcessLogLine(line);
                        }
                    }
                }

                if (reader != null)
                {
                    string? newLine;
                    while ((newLine = await reader.ReadLineAsync(ct)) != null)
                    {
                        ProcessLogLine(newLine);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ログ監視エラー: {ex.Message}");
            }

            await Task.Delay(1000, ct);
        }

        reader?.Dispose();
        stream?.Dispose();
    }

    private void ProcessLogLine(string line)
    {
        var enteringMatch = EnteringRoomRegex.Match(line);
        if (enteringMatch.Success)
        {
            var worldName = enteringMatch.Groups[1].Value.Trim();
            lock (_stateLock)
            {
                _currentState.WorldName = worldName;
                _currentState.Players.Clear();
            }
            return;
        }

        var joiningMatch = JoiningWorldRegex.Match(line);
        if (joiningMatch.Success)
        {
            lock (_stateLock)
            {
                _currentState.WorldId = joiningMatch.Groups[1].Value.Trim();
            }
            return;
        }

        var joinMatch = PlayerJoinedRegex.Match(line);
        if (joinMatch.Success)
        {
            var playerName = joinMatch.Groups[1].Value.Trim();
            lock (_stateLock)
            {
                _currentState.Players.Add(playerName);
            }
            return;
        }

        var leftMatch = PlayerLeftRegex.Match(line);
        if (leftMatch.Success)
        {
            var playerName = leftMatch.Groups[1].Value.Trim();
            lock (_stateLock)
            {
                _currentState.Players.Remove(playerName);
            }
            return;
        }
    }

    private static string? GetLatestLogFile()
    {
        if (!Directory.Exists(VRChatLogDirectory))
            return null;

        var directoryInfo = new DirectoryInfo(VRChatLogDirectory);
        var files = directoryInfo.GetFiles("output_log_*.txt")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();

        return files.FirstOrDefault()?.FullName;
    }

    /// <summary>
    /// 指定された撮影日時に合致する過去ログを探索し、その時点のワールド状態を再現します。
    /// </summary>
    private VRChatWorldState? SearchPastLogForTimestamp(DateTime targetTime)
    {
        if (!Directory.Exists(VRChatLogDirectory))
            return null;

        var directoryInfo = new DirectoryInfo(VRChatLogDirectory);
        var files = directoryInfo.GetFiles("output_log_*.txt")
            .Where(f => f.CreationTime <= targetTime.AddHours(2) && f.LastWriteTime >= targetTime.AddHours(-2))
            .OrderBy(f => f.CreationTime)
            .ToList();

        foreach (var file in files)
        {
            try
            {
                using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);

                var tempState = new VRChatWorldState();
                var lastValidState = new VRChatWorldState();
                bool foundAny = false;

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    // ログ行のタイムスタンプ解析
                    var tsMatch = LogTimestampRegex.Match(line);
                    if (tsMatch.Success && DateTime.TryParseExact(
                        tsMatch.Groups[1].Value,
                        "yyyy.MM.dd HH:mm:ss",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var logTime))
                    {
                        if (logTime > targetTime)
                        {
                            // 撮影時刻を超えたので、直前までのワールド状態を確定
                            if (foundAny)
                            {
                                return lastValidState;
                            }
                            break;
                        }
                    }

                    var enteringMatch = EnteringRoomRegex.Match(line);
                    if (enteringMatch.Success)
                    {
                        tempState.WorldName = enteringMatch.Groups[1].Value.Trim();
                        tempState.Players.Clear();
                        foundAny = true;
                        continue;
                    }

                    var joinMatch = PlayerJoinedRegex.Match(line);
                    if (joinMatch.Success)
                    {
                        tempState.Players.Add(joinMatch.Groups[1].Value.Trim());
                        continue;
                    }

                    var leftMatch = PlayerLeftRegex.Match(line);
                    if (leftMatch.Success)
                    {
                        tempState.Players.Remove(leftMatch.Groups[1].Value.Trim());
                        continue;
                    }

                    lastValidState.WorldName = tempState.WorldName;
                    lastValidState.WorldId = tempState.WorldId;
                    lastValidState.Players.Clear();
                    foreach (var p in tempState.Players)
                    {
                        lastValidState.Players.Add(p);
                    }
                }

                if (foundAny)
                {
                    return lastValidState;
                }
            }
            catch
            {
                // 次のファイルを試行
            }
        }

        return null;
    }

    public void Dispose()
    {
        StopLiveMonitoring();
    }
}
