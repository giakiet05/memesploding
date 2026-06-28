using System;

namespace Network.API
{
    [Serializable]
    public class ApiConfig
    {
        public string baseUrl;
        public int timeout;
    }
}