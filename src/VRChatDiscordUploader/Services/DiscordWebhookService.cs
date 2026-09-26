using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using VRChatDiscordUploader.Models;

namespace VRChatDiscordUploader.Services;

public class DiscordWebhookService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    private readonly ConfigurationService _configService;
    private readonly ImageProcessingService _imageService;

    public DiscordWebhookService(ConfigurationService configService, ImageProcessingService imageService)
    {
        _configService = configService;
        _imageService = imageService;
    }

    /// <summary>
    /// 写真リストをDiscordへアップロードします。
    /// </summary>
    public async Task<UploadHistoryItem> UploadPhotosAsync(List<VRCPhotoInfo> photos)
    {
        var config = _configService.CurrentConfig;
        var webhookUrl = _configService.GetWebhookUrl();

        var historyItem = new UploadHistoryItem
        {
            Timestamp = DateTime.Now,
            FilePaths = photos.Select(p => p.FilePath).ToList(),
            WorldName = photos.FirstOrDefault()?.WorldName ?? string.Empty
        };

        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            historyItem.Status = UploadStatus.Failed;
            historyItem.ErrorMessage = "Discord Webhook URLが設定されていません。";
            return historyItem;
        }

        webhookUrl = webhookUrl.Trim();
        if (!webhookUrl.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase) &&
            !webhookUrl.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase))
        {
            historyItem.Status = UploadStatus.Failed;
            historyItem.ErrorMessage = "Discord Webhook URLの形式が無効です。「https://discord.com/api/webhooks/...」で始まるURLを設定してください。";
            return historyItem;
        }

        try
        {
            var primaryPhoto = photos.FirstOrDefault();
            var targetMonth = primaryPhoto?.CapturedAt.ToString("yyyy-MM") ?? DateTime.Now.ToString("yyyy-MM");

            // URLクエリおよびスレッド作成パラメータの構築
            string requestUrl = webhookUrl;
            if (!requestUrl.Contains("?"))
            {
                requestUrl += "?wait=true";
            }
            else
            {
                requestUrl += "&wait=true";
            }

            string? newThreadName = null;

            switch (config.Discord.DestinationType)
            {
                case DestinationType.Thread:
                    if (!string.IsNullOrWhiteSpace(config.Discord.ThreadId))
                    {
                        requestUrl += $"&thread_id={config.Discord.ThreadId.Trim()}";
                        historyItem.DestinationDescription = $"スレッド (ID: {config.Discord.ThreadId})";
                    }
                    else
                    {
                        historyItem.DestinationDescription = "スレッド (ID未指定)";
                    }
                    break;

                case DestinationType.Forum:
                    // キャッシュされた年月とスレッドIDを確認
                    if (config.Discord.ForumCachedMonth == targetMonth &&
                        !string.IsNullOrWhiteSpace(config.Discord.ForumCachedThreadId))
                    {
                        // 既存の今月スレッドへ投稿
                        requestUrl += $"&thread_id={config.Discord.ForumCachedThreadId.Trim()}";
                        historyItem.DestinationDescription = $"フォーラム ({targetMonth} スレッド)";
                    }
                    else
                    {
                        // 新規に月別スレッドを立ち上げる
                        var template = config.Discord.ForumThreadTitleTemplate;
                        if (string.IsNullOrWhiteSpace(template))
                        {
                            template = "{Year}年{Month}月の写真";
                        }

                        var dt = primaryPhoto?.CapturedAt ?? DateTime.Now;
                        newThreadName = template
                            .Replace("{Year}", dt.ToString("yyyy"))
                            .Replace("{Month}", dt.ToString("MM"))
                            .Replace("{Date}", dt.ToString("yyyy/MM/dd"));

                        historyItem.DestinationDescription = $"フォーラム (新規: {newThreadName})";
                    }
                    break;

                default:
                    historyItem.DestinationDescription = "チャンネル";
                    break;
            }

            // マルチパートフォームデータの構築
            using var form = new MultipartFormDataContent();

            // 画像処理と添付
            var processedImages = new List<ProcessedImageResult>();
            for (int i = 0; i < photos.Count; i++)
            {
                var photo = photos[i];
                var processed = _imageService.ProcessImageForUpload(photo.FilePath, config.UploadBehavior.MaxFileSizeMB);
                processedImages.Add(processed);

                var fileContent = new ByteArrayContent(processed.Data);
                fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(processed.ContentType);
                form.Add(fileContent, $"files[{i}]", processed.FileName);
            }

            // ペイロードJSONの構築
            var payloadNode = BuildPayloadJson(photos, processedImages, newThreadName, config);
            var jsonString = payloadNode.ToJsonString();
            var jsonContent = new StringContent(jsonString, Encoding.UTF8, "application/json");
            form.Add(jsonContent, "payload_json");

            // 送信（429レートリミット時の自動リトライ対応）
            HttpResponseMessage? response = null;
            int maxRetries = 3;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                response = await HttpClient.PostAsync(requestUrl, form);
                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                if ((int)response.StatusCode == 429) // Too Many Requests
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    double retryAfterSeconds = 2.0;

                    try
                    {
                        var jsonResp = JsonNode.Parse(responseBody);
                        if (jsonResp?["retry_after"] != null)
                        {
                            retryAfterSeconds = (double)(jsonResp["retry_after"]?.GetValue<double>() ?? 2.0);
                        }
                    }
                    catch
                    {
                        // パース失敗時はデフォルト2秒
                    }

                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(10, retryAfterSeconds + 0.5)));
                    continue;
                }

                // その他のエラー
                break;
            }

            if (response != null && response.IsSuccessStatusCode)
            {
                historyItem.Status = UploadStatus.Success;

                // フォーラム新規作成時のスレッドIDキャッシュ保存
                if (newThreadName != null)
                {
                    try
                    {
                        var resStr = await response.Content.ReadAsStringAsync();
                        var resJson = JsonNode.Parse(resStr);
                        // Discord仕様: wait=true の場合、フォーラムで新規作成されたスレッド内のメッセージが返る。
                        // その channel_id が新規スレッドのIDとなる。
                        var channelId = resJson?["channel_id"]?.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(channelId))
                        {
                            config.Discord.ForumCachedMonth = targetMonth;
                            config.Discord.ForumCachedThreadId = channelId;
                            _configService.SaveConfig();
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"スレッドIDキャッシュの更新に失敗しました: {ex.Message}");
                    }
                }
            }
            else
            {
                historyItem.Status = UploadStatus.Failed;
                var errDetail = response != null ? await response.Content.ReadAsStringAsync() : "通信に失敗しました。";
                historyItem.ErrorMessage = $"Discord API エラー ({(int?)response?.StatusCode}): {errDetail}";
            }
        }
        catch (Exception ex)
        {
            historyItem.Status = UploadStatus.Failed;
            historyItem.ErrorMessage = $"送信処理例外: {ex.Message}";
        }

        return historyItem;
    }

    static DiscordWebhookService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VRChatDiscordUploader/1.0 (+https://github.com)");
    }

    /// <summary>
    /// Webhook URLへの疎通テストを送信します。
    /// </summary>
    public async Task<(bool Success, string Message)> TestWebhookAsync(string webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return (false, "Webhook URLが入力されていません。");
        }

        webhookUrl = webhookUrl.Trim();
        if (!webhookUrl.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase) &&
            !webhookUrl.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Webhook URLの形式が正しくありません。「https://discord.com/api/webhooks/...」で始まるDiscordのWebhook URLを入力してください。");
        }

        try
        {
            var config = _configService.CurrentConfig;
            string requestUrl = webhookUrl;
            var payload = new JsonObject
            {
                ["content"] = "🔔 VRChat Discord Uploader: 接続テストに成功しました。"
            };

            // スレッドまたはフォーラムの指定がある場合の対応
            if (config.Discord.DestinationType == DestinationType.Thread && !string.IsNullOrWhiteSpace(config.Discord.ThreadId))
            {
                requestUrl += (requestUrl.Contains('?') ? "&" : "?") + $"thread_id={config.Discord.ThreadId.Trim()}";
            }
            else if (config.Discord.DestinationType == DestinationType.Forum)
            {
                if (!string.IsNullOrWhiteSpace(config.Discord.ForumCachedThreadId))
                {
                    requestUrl += (requestUrl.Contains('?') ? "&" : "?") + $"thread_id={config.Discord.ForumCachedThreadId.Trim()}";
                }
                else
                {
                    payload["thread_name"] = "接続テスト";
                }
            }

            using var content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
            var response = await HttpClient.PostAsync(requestUrl, content);

            if (response.IsSuccessStatusCode)
            {
                App.Log("Webhook接続テスト成功");
                return (true, "接続に成功しました。Discordにテストメッセージが投稿されました。");
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync();
                App.Log($"Webhook接続テストエラー ({(int)response.StatusCode}): {err}");
                return (false, $"エラー ({(int)response.StatusCode}): {err}");
            }
        }
        catch (Exception ex)
        {
            App.Log($"Webhook接続テスト例外: {ex.Message}");
            return (false, $"通信エラー: {ex.Message}");
        }
    }

    private JsonObject BuildPayloadJson(
        List<VRCPhotoInfo> photos,
        List<ProcessedImageResult> images,
        string? newThreadName,
        AppConfig config)
    {
        var root = new JsonObject();

        if (!string.IsNullOrWhiteSpace(newThreadName))
        {
            root["thread_name"] = newThreadName;
        }

        // 「画像のみ送信モード」の場合
        if (config.UploadBehavior.ImageOnly)
        {
            return root;
        }

        // Embed埋め込み作成
        var primaryPhoto = photos.FirstOrDefault();
        if (primaryPhoto == null)
            return root;

        var embedsArray = new JsonArray();
        var embed = new JsonObject
        {
            ["color"] = 0x5865F2 // Discord Blurple
        };

        // タイトル（ワールド名）
        if (config.Metadata.IncludeWorldName && !string.IsNullOrWhiteSpace(primaryPhoto.WorldName))
        {
            embed["title"] = $"📍 {primaryPhoto.WorldName}";
        }

        var fields = new JsonArray();

        // 撮影日時フィールド
        if (config.Metadata.IncludeDateTime)
        {
            fields.Add(new JsonObject
            {
                ["name"] = "🕒 撮影日時",
                ["value"] = primaryPhoto.CapturedAt.ToString("yyyy/MM/dd HH:mm:ss"),
                ["inline"] = true
            });
        }

        // ワールドID / インスタンス情報フィールド
        if (config.Metadata.IncludeWorldId && !string.IsNullOrWhiteSpace(primaryPhoto.WorldId))
        {
            fields.Add(new JsonObject
            {
                ["name"] = "🌐 インスタンス",
                ["value"] = $"`{primaryPhoto.WorldId}`",
                ["inline"] = false
            });
        }

        // 同室ユーザー名フィールド
        if (config.Metadata.IncludeUsers && primaryPhoto.PlayersInRoom.Count > 0)
        {
            var users = primaryPhoto.PlayersInRoom;
            int max = Math.Max(1, config.Metadata.MaxUsersCount);
            string userText;

            if (users.Count <= max)
            {
                userText = string.Join(", ", users);
            }
            else
            {
                var listed = string.Join(", ", users.Take(max));
                var remaining = users.Count - max;
                userText = $"{listed} (+他{remaining}名)";
            }

            fields.Add(new JsonObject
            {
                ["name"] = $"👥 同室のプレイヤー ({users.Count}名)",
                ["value"] = userText,
                ["inline"] = false
            });
        }

        if (fields.Count > 0)
        {
            embed["fields"] = fields;
        }

        // 1枚目をEmbedに埋め込みプレビューとして表示
        if (images.Count > 0)
        {
            embed["image"] = new JsonObject
            {
                ["url"] = $"attachment://{images[0].FileName}"
            };
        }

        embedsArray.Add(embed);
        root["embeds"] = embedsArray;

        return root;
    }
}
