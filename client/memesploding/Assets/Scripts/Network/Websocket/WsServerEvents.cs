using System;
using Newtonsoft.Json;

namespace Network.Websocket
{
    [Serializable]
    public class WsServerEvent<T>
    {
        [JsonProperty("event")]
        public string @event;
        public T data;
        public DateTime timestamp;

        [JsonIgnore]
        public WsServerEventType eventType
        {
            get { return WsEventTypeParser.ParseServerEvent(@event); }
        }
    }
}
