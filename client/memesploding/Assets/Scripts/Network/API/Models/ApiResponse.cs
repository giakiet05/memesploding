using System;
using Newtonsoft.Json.Linq;

namespace Network.API.Models
{
    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public string message;
        public string errorCode;
        public JObject details;
        public T data;
    }
}
