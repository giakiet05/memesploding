using UnityEngine;

namespace Events
{
    public enum EventType
    {
        CardPlayedEvent,
        DrawCardEvent,
        WsConnected,
        WsAck,
        WsGameplayEvent,
        WsStateSnapshot,
        WsError,
        WsStatusChanged
    }
}
