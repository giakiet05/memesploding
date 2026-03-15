using System;
using UnityEngine;

namespace API
{
    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public string message;
        public T data;
        public string error_code;
    }

}