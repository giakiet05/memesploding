using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
#endif
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Network.Websocket
{
    public class AppRoomWebsocketClient
    {
        private const char RecordSeparator = '';

        private static AppRoomWebsocketClient _instance;
        public static AppRoomWebsocketClient Instance => _instance ??= new AppRoomWebsocketClient();

        private readonly object _sync = new object();
        private readonly object _dispatcherSync = new object();

#if UNITY_WEBGL && !UNITY_EDITOR
        private NativeWebSocket.WebSocket _socket;
#else
        private ClientWebSocket _socket;
        private CancellationTokenSource _receiveCts;
        private Task _receiveTask;
#endif
        private EventQueueDispatcher _dispatcher;

        public AppRoomConnectionStatus Status { get; private set; } = AppRoomConnectionStatus.Disconnected;
        public AppRoomSessionInfo Session { get; } = new AppRoomSessionInfo();

        public event Action<AppRoomConnectionStatus> OnStatusChanged;
        public event Action<AppRoomMemberJoinedDto> OnRoomMemberJoined;
        public event Action<AppRoomMemberLeftDto> OnRoomMemberLeft;
        public event Action<AppRoomMemberKickedDto> OnRoomMemberKicked;
        public event Action<AppRoomReadyStatusChangedDto> OnRoomReadyStatusChanged;
        public event Action<AppRoomMatchStartingDto> OnRoomMatchStarting;
        public event Action<AppRoomHostChangedDto> OnRoomHostChanged;
        public event Action<AppRoomDissolvedDto> OnRoomDissolved;
        public event Action<AppRoomErrorDto> OnError;

        private AppRoomWebsocketClient()
        {
        }

        public async Task ConnectAsync(string accessToken, string roomCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentException("accessToken is required", nameof(accessToken));

            if (Status == AppRoomConnectionStatus.Connected || Status == AppRoomConnectionStatus.Connecting)
            {
                Debug.LogWarning("[RoomWS] ConnectAsync called while already connected — forcing disconnect first.");
                try { await DisconnectAsync(cancellationToken); } catch { }
            }

            CleanupSocket();
            EnsureDispatcher();
            SetStatus(AppRoomConnectionStatus.Connecting);

            Session.wsUrl = BuildAppHubWsUrl();
            Session.accessToken = accessToken;
            Session.roomCode = roomCode ?? string.Empty;
            Session.lastError = null;

            var uri = BuildUriWithAccessToken(Session.wsUrl, accessToken);

#if UNITY_WEBGL && !UNITY_EDITOR
            _socket = new NativeWebSocket.WebSocket(uri.ToString());

            _socket.OnOpen += async () =>
            {
                try
                {
                    await SendRawAsync("{\"protocol\":\"json\",\"version\":1}", CancellationToken.None);
                    Session.connectedAtUtc = DateTime.UtcNow;
                    SetStatus(AppRoomConnectionStatus.Connected);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[RoomWS WebGL] OnOpen error: {ex.Message}");
                    Session.lastError = ex.Message;
                    CleanupSocket();
                    SetStatus(AppRoomConnectionStatus.Faulted);
                }
            };

            _socket.OnMessage += (bytes) =>
            {
                var chunk = Encoding.UTF8.GetString(bytes);
                var messages = chunk.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries);
                foreach (var message in messages)
                    HandleSignalRMessage(message);
            };

            _socket.OnError += (errMsg) =>
            {
                Session.lastError = errMsg;
                CleanupSocket();
                SetStatus(AppRoomConnectionStatus.Faulted);
            };

            _socket.OnClose += (closeCode) =>
            {
                CleanupSocket();
                SetStatus(AppRoomConnectionStatus.Disconnected);
            };

            try
            {
                await _socket.Connect();
            }
            catch (Exception ex)
            {
                Session.lastError = ex.Message;
                CleanupSocket();
                SetStatus(AppRoomConnectionStatus.Faulted);
                throw;
            }
#else
            _socket = new ClientWebSocket();
            _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            try
            {
                await _socket.ConnectAsync(uri, cancellationToken);
                await SendRawAsync("{\"protocol\":\"json\",\"version\":1}", cancellationToken);

                _receiveCts = new CancellationTokenSource();
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token));
                Session.connectedAtUtc = DateTime.UtcNow;
                SetStatus(AppRoomConnectionStatus.Connected);
            }
            catch (Exception ex)
            {
                Session.lastError = ex.Message;
                CleanupSocket();
                SetStatus(AppRoomConnectionStatus.Faulted);
                throw;
            }
#endif
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _receiveCts?.Cancel();
#endif

            if (_socket != null)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                if (_socket.State == NativeWebSocket.WebSocketState.Open)
                {
                    try { await _socket.Close(); } catch { }
                }
#else
                if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                {
                    try
                    {
                        await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", cancellationToken);
                    }
                    catch { }
                }
#endif
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            if (_receiveTask != null)
            {
                try { await _receiveTask; } catch { }
            }
#endif

            CleanupSocket();
            SetStatus(AppRoomConnectionStatus.Disconnected);
        }

        public Task LeaveRoomAsync(string roomCode, CancellationToken cancellationToken = default)
        {
            return SendInvocationAsync("LeaveRoom", cancellationToken, roomCode);
        }

        public static async Task ForceLeaveRoomAsync(string accessToken, string roomCode)
        {
            if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(roomCode))
                return;

            try
            {
                Debug.Log($"[ForceLeaveRoom] Leaving room {roomCode} via REST...");
                var response = await Network.API.Services.RoomService.Instance.LeaveRoomAsync(roomCode, accessToken);
                if (response?.success == true)
                    Debug.Log($"[ForceLeaveRoom] Left room {roomCode} successfully.");
                else
                    Debug.LogWarning($"[ForceLeaveRoom] Leave room {roomCode}: errorCode={response?.errorCode} message={response?.message}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ForceLeaveRoom] Failed: {ex.Message}");
            }
        }

        public Task SetReadyStatusAsync(string roomCode, bool isReady, CancellationToken cancellationToken = default)
        {
            return SendInvocationAsync("SetReadyStatus", cancellationToken, roomCode, isReady);
        }

        public Task StartRoomMatchAsync(string roomCode, CancellationToken cancellationToken = default)
        {
            return SendInvocationAsync("StartRoomMatch", cancellationToken, roomCode);
        }

        public Task KickRoomMemberAsync(string roomCode, string targetUserId, CancellationToken cancellationToken = default)
        {
            return SendInvocationAsync("KickRoomMember", cancellationToken, roomCode, targetUserId);
        }

        private async Task SendInvocationAsync(string target, CancellationToken cancellationToken, params object[] arguments)
        {
            if (string.IsNullOrWhiteSpace(target))
                throw new ArgumentException("target is required", nameof(target));

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_socket == null || _socket.State != NativeWebSocket.WebSocketState.Open)
#else
            if (_socket == null || _socket.State != WebSocketState.Open)
#endif
                throw new InvalidOperationException("Room websocket is not connected");

            var invocation = new
            {
                type = 1,
                target,
                arguments = arguments ?? Array.Empty<object>()
            };

            await SendRawAsync(JObject.FromObject(invocation).ToString(), cancellationToken);
        }

        private async Task SendRawAsync(string jsonPayload, CancellationToken cancellationToken)
        {
            Debug.Log($"[RoomWS →] {jsonPayload}");
            var framed = jsonPayload + RecordSeparator;
#if UNITY_WEBGL && !UNITY_EDITOR
            await _socket.SendText(framed);
#else
            var bytes = Encoding.UTF8.GetBytes(framed);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
#endif
        }

#if !UNITY_WEBGL || UNITY_EDITOR
        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            var frameBuilder = new StringBuilder();

            try
            {
                while (!cancellationToken.IsCancellationRequested && _socket != null && _socket.State == WebSocketState.Open)
                {
                    var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        CleanupSocket();
                        SetStatus(AppRoomConnectionStatus.Disconnected);
                        return;
                    }

                    frameBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    if (!result.EndOfMessage)
                        continue;

                    var chunk = frameBuilder.ToString();
                    frameBuilder.Clear();

                    var messages = chunk.Split(RecordSeparator, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var message in messages)
                        HandleSignalRMessage(message);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Session.lastError = ex.Message;
                CleanupSocket();
                SetStatus(AppRoomConnectionStatus.Faulted);
                Debug.LogError($"[RoomWS] Receive loop error: {ex.Message}");
            }
        }
#endif

        private void HandleSignalRMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            Debug.Log($"[RoomWS ←] {message}");
            Session.lastMessageAtUtc = DateTime.UtcNow;

            if (message == "{}")
                return;

            JObject root;
            try
            {
                root = JObject.Parse(message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomWS] Cannot parse message: {ex.Message}");
                return;
            }

            var type = root.Value<int?>("type");
            if (type == 6)
                return;

            if (type == 7)
            {
                Session.lastError = root.Value<string>("error");
                SetStatus(AppRoomConnectionStatus.Faulted);
                return;
            }

            var target = root.GetValue("target", StringComparison.OrdinalIgnoreCase)?.Value<string>();
            if (!string.Equals(target, "ReceiveMessage", StringComparison.OrdinalIgnoreCase))
                return;

            var args = root.GetValue("arguments", StringComparison.OrdinalIgnoreCase) as JArray;
            if (args == null || args.Count == 0)
                return;

            var serverEvent = args[0] as JObject;
            if (serverEvent == null)
                return;

            var eventName = serverEvent.GetValue("event", StringComparison.OrdinalIgnoreCase)?.Value<string>();
            var dataToken = serverEvent.GetValue("data", StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(eventName) || dataToken == null)
                return;

            switch (eventName)
            {
                case "RoomMemberJoined":
                    Publish(() => OnRoomMemberJoined?.Invoke(dataToken.ToObject<AppRoomMemberJoinedDto>()));
                    break;
                case "RoomMemberLeft":
                    Publish(() => OnRoomMemberLeft?.Invoke(dataToken.ToObject<AppRoomMemberLeftDto>()));
                    break;
                case "RoomMemberKicked":
                    Publish(() => OnRoomMemberKicked?.Invoke(dataToken.ToObject<AppRoomMemberKickedDto>()));
                    break;
                case "RoomReadyStatusChanged":
                    Publish(() => OnRoomReadyStatusChanged?.Invoke(dataToken.ToObject<AppRoomReadyStatusChangedDto>()));
                    break;
                case "RoomMatchStarting":
                    Publish(() => OnRoomMatchStarting?.Invoke(dataToken.ToObject<AppRoomMatchStartingDto>()));
                    break;
                case "RoomHostChanged":
                    Publish(() => OnRoomHostChanged?.Invoke(dataToken.ToObject<AppRoomHostChangedDto>()));
                    break;
                case "RoomDissolved":
                    Publish(() => OnRoomDissolved?.Invoke(dataToken.ToObject<AppRoomDissolvedDto>()));
                    break;
                case "Error":
                    Publish(() =>
                    {
                        var error = dataToken.ToObject<AppRoomErrorDto>();
                        Session.lastError = error?.message;
                        OnError?.Invoke(error);
                    });
                    break;
            }
        }

        private void CleanupSocket()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            _receiveCts?.Dispose();
            _receiveCts = null;
#endif

            if (_socket != null)
            {
#if !UNITY_WEBGL || UNITY_EDITOR
                _socket.Dispose();
#endif
                _socket = null;
            }

#if !UNITY_WEBGL || UNITY_EDITOR
            _receiveTask = null;
#endif
        }

        private void SetStatus(AppRoomConnectionStatus status)
        {
            lock (_sync)
            {
                if (Status == status)
                    return;

                Status = status;
            }

            Publish(() => OnStatusChanged?.Invoke(status));
        }

        private void EnsureDispatcher()
        {
            if (_dispatcher != null)
                return;

            lock (_dispatcherSync)
            {
                if (_dispatcher == null)
                    _dispatcher = EventQueueDispatcher.Instance;
            }
        }

        private void Publish(Action action)
        {
            if (action == null)
                return;

            EnsureDispatcher();
            _dispatcher.Enqueue(action);
        }

        private static string BuildAppHubWsUrl()
        {
            var baseUrl = Config.Api.baseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("API baseUrl is not configured.");

            var apiUri = new Uri(baseUrl);
            var builder = new UriBuilder(apiUri)
            {
                Scheme = string.Equals(apiUri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws",
                Path = "/api/v1/ws",
                Query = string.Empty
            };

            return builder.Uri.ToString();
        }

        private static Uri BuildUriWithAccessToken(string wsUrl, string accessToken)
        {
            var builder = new UriBuilder(wsUrl);
            var tokenParam = $"access_token={Uri.EscapeDataString(accessToken)}";
            builder.Query = tokenParam;
            return builder.Uri;
        }

        private class EventQueueDispatcher : MonoBehaviour
        {
            private static EventQueueDispatcher _instance;
            private readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

            public static EventQueueDispatcher Instance
            {
                get
                {
                    if (_instance != null)
                        return _instance;

                    var gameObject = new GameObject("[RoomWsEventDispatcher]");
                    _instance = gameObject.AddComponent<EventQueueDispatcher>();
                    DontDestroyOnLoad(gameObject);
                    return _instance;
                }
            }

            public void Enqueue(Action action)
            {
                if (action != null)
                    _queue.Enqueue(action);
            }

            private void Update()
            {
                while (_queue.TryDequeue(out var action))
                {
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[RoomWS] Event dispatch error: {ex.Message}");
                    }
                }
            }
        }
    }
}
