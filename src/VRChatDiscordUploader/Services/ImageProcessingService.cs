using System;
using System.IO;
using SkiaSharp;

namespace VRChatDiscordUploader.Services;

public class ProcessedImageResult
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "image/png";
    public bool WasResized { get; set; } = false;
    public int Width { get; set; }
    public int Height { get; set; }
}

public class ImageProcessingService
{
    /// <summary>
    /// 画像ファイルを読み込み、必要に応じてPNG形式のまま解像度を縮小リサイズして上限サイズ内に収めます。
    /// </summary>
    public ProcessedImageResult ProcessImageForUpload(string filePath, double maxFileSizeMB)
    {
        var fileInfo = new FileInfo(filePath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("画像ファイルが見つかりません。", filePath);
        }

        var fileName = Path.GetFileName(filePath);
        long maxBytes = (long)(maxFileSizeMB * 1024 * 1024);

        // ファイルサイズが既に上限以内の場合はそのまま読み込んで返却
        if (fileInfo.Length <= maxBytes)
        {
            var rawBytes = File.ReadAllBytes(filePath);
            using var codec = SKCodec.Create(filePath);
            var width = codec?.Info.Width ?? 0;
            var height = codec?.Info.Height ?? 0;

            return new ProcessedImageResult
            {
                Data = rawBytes,
                FileName = fileName,
                ContentType = GetContentType(fileName),
                WasResized = false,
                Width = width,
                Height = height
            };
        }

        // 上限を超えている場合は、SkiaSharpを用いてPNGのまま解像度を縮小
        using var originalBitmap = SKBitmap.Decode(filePath);
        if (originalBitmap == null)
        {
            throw new InvalidOperationException("画像のデコードに失敗しました。");
        }

        int currentWidth = originalBitmap.Width;
        int currentHeight = originalBitmap.Height;

        // 初期スケール推定（面積比から概算）
        double sizeRatio = (double)maxBytes / fileInfo.Length;
        double scale = Math.Min(0.9, Math.Sqrt(sizeRatio) * 0.95); // 安全マージン

        byte[] encodedData = Array.Empty<byte>();

        // 最大8回のリサイズ試行で上限サイズ以下に確実に収める
        for (int attempt = 0; attempt < 8; attempt++)
        {
            int targetWidth = Math.Max(50, (int)(originalBitmap.Width * scale));
            int targetHeight = Math.Max(50, (int)(originalBitmap.Height * scale));

            using var resizedBitmap = originalBitmap.Resize(
                new SKImageInfo(targetWidth, targetHeight),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)
            );

            if (resizedBitmap == null)
            {
                break;
            }

            using var image = SKImage.FromBitmap(resizedBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            encodedData = data.ToArray();
            currentWidth = targetWidth;
            currentHeight = targetHeight;

            if (encodedData.Length <= maxBytes)
            {
                break;
            }

            // 実測サイズ比率から次のスケールを精密に推定
            double ratio = Math.Sqrt((double)maxBytes / encodedData.Length) * 0.92;
            scale = Math.Min(scale * 0.75, scale * ratio);
        }

        return new ProcessedImageResult
        {
            Data = encodedData,
            FileName = Path.ChangeExtension(fileName, ".png"),
            ContentType = "image/png",
            WasResized = true,
            Width = currentWidth,
            Height = currentHeight
        };
    }

    private static string GetContentType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}
