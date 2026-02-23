using System;

namespace Credo.BlobStorage.Client
{
    public class BlobStorageException : Exception
    {
        public string ErrorCode { get; }
        public int StatusCode { get; }

        public BlobStorageException(string errorCode, string message, int statusCode)
            : base(message)
        {
            ErrorCode = errorCode;
            StatusCode = statusCode;
        }
    }
}
