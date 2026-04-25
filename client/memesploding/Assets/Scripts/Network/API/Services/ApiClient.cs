using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace API.Services
{
    public class ApiClient
    {
        private static ApiClient _instance;
        public static ApiClient Instance => _instance ??= new ApiClient();

        private ApiClient() { }

        public async Task<ApiResponse<T>> GetAsync<T>(string url, string token = null)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                SetHeaders(request, token);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                return HandleResponse<T>(request);
            }
        }

        public async Task<ApiResponse<T>> PostAsync<T>(string url, object body, string token = null)
        {
            return await SendRequestAsync<T>(url, "POST", body, token);
        }

        public async Task<ApiResponse<T>> PutAsync<T>(string url, object body, string token = null)
        {
            return await SendRequestAsync<T>(url, "PUT", body, token);
        }

        private async Task<ApiResponse<T>> SendRequestAsync<T>(string url, string method, object body, string token)
        {
            string json = JsonConvert.SerializeObject(body);

            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();

                SetHeaders(request, token);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                return HandleResponse<T>(request);
            }
        }

        private void SetHeaders(UnityWebRequest request, string token)
        {
            request.SetRequestHeader("Content-Type", "application/json");

            if (!string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", "Bearer " + token);
        }

        private ApiResponse<T> HandleResponse<T>(UnityWebRequest request)
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                return JsonConvert.DeserializeObject<ApiResponse<T>>(request.downloadHandler.text);
            }

            Debug.LogError($"[API Error] {request.error} | Response: {request.downloadHandler.text}");
            return null;
        }
    }
}