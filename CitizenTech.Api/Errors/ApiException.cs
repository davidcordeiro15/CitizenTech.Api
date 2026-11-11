namespace CitizenTech.Api.Errors;

public sealed class ApiException(int statusCode, string code) : Exception(code)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
