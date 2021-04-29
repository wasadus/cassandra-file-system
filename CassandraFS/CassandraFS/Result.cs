using System;

namespace CassandraFS
{
    public class Result
    {
        public Result()
        {
        }

        public Result(FileSystemError? errorType)
        {
            ErrorType = errorType;
        }

        public static Result Ok()
        {
            return new Result();
        }

        public static Result<TValue> Ok<TValue>(TValue value)
        {
            return new Result<TValue>(value);
        }

        public static Result Fail(FileSystemError? errorType)
        {
            return new Result(errorType);
        }

        public static Result<TValue> Fail<TValue>(FileSystemError? errorType)
        {
            return new Result<TValue>(errorType);
        }

        public Result<TOutput> Then<TOutput>(Func<TOutput> continuation)
        {
            return IsSuccessful() ? Ok(continuation()) : Fail<TOutput>(ErrorType);
        }

        public Result Then(Func<Result> continuation)
        {
            return IsSuccessful() ? continuation() : Fail(ErrorType);
        }

        public Result Check(Func<bool> condition, FileSystemError errorType)
        {
            return !IsSuccessful() || condition() ? this : Fail(errorType);
        }

        public FileSystemError? ErrorType { get; protected set; }

        public bool IsSuccessful() => ErrorType == null;
    }

    public class Result<TValue>
    {
        public Result(TValue value)
        {
            Value = value;
        }

        public Result(FileSystemError? errorType)
        {
            ErrorType = errorType;
        }

        public static implicit operator Result<TValue>(Result result)
        {
            return new Result<TValue>(result.ErrorType);
        }

        public Result<TOutput> Then<TOutput>(Func<TValue, Result<TOutput>> continuation)
        {
            return IsSuccessful() ? continuation(Value) : Result.Fail<TOutput>(ErrorType);
        }

        public Result<TValue> Check(Func<TValue, bool> condition, FileSystemError errorType)
        {
            return !IsSuccessful() || condition(Value) ? this : Result.Fail<TValue>(errorType);
        }

        public Result<TValue> Check(Func<TValue, bool> condition, Func<Result<TValue>> elseFunc)
        {
            return !IsSuccessful() || condition(Value) ? this : elseFunc();
        }

        public Result Then(Func<TValue, Result> continuation)
        {
            return IsSuccessful() ? continuation(Value) : Result.Fail(ErrorType);
        }

        public TValue Value { get; }

        public FileSystemError? ErrorType { get; }

        public bool IsSuccessful() => ErrorType == null;
    }

    public enum FileSystemError
    {
        IsDirectory,
        NotDirectory,
        NoEntry,
        AlreadyExist,
        DirectoryNotEmpty,
        InvalidArgument,
        NoAttribute,
        OutOfRange,
        PermissionDenied,
        AccessDenied,
    }
}