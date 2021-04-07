﻿using System;

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

        public static Result Fail(FileSystemError errorType)
        {
            return new Result(errorType);
        }

        public static Result<TValue> Fail<TValue>(FileSystemError errorType)
        {
            return new Result<TValue>(errorType);
        }

        public FileSystemError? ErrorType { get; protected set; }

        public bool IsSuccessful() => ErrorType == null;
    }

    public class Result<TValue>
    {
        public Result(TValue value)
        {
            this.value = value;
        }

        public Result(FileSystemError? errorType)
        {
            ErrorType = errorType;
        }

        public static implicit operator Result<TValue>(Result result)
        {
            return new Result<TValue>(result.ErrorType);
        }

        public TValue Value
        {
            get
            {
                if (IsSuccessful())
                    return value;

                throw new InvalidOperationException($"Result isn't successful. Error type: {ErrorType}");
            }
        }

        private readonly TValue value = default!;

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