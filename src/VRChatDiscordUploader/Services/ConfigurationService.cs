using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using VRChatDiscordUploader.Models;

namespace VRChatDiscordUploader.Services;

public class ConfigurationService
{
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VRCDiscordUploader"
    );

    private static readonly string ConfigFilePath = Path.Combine(AppDataFolder, "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppConfig CurrentConfig { get; private set; } = new();

    public ConfigurationService()
    {
        EnsureDirectoryExists();
        LoadConfig();
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(AppDataFolder))
        {
            Directory.CreateDirectory(AppDataFolder);
        }
    }

    public void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath, Encoding.UTF8);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (loaded != null)
                {
                    CurrentConfig = loaded;
                }
            }
            else
            {
                CurrentConfig = new AppConfig();
                SaveConfig();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"設定ファイルの読み込みに失敗しました: {ex.Message}");
            CurrentConfig = new AppConfig();
        }
    }

    public void SaveConfig()
    {
        try
        {
            EnsureDirectoryExists();
            var json = JsonSerializer.Serialize(CurrentConfig, JsonOptions);
            File.WriteAllText(ConfigFilePath, json, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"設定ファイルの保存に失敗しました: {ex.Message}");
        }
    }

    // Windows標準のDPAPIを利用した暗号化
    public string EncryptString(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(
                plainBytes,
                null,
                DataProtectionScope.CurrentUser
            );
            return Convert.ToBase64String(encryptedBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    // Windows標準のDPAPIを利用した復号
    public string DecryptString(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var decryptedBytes = ProtectedData.Unprotect(
                encryptedBytes,
                null,
                DataProtectionScope.CurrentUser
            );
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    // Webhook URLの取得・設定ヘルパー
    public string GetWebhookUrl() => DecryptString(CurrentConfig.Discord.EncryptedWebhookUrl);

    public void SetWebhookUrl(string url)
    {
        CurrentConfig.Discord.EncryptedWebhookUrl = EncryptString(url);
        SaveConfig();
    }

    // VRChatの写真保存フォルダを自動検出
    public static string GetDefaultVRChatPictureDirectory()
    {
        var myPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return Path.Combine(myPictures, "VRChat");
    }

    // 有効な写真監視フォルダを取得
    public string GetEffectiveWatchDirectory()
    {
        if (!string.IsNullOrWhiteSpace(CurrentConfig.General.CustomWatchDirectory) &&
            Directory.Exists(CurrentConfig.General.CustomWatchDirectory))
        {
            return CurrentConfig.General.CustomWatchDirectory;
        }

        return GetDefaultVRChatPictureDirectory();
    }
}
