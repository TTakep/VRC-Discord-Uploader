using System;

namespace VRChatDiscordUploader.Models;

public class AnnouncementItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public string FormattedDate => PublishedAt.ToString("yyyy/MM/dd");
    public string LinkUrl { get; set; } = string.Empty;
    public bool IsImportant { get; set; } = false;
}

public class UpdateCheckResult
{
    public bool HasUpdate { get; set; }
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}
