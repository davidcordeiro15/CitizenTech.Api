using CitizenTech.Api.Contracts;
using CitizenTech.Api.Data;
using CitizenTech.Api.Errors;
using CitizenTech.Api.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 65536);
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
}).ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = ContractMiddleware.InvalidModel;
    options.SuppressMapClientErrors = true;
});
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<ContractOpenApiTransformer>());
builder.Services.AddDbContext<GuidanceDbContext>(options =>
{
    var connection = builder.Configuration.GetConnectionString("Oracle");
    if (string.IsNullOrWhiteSpace(connection))
        options.UseOracle(oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19).CommandTimeout(5));
    else
        options.UseOracle(connection, oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19).CommandTimeout(5));
});
builder.Services.AddScoped<IGuidanceRecordStore, OracleGuidanceRecordStore>();
builder.Services.AddScoped<GuidanceService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AdminKeyFilter>();
builder.Services.AddScoped<ISessionEvaluationStore, SessionEvaluationStore>();
builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<AdminQueryService>();
builder.Services.AddGuidanceProviders(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("guidance", limiter =>
    {
        limiter.PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 30);
        limiter.Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60));
        limiter.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("admin", limiter =>
    {
        limiter.PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 30);
        limiter.Window = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:WindowSeconds", 60));
        limiter.QueueLimit = 0;
    });
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(ErrorResponse.Create("RATE_LIMITED",
            context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>()?.ControllerTypeInfo.AsType() == typeof(CitizenTech.Api.Controllers.AdminController)
                ? context.HttpContext.Items["RequestId"] as Guid? : null), token);
    };
});
var app = builder.Build();
app.UseMiddleware<ContractMiddleware>();
app.UseStatusCodePages(async statusContext =>
{
    var http = statusContext.HttpContext;
    if (http.Response.StatusCode == 415) http.Response.StatusCode = 400;
    var code = http.Response.StatusCode == 413 ? "REQUEST_TOO_LARGE" :
        http.Response.StatusCode is >= 400 and < 500 ? "INVALID_REQUEST" : "INTERNAL_ERROR";
    await http.Response.WriteAsJsonAsync(ErrorResponse.Create(code, http.Items["RequestId"] as Guid?), http.RequestAborted);
});
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "CitizenTech v1"));
}
app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapControllers();
app.Run();

public partial class Program;
