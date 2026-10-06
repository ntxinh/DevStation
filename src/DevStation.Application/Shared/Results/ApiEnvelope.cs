namespace DevStation.Application.Shared.Results;

public sealed record ApiEnvelope<T>(T? Data, ApiError? Error);

public sealed record ApiError(string Code, string Message);
