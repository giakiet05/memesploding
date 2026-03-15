using UnityEngine;

namespace API.Services
{
    public class UserService
    {
        private static UserService _instance;

        public static UserService Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new UserService();

                return _instance;
            }
        }

        private string _baseURL = Config.Api.baseUrl + "/users";

        private UserService() { }
    }
}