using BadmintonShop.Core.Interfaces;

namespace BadmintonShop.Api.Catalog;

public sealed class CatalogErrorMiddleware(RequestDelegate next, ILogger<CatalogErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted && !context.RequestAborted.IsCancellationRequested)
        {
            var (status, message) = ex switch
            {
                EngagementConflictException => (409, "Đánh giá hoặc mã giảm giá đã tồn tại."),
                CatalogException error => (error.StatusCode, error.Message),
                OrderConflictException error => (409, error.Message),
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (409, "Dữ liệu đã thay đổi. Vui lòng tải lại."),
                CartUserNotFoundException => (401, "Tài khoản không tồn tại."),
                CatalogSlugConflictException => (409, "Slug đã được sử dụng. Vui lòng thử lại."),
                CatalogReferenceConflictException => (409, "Dữ liệu đang được tham chiếu."),
                _ => (500, "Đã xảy ra lỗi máy chủ.")
            };
            if (status == 500) logger.LogError(ex, "Không thể xử lý yêu cầu catalog.");
            context.Response.Clear();
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(ApiResponse.Error(message), context.RequestAborted);
        }
    }
}
