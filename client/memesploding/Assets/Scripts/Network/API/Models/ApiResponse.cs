using System;

namespace Network.API.Models
{
    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public string message;
        public T data;
    }

}
