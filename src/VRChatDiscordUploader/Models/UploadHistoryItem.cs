using System;
using System.Collections.Generic;

namespace VRChatDiscordUploader.Models;

public enum UploadStatus
{
    Success,
    Failed,
    Retrying
}

public class UploadHistoryItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string FormattedTimestamp => Timestamp.ToString("yyyy/MM/dd HH:mm:ss");
    public UploadStatus Status { get; set; } = UploadStatus.Success;
    public List<string> FilePaths { get; set; } = new();
    public string FirstFilePath => FilePaths.Count > 0 ? FilePaths[0] : string.Empty;
    public int PhotoCount => FilePaths.Count;
    public string WorldName { get; set; } = string.Empty;
    public string DestinationDescription { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
