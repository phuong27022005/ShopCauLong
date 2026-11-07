namespace BadmintonShop.Api.Catalog;

public sealed class ProductImageStore(IWebHostEnvironment environment, ILogger<ProductImageStore> logger)
{
    private const int MaxBytes = 2 * 1024 * 1024;
    private const string UrlPrefix = "/uploads/";

    private string UploadDirectory => Path.Combine(
        environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads");

    public async Task<string> SaveAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw CatalogException.BadRequest("Vui lòng chọn ảnh.");
        if (file.Length > MaxBytes) throw CatalogException.BadRequest("Ảnh không được vượt quá 2 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            throw CatalogException.BadRequest("Chỉ chấp nhận ảnh JPG, PNG hoặc WebP.");

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var count = await input.ReadAtLeastAsync(header, header.Length, false, ct);
        if (!MatchesSignature(extension, header.AsSpan(0, count)))
            throw CatalogException.BadRequest("Nội dung ảnh không khớp với định dạng tệp.");

        Directory.CreateDirectory(UploadDirectory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var url = UrlPrefix + fileName;
        try
        {
            await using var output = new FileStream(Path.Combine(UploadDirectory, fileName),
                FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await output.WriteAsync(header.AsMemory(0, count), ct);
            await input.CopyToAsync(output, ct);
        }
        catch
        {
            Delete(url);
            throw;
        }

        return url;
    }

    public void Delete(string url)
    {
        try
        {
            File.Delete(Path.Combine(UploadDirectory, Path.GetFileName(url)));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Không dọn được ảnh đã tải lên: {ImageUrl}", url);
        }
    }

    // Kiểm tra chữ ký tệp thay vì tin MIME do client gửi.
    private static bool MatchesSignature(string extension, ReadOnlySpan<byte> header) => extension switch
    {
        ".jpg" or ".jpeg" => header.StartsWith(new byte[] { 0xff, 0xd8, 0xff }),
        ".png" => header.StartsWith(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
        ".webp" => header.Length >= 12 && header.StartsWith("RIFF"u8) && header[8..].StartsWith("WEBP"u8),
        _ => false
    };
}
