using System.Diagnostics.CodeAnalysis;

namespace SkillMuni.Common.Results;

/// <summary>Outcome of a use case that returns no value.</summary>
public class Result
{
    protected Result(Error? error)
    {
        Error = error;
    }

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => new(error);
}

/// <summary>Outcome of a use case that returns a value on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null)
    {
        _value = value;
    }

    private Result(Error error, bool _)
        : base(error)
    {
    }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error) => new(error, false);

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error, false);
}
