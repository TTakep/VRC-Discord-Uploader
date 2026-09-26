using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using VRChatDiscordUploader.Models;

namespace VRChatDiscordUploader.Services;

public class AnnouncementService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    // 配布・公開時のGitHub Releases または お知らせ用JSONのエンドポイント
    private const string AnnouncementsUrl = "https://raw.githubusercontent.com/Takep/VRC-Discord-Uploader/main/announcements.json";
    private const string GitHubReleasesApiUrl = "https://api.github.com/repos/Takep/VRC-Discord-Uploader/releases/latest";

    public async Task<List<AnnouncementItem>> GetAnnouncementsAsync()
    {
        try
        {
            var items = await HttpClient.GetFromJsonAsync<List<AnnouncementItem>>(AnnouncementsUrl);
            if (items != null)
            {
                return items;
            }
        }
        catch
        {
            // オフラインまたはエンドポイント未設置時のフォールバック（デフォルトメッセージ）
        }

        return new List<AnnouncementItem>
        {
            new()
            {
                Id = "welcome",
                Title = "VRChat Discord Uploader へようこそ",
                Content = "本ソフトウェアをご利用いただきありがとうございます。設定画面からDiscord Webhook URLを登録してご利用ください。",
                PublishedAt = DateTime.Now,
                LinkUrl = "https://github.com/Takep/VRC-Discord-Uploader",
                IsImportant = false
            }
        };
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        var result = new UpdateCheckResult { HasUpdate = false };
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GitHubReleasesApiUrl);
            request.Headers.UserAgent.ParseAdd("VRChatDiscordUploader/1.0");

            var response = await HttpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var tagName = root.GetProperty("tag_name").GetString() ?? "";
                var body = root.GetProperty("body").GetString() ?? "";
                var htmlUrl = root.GetProperty("html_url").GetString() ?? "";

                var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
                var latestVersionStr = tagName.TrimStart('v', 'V');

                if (Version.TryParse(latestVersionStr, out var latestVer) && currentVersion != null)
                {
                    if (latestVer > currentVersion)
                    {
                        result.HasUpdate = true;
                        result.LatestVersion = tagName;
                        result.ReleaseNotes = body;
                        result.DownloadUrl = htmlUrl;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"アップデート確認に失敗しました: {ex.Message}");
        }

        return result;
    }
}
