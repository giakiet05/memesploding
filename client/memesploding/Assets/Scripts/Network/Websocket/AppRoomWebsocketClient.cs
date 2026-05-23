using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Network.Websocket
{
    public class AppRoomWebsocketClient
    {
        private const char RecordSeparator = '\u001e';

        private static AppRoomWebsocketClient _instance;
        public static AppRoomWebsocketClient Instance => _instance ??= new AppRoomWebsocketClient();

        private readonly object _sync = new object();
        private readonly object _dispatcherSync = new object();

        private ClientWebSocket _socket;
        private CancellationTokenSource _receiveCts;
        private Task _receiveTask;
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
                return;

            CleanupSocket();
            EnsureDispatcher();
            SetStatus(AppRoomConnectionStatus.Connecting);

            Session.wsUrl = BuildAppHubWsUrl();
            Session.accessToken = accessToken;
            Session.roomCode = roomCode ?? string.Empty;
            Session.lastError = null;

            _socket = new ClientWebSocket();
            _socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);

            try
            {
                await _socket.ConnectAsync(BuildUriWithAccessToken(Session.wsUrl, accessToken), cancellationToken);
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
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _receiveCts?.Cancel();

            if (_socket != null && (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived))
            {
                try
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", cancellationToken);
                }
                catch
                {
                }
            }

            if (_receiveTask != null)
            {
                try
                {
                    await _receiveTask;
                }
                catch
                {
                }
            }

            CleanupSocket();
            SetStatus(AppRoomConnectionStatus.Disconnected);
        }

        public Task LeaveRoomAsync(string roomCode, CancellationToken cancellationToken = default)
        {
            return SendInvocationAsync("LeaveRoom", cancellationToken, roomCode);
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

            if (_socket == null || _socket.State != WebSocketState.Open)
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
            var framed = jsonPayload + RecordSeparator;
            var bytes = Encoding.UTF8.GetBytes(framed);
            await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
        }

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

        private void HandleSignalRMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

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

            var target = root.Value<string>("target");
            if (!string.Equals(target, "ReceiveMessage", StringComparison.Ordinal))
                return;

            var args = root["arguments"] as JArray;
            if (args == null || args.Count == 0)
                return;

            var serverEvent = args[0] as JObject;
            if (serverEvent == null)
                return;

            var eventName = serverEvent.Value<string>("event");
            var dataToken = serverEvent["data"];
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
            _receiveCts?.Dispose();
            _receiveCts = null;

            if (_socket != null)
            {
                _socket.Dispose();
                _socket = null;
            }

            _receiveTask = null;
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
                Path = "/ws",
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
