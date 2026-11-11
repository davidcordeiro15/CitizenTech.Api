using System.Security.Cryptography;
using System.Text;
using CitizenTech.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CitizenTech.Api.Errors;

// Authorization filter runs before binding/validation. Rate limiting runs before MVC, including rejected keys.
public sealed class AdminKeyFilter(IConfiguration configuration) : IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var expected = configuration["Admin:ApiKey"];
        var headers = context.HttpContext.Request.Headers;
        var status = string.IsNullOrWhiteSpace(expected) ? 503 : 401;
        var code = status == 503 ? "ADMIN_NOT_CONFIGURED" : "UNAUTHORIZED";
        if (status != 503 && headers.TryGetValue("X-Admin-Key", out var values) && values.Count == 1
            && !string.IsNullOrEmpty(values[0]) && !values[0]!.Contains(',')
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected!)),
                SHA256.HashData(Encoding.UTF8.GetBytes(values[0]!))))
            return Task.CompletedTask;
        context.Result = new ObjectResult(ErrorResponse.Create(code,
            context.HttpContext.Items["RequestId"] as Guid?)) { StatusCode = status };
        return Task.CompletedTask;
    }
}
