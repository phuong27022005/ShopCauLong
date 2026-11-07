using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BadmintonShop.Api.Payments;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BadmintonShop.Tests;

public sealed class VnpayTests : IAsyncLifetime
{
    // Khóa giả chỉ dùng cho test, không phải khóa merchant.
    private const string Secret = "unit-test-secret-not-a-merchant-key";
    private readonly CatalogApiFactory factory = new(concurrent: true);
    private HttpClient client = null!;
    public async Task InitializeAsync()
    {
        foreach (var pair in new Dictionary<string,string?>
        {
            ["Vnpay:TmnCode"] = "TESTONLY", ["Vnpay:HashSecret"] = Secret,
            ["Vnpay:ReturnUrl"] = "https://merchant.example/api/payments/vnpay/return",
            ["Vnpay:IpnUrl"] = "https://merchant.example/api/payments/vnpay/ipn",
            ["Vnpay:FrontendReturnUrl"] = "http://localhost:5173/payment/result"
        }) factory.ConfigurationOverrides.Add(pair.Key,pair.Value);
        client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CatalogApiFactory.Token("Customer", userId:"2"));
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.EnsureCreatedAsync();
    }
    public async Task DisposeAsync() { client.Dispose(); await factory.DisposeAsync(); }
    private async Task<OrderDto> Order(string method = "VNPAY")
    {
        (await client.PostAsJsonAsync("/api/cart/items", new { productId=1, quantity=1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        var response = await client.PostAsJsonAsync("/api/orders", new CreateOrderRequest("Hà Nội","0900000000",method,null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var order = body.GetProperty("data").Deserialize<OrderDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        if (method == "VNPAY") (order.PaymentExpiredAt!.Value - order.CreatedAt).Should().Be(TimeSpan.FromMinutes(15));
        return order;
    }
    private async Task<Dictionary<string,string>> Create(int id)
    {
        var response = await client.PostAsJsonAsync("/api/payments/vnpay/create", new { orderId=id });
        response.StatusCode.Should().Be(HttpStatusCode.OK,await response.Content.ReadAsStringAsync());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var url = new Uri(body.GetProperty("data").GetProperty("paymentUrl").GetString()!);
        url.Host.Should().Be("sandbox.vnpayment.vn");
        var query = QueryHelpers.ParseQuery(url.Query).ToDictionary(x => x.Key,x => x.Value.ToString());
        VnpaySignature.Verify(query,Secret).Should().BeTrue();
        return new()
        {
            ["vnp_TmnCode"]="TESTONLY", ["vnp_TxnRef"]=query["vnp_TxnRef"], ["vnp_Amount"]=query["vnp_Amount"],
            ["vnp_ResponseCode"]="00", ["vnp_TransactionStatus"]="00", ["vnp_TransactionNo"]="1234567"
        };
    }
    private static string Callback(Dictionary<string,string> values, string route = "ipn", bool valid = true)
    {
        var fields = new Dictionary<string,string>(values) { ["vnp_SecureHash"]=valid ? VnpaySignature.Sign(values,Secret) : new string('0',128) };
        return QueryHelpers.AddQueryString("/api/payments/vnpay/"+route,fields.Select(x => new KeyValuePair<string,string?>(x.Key,x.Value)));
    }
    private async Task<string> Ipn(Dictionary<string,string> values, bool valid = true)
    {
        var response = await client.GetAsync(Callback(values,valid:valid));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("Message",out _).Should().BeTrue();
        return json.GetProperty("RspCode").GetString()!;
    }

    [Fact]
    public void SignatureMatchesIndependentVectorAndDetectsTampering()
    {
        var values = new Dictionary<string,string> { ["vnp_TxnRef"]="test-01",["vnp_OrderInfo"]="Thanh toan A & B",["vnp_Amount"]="125000000" };
        VnpaySignature.Sign(values,Secret).Should().Be("2099c411d944b627ed128186ce31abc0388df0b41bb979ab4e672da2244618dd39f60480fdb09b775cbbd962fbf9d9559473faed22f980f36772f5caad23663d");
        VnpaySignature.Amount(1250000).Should().Be("125000000");
        VnpaySignature.Amount(100.25m).Should().Be("10025");
        values["vnp_SecureHash"]=VnpaySignature.Sign(values,Secret);
        VnpaySignature.Verify(values,Secret).Should().BeTrue();
        values["vnp_Amount"]="1";
        VnpaySignature.Verify(values,Secret).Should().BeFalse();
    }

    [Fact]
    public async Task CreateChecksOwnershipPaymentMethodStatusAndDeadline()
    {
        var order=await Order();
        await Create(order.Id);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",CatalogApiFactory.Token("Customer",userId:"1"));
        (await client.PostAsJsonAsync("/api/payments/vnpay/create",new {orderId=order.Id})).StatusCode.Should().Be(HttpStatusCode.NotFound);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",CatalogApiFactory.Token("Customer",userId:"2"));
        var cod=await Order("COD");
        (await client.PostAsJsonAsync("/api/payments/vnpay/create",new {orderId=cod.Id})).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        foreach(var state in new[]{"Paid","Expired","Cancelled"})
        {
            await using(var scope=factory.Services.CreateAsyncScope())
            {
                var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();
                var entity=(await db.Orders.FindAsync(order.Id))!;
                entity.PaymentStatus=state=="Paid"?"Paid":"Unpaid";
                entity.Status=state=="Cancelled"?"Cancelled":"Pending";
                entity.PaymentExpiredAt=state=="Expired"?DateTime.UtcNow.AddMinutes(-1):DateTime.UtcNow.AddMinutes(15);
                await db.SaveChangesAsync();
            }
            (await client.PostAsJsonAsync("/api/payments/vnpay/create",new {orderId=order.Id})).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        client.DefaultRequestHeaders.Authorization=null;
        (await client.PostAsJsonAsync("/api/payments/vnpay/create",new {orderId=order.Id})).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IpnUpdatesOnceAndReturnNeverMarksPaid()
    {
        var order=await Order();
        var fields=await Create(order.Id);
        var returned=await client.GetAsync(Callback(fields,"return"));
        returned.StatusCode.Should().Be(HttpStatusCode.Redirect);
        returned.Headers.Location!.Query.Should().Contain("pending-verification");
        returned.Headers.Location.Query.Should().Contain("orderId=" + order.Id);
        await using(var scope=factory.Services.CreateAsyncScope())
            (await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Orders.FindAsync(order.Id))!.PaymentStatus.Should().Be("Unpaid");
        (await Ipn(fields)).Should().Be("00");
        (await Ipn(fields)).Should().Be("02");
        await using var read=factory.Services.CreateAsyncScope();
        var db=read.ServiceProvider.GetRequiredService<ShopDbContext>();
        (await db.Orders.FindAsync(order.Id))!.PaymentStatus.Should().Be("Paid");
        var transaction=await db.PaymentTransactions.SingleAsync();
        transaction.ResponseCode.Should().Be("00");
        transaction.RawData.Should().Contain("1234567");
    }

    [Fact]
    public async Task InvalidCallbacksDoNotChangeOrder()
    {
        var order=await Order();
        var fields=await Create(order.Id);
        (await Ipn(fields,false)).Should().Be("97");
        var missing=new Dictionary<string,string>(fields) { ["vnp_TxnRef"]="unknown" };
        (await Ipn(missing)).Should().Be("01");
        var amount=new Dictionary<string,string>(fields) { ["vnp_Amount"]="1" };
        (await Ipn(amount)).Should().Be("04");
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        (await db.Orders.FindAsync(order.Id))!.PaymentStatus.Should().Be("Unpaid");
        (await db.PaymentTransactions.SingleAsync()).ResponseCode.Should().BeNull();
    }

    [Fact]
    public async Task FailedPaymentCanRetryWithNewReference()
    {
        var order=await Order();
        var fields=await Create(order.Id);
        fields["vnp_ResponseCode"]="24";fields["vnp_TransactionStatus"]="02";
        (await Ipn(fields)).Should().Be("00");
        (await Ipn(fields)).Should().Be("02");
        await using(var read=factory.Services.CreateAsyncScope())
            (await read.ServiceProvider.GetRequiredService<ShopDbContext>().Orders.FindAsync(order.Id))!.PaymentStatus.Should().Be("Failed");
        var retry=await Create(order.Id);
        retry["vnp_TxnRef"].Should().NotBe(fields["vnp_TxnRef"]);
        (await Ipn(retry)).Should().Be("00");
        await using var scope=factory.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        (await db.Orders.FindAsync(order.Id))!.PaymentStatus.Should().Be("Paid");
        (await db.PaymentTransactions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task ExpirationRestoresOnlyUnpaidStockAndIsRepeatSafe()
    {
        var unpaid=await Order();
        var paid=await Order();
        var fields=await Create(paid.Id);
        (await Ipn(fields)).Should().Be("00");
        await using(var edit=factory.Services.CreateAsyncScope())
        {
            var db=edit.ServiceProvider.GetRequiredService<ShopDbContext>();
            foreach(var order in await db.Orders.ToListAsync()) order.PaymentExpiredAt=DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        await using(var job=factory.Services.CreateAsyncScope())
        {
            var expiry=job.ServiceProvider.GetRequiredService<PaymentExpiration>();
            await expiry.RunAsync(default);
            await expiry.RunAsync(default);
        }
        await using var read=factory.Services.CreateAsyncScope();
        var database=read.ServiceProvider.GetRequiredService<ShopDbContext>();
        (await database.Orders.FindAsync(unpaid.Id))!.Status.Should().Be("Cancelled");
        (await database.Orders.FindAsync(paid.Id))!.Status.Should().Be("Pending");
        (await database.Products.FindAsync(1))!.Stock.Should().Be(9);
    }

    [Fact]
    public async Task ConcurrentIpnsConfirmExactlyOnce()
    {
        var order=await Order();
        var fields=await Create(order.Id);
        var results=await Task.WhenAll(Ipn(fields),Ipn(fields));
        results.Should().BeEquivalentTo("00","02");
        await using var read=factory.Services.CreateAsyncScope();
        (await read.ServiceProvider.GetRequiredService<ShopDbContext>().PaymentTransactions.CountAsync()).Should().Be(1);
    }
}
