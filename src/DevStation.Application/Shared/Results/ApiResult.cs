using Microsoft.AspNetCore.Http;

namespace DevStation.Application.Shared.Results;

public static class ApiResult
{
    public static IResult Ok<T>(T data)
        => TypedResults.Json(new ApiEnvelope<T>(data, null));

    public static IResult Fail(string code, string message, int status)
        => TypedResults.Json(new ApiEnvelope<object>(null, new ApiError(code, message)), statusCode: status);
}
