using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BadmintonShop.Api.Catalog;

public sealed class ValidateCatalogRequestFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument),
                context.HttpContext.RequestAborted);
            if (result.IsValid) continue;

            context.Result = new BadRequestObjectResult(ApiResponse.Error(
                result.Errors[0].ErrorMessage, result.ToDictionary()));
            return;
        }

        await next();
    }
}
