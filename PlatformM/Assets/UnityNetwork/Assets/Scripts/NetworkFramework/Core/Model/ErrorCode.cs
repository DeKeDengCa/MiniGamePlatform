namespace NetworkFramework.Core.Model
{
    
    /// <summary>
    /// 错误码定义
    /// </summary>
    public static class ErrorCode
    {
        public const int SUCCESS = 0;
        public const int INVALID_URL = -1001;
        public const int REQUEST_TIMEOUT = -1002;
        public const int INTERNAL_PROCESS_ERROR = -1003;
        public const int PERSISTENT_CONNECTION_NOT_OK = -1004;
        public const int TOKEN_WAS_EMPTY = -1005;
        public const int HTTP_OK = 200;
        public const int INVALID_CODE = -1;
        public const int INVALID_REQUEST = -1000;
        public const int DATA_COMPRESS_FAIL = -2001;
        public const int DATA_DECOMPRESS_FAIL = -2002;
        
        
        
        public const int NETWORK_ERROR = 1002;
        public const int RESPONSE_PARSE_ERROR = 1004;
        public const int SERVER_ERROR = 1005;
        public const int CLIENT_ERROR = 1006;
        public const int UNKNOWN_ERROR = 1007;
        public const int CONNECTION_FAILED = 1008;
        public const int REQUEST_CANCELLED = 1009;
        public const int SEND_FAILED = 1010;
        public const int CONNECTION_CLOSED = 1011;
        public const int INVALID_DATA_FORMAT = 1012;
        public const int ADAPTER_ERROR = 1014;
        public const int HTTP_ERROR = 1015;
    }
}