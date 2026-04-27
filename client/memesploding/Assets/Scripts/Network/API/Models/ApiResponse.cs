using System;

namespace Network.API.Models
{
    [Serializable]
    public class ApiResponse<T>
    {
        public string message;
        public T data;
    }

}