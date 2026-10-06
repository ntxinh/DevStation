namespace DevStation.Application.Shared.Results;

public sealed class AppException(string code, int status, string message) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
