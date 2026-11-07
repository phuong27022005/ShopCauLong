using AutoMapper;
using BadmintonShop.Core.DTOs;
using BadmintonShop.Core.Entities;

namespace BadmintonShop.Api.Catalog;

public sealed class CatalogMappingProfile : Profile
{
    public CatalogMappingProfile()
    {
        CreateMap<Brand, BrandDto>();
        CreateMap<Category, CategoryDto>();
        CreateMap<Product, ProductListDto>();
        CreateMap<Product, ProductDetailDto>();
    }
}
