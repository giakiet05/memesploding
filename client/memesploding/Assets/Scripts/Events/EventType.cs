using UnityEngine;

namespace Events
{
    public enum EventType
    {
        CardPlayedEvent,
        DrawCardEvent,

        TurnStart,
        TurnEnd,

        WsConnected,
        WsAck,
        WsGameplayEvent,
        WsStateSnapshot,
        WsError,
        WsStatusChanged,
        SceneChanged
    }
}
