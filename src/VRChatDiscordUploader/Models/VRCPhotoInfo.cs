using System;
using System.Collections.Generic;

namespace VRChatDiscordUploader.Models;

public class VRCPhotoInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public long FileSizeBytes { get; set; }
    
    // ログから抽出したメタデータ
    public string WorldName { get; set; } = string.Empty;
    public string WorldId { get; set; } = string.Empty;
    public List<string> PlayersInRoom { get; set; } = new();

    // 過去写真かリアルタイム写真か
    public bool IsManualUpload { get; set; } = false;
}
