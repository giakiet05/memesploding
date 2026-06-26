using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Network.API.Models;
using UnityEngine;
using UnityEngine.Networking;

namespace Network.API.Services
{
    public class ApiClient
    {
        private static ApiClient _instance;
        public static ApiClient Instance => _instance ??= new ApiClient();

        private ApiClient() { }

        public async Task<ApiResponse<T>> GetAsync<T>(string url, string token = null)
        {
            Debug.Log($"[API →] GET {url}");
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

        public async Task<ApiResponse<T>> PatchAsync<T>(string url, object body = null, string token = null)
        {
            return await SendRequestAsync<T>(url, "PATCH", body, token);
        }

        public async Task<ApiResponse<T>> DeleteAsync<T>(string url, string token = null)
        {
            Debug.Log($"[API →] DELETE {url}");
            using (UnityWebRequest request = UnityWebRequest.Delete(url))
            {
                SetHeaders(request, token);

                var operation = request.SendWebRequest();
                while (!operation.isDone) await Task.Yield();

                return HandleResponse<T>(request);
            }
        }

        private async Task<ApiResponse<T>> SendRequestAsync<T>(string url, string method, object body, string token)
        {
            string json = JsonConvert.SerializeObject(body ?? new object());
            Debug.Log($"[API →] {method} {url} | body: {json}");

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
            var responseText = request.downloadHandler?.text;
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[API ←] {request.responseCode} {request.method} {request.url} | {responseText}");
                var response = JsonConvert.DeserializeObject<ApiResponse<T>>(responseText);
                if (response != null)
                    response.success = true;

                return response;
            }

            var message = TryReadErrorMessage(responseText);
            Debug.LogError($"[API ←] ERROR {request.responseCode} {request.method} {request.url} | {responseText}");

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                try
                {
                    var response = JsonConvert.DeserializeObject<ApiResponse<T>>(responseText);
                    if (response != null)
                    {
                        response.success = false;
                        if (string.IsNullOrWhiteSpace(response.message))
                            response.message = string.IsNullOrWhiteSpace(message) ? request.error : message;
                        return response;
                    }
                }
                catch
                {
                }
            }

            return new ApiResponse<T>
            {
                success = false,
                message = string.IsNullOrWhiteSpace(message) ? request.error : message,
                data = default
            };
        }

        private string TryReadErrorMessage(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText))
                return null;

            try
            {
                var response = JsonConvert.DeserializeObject<ApiResponse<object>>(responseText);
                if (!string.IsNullOrWhiteSpace(response?.message))
                    return response.message;
            }
            catch
            {
                return responseText;
            }

            return responseText;
        }
    }
}
