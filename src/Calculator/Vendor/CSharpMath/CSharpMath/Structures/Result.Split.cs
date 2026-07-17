using System;
using CSharpMath.Structures;

namespace CSharpMath.Structures;

public readonly record struct Result<T>
{
    public Result(T value) => (Value, Error) = (value, null);
    public Result(ResultImplicitError error) => (Value, Error) = (default!, error.Error);
    internal readonly T Value;
    public string? Error { get; }

    public void Deconstruct(out T value, out string? error) => (value, error) = (Value, Error);
    public void Match(Action<T> successAction, Action<string> errorAction)
    {
        System.ArgumentNullException.ThrowIfNull(errorAction);
        System.ArgumentNullException.ThrowIfNull(successAction);
        if (Error != null)
            errorAction(Error);
        else
            successAction(Value);
    }

    public TResult Match<TResult>(Func<T, TResult> successFunc, Func<string, TResult> errorFunc)
    {
        System.ArgumentNullException.ThrowIfNull(errorFunc);
        System.ArgumentNullException.ThrowIfNull(successFunc);
        return Error != null ? errorFunc(Error) : successFunc(Value);
    }
    public Result Bind(Action<T> method)
    {
        System.ArgumentNullException.ThrowIfNull(method);
        if (Error is { } error)
            return error;
        method(Value);
        return Result.Ok();
    }

    public Result Bind(Func<T, Result> method)
    {
        System.ArgumentNullException.ThrowIfNull(method);
        return Error ?? method(Value);
    }
    public Result<TResult> Bind<TResult>(Func<T, TResult> method)
    {
        System.ArgumentNullException.ThrowIfNull(method);
        return Error is { } error
            ? new Result<TResult>(Result.Err(error))
            : new Result<TResult>(method(Value));
    }
    public Result<TResult> Bind<TResult>(Func<T, Result<TResult>> method)
    {
        System.ArgumentNullException.ThrowIfNull(method);
        return Error is { } error
            ? new Result<TResult>(Result.Err(error))
            : method(Value);
    }
}
