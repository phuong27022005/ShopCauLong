using System.Security.Claims;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Api.Engagement;
using BadmintonShop.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BadmintonShop.Api.Controllers;

[ApiController]
[Route("api/products/{productId:int}/reviews")]
public sealed class ReviewsController(ReviewService reviews) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue("sub"),out var id) && id>0
        ? id : throw new CatalogException(401,"Token không có userId hợp lệ.");
    [Authorize]
    [HttpGet("eligibility")]
    public async Task<IActionResult> Eligibility(int productId, CancellationToken ct) =>
        Ok(ApiResponse.Ok(await reviews.EligibilityAsync(UserId, productId, ct)));
    [HttpGet]
    public async Task<IActionResult> List(int productId,CancellationToken ct,int page=1,int pageSize=12) =>
        Ok(ApiResponse.Ok(await reviews.ListAsync(productId,page,pageSize,ct)));
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create(int productId,ReviewRequest input,CancellationToken ct) =>
        StatusCode(201,ApiResponse.Ok(await reviews.SaveAsync(UserId,productId,null,input,ct)));
    [Authorize]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int productId,int id,ReviewRequest input,CancellationToken ct) =>
        Ok(ApiResponse.Ok(await reviews.SaveAsync(UserId,productId,id,input,ct)));
    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int productId,int id,CancellationToken ct)
    {
        await reviews.DeleteAsync(UserId,User.IsInRole("Admin"),productId,id,ct);
        return Ok(ApiResponse.Ok());
    }
}
[ApiController]
[Route("api/coupons")]
public sealed class CouponsController(CouponService coupons) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(ValidateCouponRequest input,CancellationToken ct)
    {
        var (coupon,discount)=await coupons.ValidateAsync(input.Code,input.Subtotal,ct);
        return Ok(ApiResponse.Ok(new { code=coupon.Code, discountAmount=discount, totalAmount=input.Subtotal-discount }));
    }
}
[ApiController]
[Authorize(Policy="AdminOnly")]
[Route("api/admin/coupons")]
public sealed class AdminCouponsController(CouponService coupons) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(ApiResponse.Ok(await coupons.ListAsync(ct)));
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id,CancellationToken ct) => Ok(ApiResponse.Ok(await coupons.GetAsync(id,ct)));
    [HttpPost]
    public async Task<IActionResult> Create(CouponRequest input,CancellationToken ct)
    {
        var coupon=await coupons.SaveAsync(null,input,ct);
        return CreatedAtAction(nameof(Get),new {id=coupon.Id},ApiResponse.Ok(coupon));
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id,CouponRequest input,CancellationToken ct) =>
        Ok(ApiResponse.Ok(await coupons.SaveAsync(id,input,ct)));
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id,CancellationToken ct)
    {
        await coupons.DeleteAsync(id,ct);
        return Ok(ApiResponse.Ok());
    }
}
