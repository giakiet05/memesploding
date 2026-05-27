using Gameplay.Card;
using Network.Websocket;

namespace Events
{
    public abstract class BaseEventPayload { }

    public class CardPlayedEventPayload : BaseEventPayload
    {
        public CardPlayedEventPayload(BaseCard card, string playerID)
        {
            PlayedCard = card;
            PlayerID = playerID;
        }

        public BaseCard PlayedCard { get; }
        public string PlayerID { get; }
    }

    public class TurnStartEventPayload : BaseEventPayload
    {
        public TurnStartEventPayload(string userID)
        {
            UserID = userID;
        }

        public string UserID { get; }
    }

    public class TurnEndEventPayload : BaseEventPayload
    {
        public TurnEndEventPayload(string userID)
        {
            UserID = userID;
        }

        public string UserID { get; }
    }

    public class WsConnectedEventPayload : BaseEventPayload
    {
        public WsConnectedEventPayload(WsConnectedDto data)
        {
            Data = data;
        }

        public WsConnectedDto Data { get; }
    }

    public class WsAckEventPayload : BaseEventPayload
    {
        public WsAckEventPayload(WsAckDto data)
        {
            Data = data;
        }

        public WsAckDto Data { get; }
    }

    public class WsGameplayEventPayload : BaseEventPayload
    {
        public WsGameplayEventPayload(WsGameplayEventDto data)
        {
            Data = data;
            EventType = data != null ? data.EventType : WsGameplayEventType.Unknown;
            ParsedPayload = data != null ? data.ParsedPayload : null;
            RawType = data != null ? data.type : null;
            RawPayload = data != null ? data.payload : null;
            StateVersion = data != null ? data.stateVersion : 0;
        }

        public WsGameplayEventDto Data { get; }
        public WsGameplayEventType EventType { get; }
        public WsGameplayPayloadBase ParsedPayload { get; }
        public string RawType { get; }
        public string RawPayload { get; }
        public long StateVersion { get; }
    }

    public class WsStateSnapshotEventPayload : BaseEventPayload
    {
        public WsStateSnapshotEventPayload(WsStateSnapshotDto data, WsServerEventType serverEventType)
        {
            Data = data;
            ServerEventType = serverEventType;
        }

        public WsStateSnapshotDto Data { get; }
        public WsServerEventType ServerEventType { get; }
    }

    public class WsErrorEventPayload : BaseEventPayload
    {
        public WsErrorEventPayload(WsErrorDto data)
        {
            Data = data;
        }

        public WsErrorDto Data { get; }
    }

    public class WsStatusChangedEventPayload : BaseEventPayload
    {
        public WsStatusChangedEventPayload(WebsocketConnectionStatus status)
        {
            Status = status;
        }

        public WebsocketConnectionStatus Status { get; }
    }

    public class SceneChangedEventPayload : BaseEventPayload
    {
        public SceneChangedEventPayload(string fromScene, string toScene)
        {
            FromScene = fromScene;
            ToScene = toScene;
        }

        public string FromScene { get; }
        public string ToScene { get; }
    }
}
