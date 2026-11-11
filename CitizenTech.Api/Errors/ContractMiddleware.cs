using CitizenTech.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace CitizenTech.Api.Errors;

public sealed class ContractMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Items["StartedAt"] = Stopwatch.GetTimestamp();
        if (context.Request.Path.StartsWithSegments("/api/v1/admin") ||
            context.Request.Path.StartsWithSegments("/api/v1/sessions"))
            context.Items["RequestId"] = Guid.NewGuid();
        try
        {
            if (HttpMethods.IsPost(context.Request.Method) && context.Request.Path == "/api/v1/guidance")
            {
                if (context.Request.ContentLength > 65536) throw new ApiException(413, "REQUEST_TOO_LARGE");
                // Lê no máximo 64 KiB + 1, inclusive para transferência chunked. Não grava corpo em disco/log.
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                deadline.CancelAfter(TimeSpan.FromSeconds(20));
                var body = new MemoryStream();
                await using var ownedBody = body;
                var buffer = new byte[4096];
                int read;
                while ((read = await context.Request.Body.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, 65537 - (int)body.Length)), deadline.Token)) > 0)
                {
                    await body.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
                    if (body.Length > 65536) throw new ApiException(413, "REQUEST_TOO_LARGE");
                }
                body.Position = 0;
                try
                {
                    using var json = await System.Text.Json.JsonDocument.ParseAsync(body, cancellationToken: deadline.Token);
                    if (json.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                        && json.RootElement.TryGetProperty("requestId", out var id)
                        && id.ValueKind == System.Text.Json.JsonValueKind.String && id.TryGetGuid(out var guid))
                        context.Items["RequestId"] = guid;
                }
                catch (System.Text.Json.JsonException) { throw new ApiException(400, "INVALID_REQUEST"); }
                body.Position = 0;
                var original = context.Request.Body;
                context.Request.Body = body;
                try { await next(context); }
                finally { context.Request.Body = original; }
                return;
            }
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
        catch (Exception ex)
        {
            var (status, code) = ex switch
            {
                ApiException api => (api.StatusCode, api.Code),
                BadHttpRequestException bad when bad.StatusCode == 413 => (413, "REQUEST_TOO_LARGE"),
                BadHttpRequestException => (400, "INVALID_REQUEST"),
                OperationCanceledException => (400, "INVALID_REQUEST"),
                _ => (500, "INTERNAL_ERROR")
            };
            // Nunca serializa exceções, conteúdo recebido ou informações do provedor/banco.
            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(ErrorResponse.Create(code,
                    context.Items["RequestId"] as Guid?), context.RequestAborted);
            }
        }
    }

    public static IActionResult InvalidModel(ActionContext context)
    {
        Guid? requestId = context.HttpContext.Items["RequestId"] as Guid?;
        return new BadRequestObjectResult(ErrorResponse.Create("INVALID_REQUEST", requestId));
    }
}
