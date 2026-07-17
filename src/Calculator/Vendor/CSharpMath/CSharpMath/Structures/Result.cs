using System;
using CSharpMath.Structures;

namespace CSharpMath.Structures;

public readonly record struct Result
{
    public Result(string error) =>
        Error = error ?? throw new ArgumentNullException(nameof(error));

    public static Result Ok() => new();
    public static Result<T> Ok<T>(T value) => new(value);
    public static SpanResult<T> Ok<T>(ReadOnlySpan<T> value) => new(value);
    public static ResultImplicitError Err(string error) => new(error);
    public string? Error { get; }

    public void Match(Action successAction, Action<string> errorAction)
    {
        System.ArgumentNullException.ThrowIfNull(errorAction);
        System.ArgumentNullException.ThrowIfNull(successAction);
        if (Error != null)
            errorAction(Error);
        else
            successAction();
    }

    public TResult Match<TResult>(Func<TResult> successFunc, Func<string, TResult> errorFunc)
    {
        System.ArgumentNullException.ThrowIfNull(errorFunc);
        System.ArgumentNullException.ThrowIfNull(successFunc);
        return Error != null ? errorFunc(Error) : successFunc();
    }
    public Result Bind<T>(Action successAction)
    {
        System.ArgumentNullException.ThrowIfNull(successAction);
        if (Error != null)
            return Error;
        successAction();
        return Ok();
    }

    public Result<T> Bind<T>(Func<T> successAction)
    {
        System.ArgumentNullException.ThrowIfNull(successAction);
        return Error is { } error
            ? new Result<T>(Err(error))
            : new Result<T>(successAction());
    }
    public Result Bind(Func<Result> successAction)
    {
        System.ArgumentNullException.ThrowIfNull(successAction);
        return Error ?? successAction();
    }
    public Result<T> Bind<T>(Func<Result<T>> successAction)
    {
        System.ArgumentNullException.ThrowIfNull(successAction);
        return Error is { } error
            ? new Result<T>(Err(error))
            : successAction();
    }
    public static Result FromString(string error) => new(error);
    public static Result FromResultImplicitError(ResultImplicitError error) => new(error.Error);
    public static implicit operator Result(string error) => FromString(error);
    public static implicit operator Result(ResultImplicitError error) => FromResultImplicitError(error);
}
