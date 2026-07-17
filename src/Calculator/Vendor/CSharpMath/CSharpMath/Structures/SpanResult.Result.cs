using System;
using CSharpMath.Structures;

namespace CSharpMath.Structures;

public readonly ref struct SpanResult<T>
{
    public SpanResult(ReadOnlySpan<T> value)
    {
        _value = value;
        Error = null;
    }

    public SpanResult(ResultImplicitError error)
    {
        _value = default;
        Error = error.Error;
    }

    private readonly ReadOnlySpan<T> _value;
    public string? Error { get; }

    public void Deconstruct(out ReadOnlySpan<T> value, out string? error)
    {
        value = _value;
        error = Error;
    }

    public void Match(SpanResultAction<T> successAction, System.Action<string> errorAction)
    {
        System.ArgumentNullException.ThrowIfNull(errorAction);
        System.ArgumentNullException.ThrowIfNull(successAction);
        if (Error != null)
            errorAction(Error);
        else
            successAction(_value);
    }

    public TResult Match<TResult>(SpanResultFunc<T, TResult> successAction, System.Func<string, TResult> errorAction)
    {
        System.ArgumentNullException.ThrowIfNull(errorAction);
        System.ArgumentNullException.ThrowIfNull(successAction);
        return Error != null ? errorAction(Error) : successAction(_value);
    }
    public Result Bind(SpanResultAction<T> method)
    {
        System.ArgumentNullException.ThrowIfNull(method);
        if (Error is { } error)
            return error;
        method(_value);
        return Result.Ok();
    }

    public Result Bind(SpanResultFunc<T, Result> method)
    {
        System.ArgumentNullException.ThrowIfNull(method);
        return Error ?? method(_value);
    }
    public Result<TResult> Bind<TResult>(SpanResultFunc<T, TResult> method)
    {
        System.ArgumentNullException.ThrowIfNull(method);
        return Error is { } error
            ? new Result<TResult>(Result.Err(error))
            : new Result<TResult>(method(_value));
    }
    public Result<TResult> Bind<TResult>(SpanResultFunc<T, Result<TResult>> method)
    {
        System.ArgumentNullException.ThrowIfNull(method);
        return Error is { } error
            ? new Result<TResult>(Result.Err(error))
            : method(_value);
    }
}
