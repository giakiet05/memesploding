using System;
using UnityEngine;

namespace API
{
    [Serializable]
    public class ApiConfig
    {
        public string baseUrl;
        public int timeout;
    }
}