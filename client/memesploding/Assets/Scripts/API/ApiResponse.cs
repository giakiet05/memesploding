using System;
using UnityEngine;

namespace API
{
    [Serializable]
    public class ApiResponse<T>
    {
        public string message;
        public T data;
    }

}