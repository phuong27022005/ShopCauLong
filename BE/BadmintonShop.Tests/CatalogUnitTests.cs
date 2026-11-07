using AutoMapper;
using BadmintonShop.Api.Catalog;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;
using BadmintonShop.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace BadmintonShop.Tests;

public sealed class CatalogUnitTests
{
    [Theory]
    [InlineData("  Vợt Đập Cầu! 4U  ", "vot-dap-cau-4u")]
    [InlineData("💥", "san-pham")]
    public async Task SlugNormalizesNames(string name, string expected)
    {
        var slug = await SlugGenerator.CreateAsync(name, "san-pham", _ => Task.FromResult(false));
        slug.Should().Be(expected);
    }

    [Fact]
    public async Task SlugSkipsAllOccupiedSuffixes()
    {
        var occupied = new HashSet<string> { "vot-cau-long", "vot-cau-long-2" };
        var slug = await SlugGenerator.CreateAsync("Vợt cầu lông", "san-pham",
            candidate => Task.FromResult(occupied.Contains(candidate)));
        slug.Should().Be("vot-cau-long-3");
    }

    [Theory]
    [InlineData("2U")]
    [InlineData("3U")]
    [InlineData("4U")]
    [InlineData("5U")]
    [InlineData("F")]
    public void ValidatorAcceptsAllSupportedWeightsAndBoundaryValues(string weight)
    {
        var request = new SaveProductRequest
        {
            Name = "Vợt", Price = 0.01m, DiscountPrice = 0, Stock = 0,
            BrandId = 1, CategoryId = 1, Weight = weight
        };

        new SaveProductValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ValidatorRejectsInvalidFields()
    {
        var request = new SaveProductRequest
        {
            Name = " ", Price = 0, DiscountPrice = 1, Stock = -1,
            BrandId = 1, CategoryId = 1, Weight = "6U"
        };

        new SaveProductValidator().Validate(request).Errors.Select(error => error.PropertyName)
            .Should().BeEquivalentTo("Name", "Price", "DiscountPrice", "Stock", "Weight");
    }

    [Fact]
    public async Task ServiceDisablesProductWithoutRemovingIt()
    {
        var product = new Product { Id = 1, IsActive = true };
        var repository = new Mock<ICatalogRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.FindAsync<Product>(1, CancellationToken.None)).ReturnsAsync(product);
        repository.Setup(repo => repo.SaveAsync(CancellationToken.None)).Returns(Task.CompletedTask);

        await Service(repository.Object).DeleteProductAsync(1, CancellationToken.None);

        product.IsActive.Should().BeFalse();
        repository.Verify(repo => repo.SaveAsync(CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task ServiceReturnsNotFoundWithoutSavingIfProductDoesNotExist()
    {
        var repository = new Mock<ICatalogRepository>();
        repository.Setup(repo => repo.FindAsync<Product>(99, CancellationToken.None)).ReturnsAsync((Product?)null);

        var action = () => Service(repository.Object).DeleteProductAsync(99, CancellationToken.None);
        var error = await action.Should().ThrowAsync<CatalogException>();

        error.Which.StatusCode.Should().Be(404);
        repository.Verify(repo => repo.SaveAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ServiceRejectsMissingBrandBeforeAddingProduct()
    {
        var repository = new Mock<ICatalogRepository>();
        repository.Setup(repo => repo.FindAsync<Brand>(99, CancellationToken.None)).ReturnsAsync((Brand?)null);

        var action = () => Service(repository.Object).SaveProductAsync(null,
            new SaveProductRequest { BrandId = 99 }, CancellationToken.None);
        var error = await action.Should().ThrowAsync<CatalogException>();

        error.Which.StatusCode.Should().Be(400);
        repository.Verify(repo => repo.Add(It.IsAny<Product>()), Times.Never);
    }

    private static CatalogService Service(ICatalogRepository repository) => new(repository, Mock.Of<IMapper>(),
        new ProductImageStore(Mock.Of<IWebHostEnvironment>(), NullLogger<ProductImageStore>.Instance));
}
