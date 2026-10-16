namespace SESMT.Domain.Common.Result;

public sealed class Result<T> where T : notnull
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(true, value, null);
    }

    public static Result<T> Failure(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("A mensagem de erro é obrigatória.", nameof(error));

        return new(false, default, error);
    }

    // Helpers úteis no código de aplicação
    public bool IsFailure => !IsSuccess;

    public T ValueOrThrow() =>
        IsSuccess
            ? Value!
            : throw new InvalidOperationException(
                $"Não é possível obter o valor de um resultado de falha: {Error}");

    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) where TOut : notnull =>
        IsSuccess
            ? Result<TOut>.Success(mapper(Value!))
            : Result<TOut>.Failure(Error!);

    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder) where TOut : notnull =>
        IsSuccess
            ? binder(Value!)
            : Result<TOut>.Failure(Error!);
}