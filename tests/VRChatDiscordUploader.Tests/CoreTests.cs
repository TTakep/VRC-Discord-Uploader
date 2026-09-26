using System;
using System.IO;
using SkiaSharp;
using VRChatDiscordUploader.Models;
using VRChatDiscordUploader.Services;
using Xunit;

namespace VRChatDiscordUploader.Tests;

public class CoreTests
{
    [Fact]
    public void ConfigurationService_DpapiEncryption_EncryptsAndDecryptsCorrectly()
    {
        var configService = new ConfigurationService();
        var originalUrl = "https://discord.com/api/webhooks/123456789/abcdefghijklmnopqrstuvwxyz";

        var encrypted = configService.EncryptString(originalUrl);
        Assert.False(string.IsNullOrEmpty(encrypted));
        Assert.NotEqual(originalUrl, encrypted);

        var decrypted = configService.DecryptString(encrypted);
        Assert.Equal(originalUrl, decrypted);
    }

    [Fact]
    public void VRCLogParser_ExtractPhotoMetadata_ParsesTimestampFromFilename()
    {
        var parser = new VRCLogParserService();
        var tempFile = Path.Combine(Path.GetTempPath(), "VRChat_2026-08-15_14-30-45.123_1920x1080.png");
        
        try
        {
            File.WriteAllText(tempFile, "dummy");
            var metadata = parser.ExtractPhotoMetadata(tempFile, isRealtime: false);

            Assert.Equal(2026, metadata.CapturedAt.Year);
            Assert.Equal(8, metadata.CapturedAt.Month);
            Assert.Equal(15, metadata.CapturedAt.Day);
            Assert.Equal(14, metadata.CapturedAt.Hour);
            Assert.Equal(30, metadata.CapturedAt.Minute);
            Assert.Equal(45, metadata.CapturedAt.Second);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ImageProcessingService_PreservesPng_AndResizesWhenExceedingLimit()
    {
        var imageService = new ImageProcessingService();
        var tempFile = Path.Combine(Path.GetTempPath(), "test_large_image.png");

        try
        {
            // 4K解像度 (3840x2160) のランダムカラー画像を生成
            using (var bitmap = new SKBitmap(3840, 2160))
            {
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.CadetBlue);
                using var paint = new SKPaint { Color = SKColors.IndianRed, StrokeWidth = 10 };
                for (int i = 0; i < 500; i++)
                {
                    canvas.DrawLine(i * 7, 0, i * 5, 2160, paint);
                }

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var fs = File.OpenWrite(tempFile);
                data.SaveTo(fs);
            }

            var originalSize = new FileInfo(tempFile).Length;
            
            // ファイルサイズより確実に小さい上限（元のサイズの半分）を設定してリサイズを強制
            double maxMB = (originalSize / 2.0) / (1024.0 * 1024.0);
            var result = imageService.ProcessImageForUpload(tempFile, maxMB);

            Assert.True(result.WasResized);
            Assert.Equal("image/png", result.ContentType);
            Assert.EndsWith(".png", result.FileName);
            Assert.True(result.Data.Length <= (long)(maxMB * 1024 * 1024) + 1024); // 許容マージン
            Assert.True(result.Width < 3840);
            Assert.True(result.Height < 2160);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void ForumTitleTemplate_FormatsCorrectly()
    {
        var dt = new DateTime(2026, 9, 26);
        var template = "{Year}年{Month}月の写真";
        var title = template
            .Replace("{Year}", dt.ToString("yyyy"))
            .Replace("{Month}", dt.ToString("MM"))
            .Replace("{Date}", dt.ToString("yyyy/MM/dd"));

        Assert.Equal("2026年09月の写真", title);
    }

    [Fact]
    public async Task DiscordWebhookService_TestWebhookAsync_RejectsInvalidUrl()
    {
        var configService = new ConfigurationService();
        var imageService = new ImageProcessingService();
        var discordService = new DiscordWebhookService(configService, imageService);

        // GoogleアカウントのURLなど無効なURL
        var (success1, msg1) = await discordService.TestWebhookAsync("https://accounts.google.com/o/oauth2/v2/auth");
        Assert.False(success1);
        Assert.Contains("形式が正しくありません", msg1);

        // 空URL
        var (success2, msg2) = await discordService.TestWebhookAsync("");
        Assert.False(success2);
        Assert.Contains("入力されていません", msg2);
    }
}
