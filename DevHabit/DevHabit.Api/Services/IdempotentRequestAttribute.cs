using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace DevHabit.Api.Services;

[AttributeUsage(AttributeTargets.Method)]
public sealed class IdempotentRequestAttribute : Attribute, IAsyncActionFilter
{
    private const string IdempotenceKeyHeader = "Idempotency-Key";
    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(60);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(
            IdempotenceKeyHeader,
            out StringValues idempotenceKeyValue) ||
            !Guid.TryParse(idempotenceKeyValue, out Guid idempotenceKey))
        {
            ProblemDetailsFactory problemDetailsFactory = context.HttpContext.RequestServices
                .GetRequiredService<ProblemDetailsFactory>();

            ProblemDetails problemDetails = problemDetailsFactory.CreateProblemDetails(
                context.HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request",
                detail: $"Invalid or missing {IdempotenceKeyHeader} header");

            context.Result = new BadRequestObjectResult(problemDetails);
            return;
        }

        // In production code you would want to use some kind of distributed cache. This is just for proof of concept. 
        IMemoryCache cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        string cacheKey = $"IdempotentRequest:{idempotenceKey}";

        int? statusCode = cache.Get<int?>(cacheKey);
        if (statusCode is not null)
        {
            var result = new StatusCodeResult(statusCode.Value);
            context.Result = result;
            return;
        }

        ActionExecutedContext executedContext = await next();

        if (executedContext.Result is ObjectResult objectResult)
        {
            cache.Set(cacheKey, objectResult.StatusCode, DefaultCacheDuration);
        }
    }
}
