using System;
using Lux.Platform.Abstractions.Common;

namespace Lux.Platform.Abstractions.Common;

/// <summary>
/// ظ†ظ…ط· ط§ظ„ظ†طھظٹط¬ط© ظ„طھط¬ظ†ط¨ ط§ط³طھط®ط¯ط§ظ… Exceptions ظپظٹ ط§ظ„طھط­ظƒظ… ظپظٹ ط³ظٹط± ط§ظ„ط¹ظ…ظ„
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string ErrorMessage { get; }
    public ErrorType ErrorType { get; }
    public Exception? Exception { get; }

    public bool IsFailure => !IsSuccess;

    protected Result(bool isSuccess, string errorMessage, ErrorType errorType, Exception? exception)
    {
        if (isSuccess && errorType != ErrorType.None)
            throw new InvalidOperationException("Successful result cannot have an error type.");
            
        if (!isSuccess && errorType == ErrorType.None)
            throw new InvalidOperationException("Failure result must have an error type.");

        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
        ErrorType = errorType;
        Exception = exception;
    }

    public static Result Success() 
        => new(true, string.Empty, ErrorType.None, null);
    
    public static Result Failure(string errorMessage, ErrorType errorType, Exception? exception = null) 
        => new(false, errorMessage, errorType, exception);
}

/// <summary>
/// ظ†ظ…ط· ط§ظ„ظ†طھظٹط¬ط© ظ…ط¹ ط¥ط±ط¬ط§ط¹ ظ‚ظٹظ…ط©
/// </summary>
public class Result<T> : Result
{
    private readonly T? _value;
    
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access value of a failure result.");

    protected Result(T? value, bool isSuccess, string errorMessage, ErrorType errorType, Exception? exception) 
        : base(isSuccess, errorMessage, errorType, exception)
    {
        _value = value;
    }

    public static Result<T> Success(T value) 
        => new(value, true, string.Empty, ErrorType.None, null);
    
    public new static Result<T> Failure(string errorMessage, ErrorType errorType, Exception? exception = null) 
        => new(default, false, errorMessage, errorType, exception);
}
