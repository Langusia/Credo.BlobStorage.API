namespace Credo.BlobStorage.Client
{
    public class BlobStorageResult
    {
        public bool IsSuccess { get; }
        public int StatusCode { get; }
        public string? ErrorCode { get; }
        public string? ErrorMessage { get; }

        protected BlobStorageResult(bool isSuccess, int statusCode, string? errorCode, string? errorMessage)
        {
            IsSuccess = isSuccess;
            StatusCode = statusCode;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public void EnsureSuccess()
        {
            if (!IsSuccess)
                throw new BlobStorageException(ErrorCode ?? "Unknown", ErrorMessage ?? "Request failed.", StatusCode);
        }

        internal static BlobStorageResult Success(int statusCode)
            => new BlobStorageResult(true, statusCode, null, null);

        internal static BlobStorageResult Failure(int statusCode, string? errorCode, string? errorMessage)
            => new BlobStorageResult(false, statusCode, errorCode, errorMessage);
    }

    public class BlobStorageResult<T> : BlobStorageResult
    {
        private readonly T _value;

        public T Value
        {
            get
            {
                if (!IsSuccess)
                    throw new BlobStorageException(ErrorCode ?? "Unknown", ErrorMessage ?? "Request failed.", StatusCode);
                return _value;
            }
        }

        internal BlobStorageResult(bool isSuccess, int statusCode, T value, string? errorCode, string? errorMessage)
            : base(isSuccess, statusCode, errorCode, errorMessage)
        {
            _value = value;
        }

        internal static BlobStorageResult<T> Success(int statusCode, T value)
            => new BlobStorageResult<T>(true, statusCode, value, null, null);

        internal new static BlobStorageResult<T> Failure(int statusCode, string? errorCode, string? errorMessage)
            => new BlobStorageResult<T>(false, statusCode, default!, errorCode, errorMessage);
    }
}
