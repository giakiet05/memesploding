using System;
using UnityEngine.Events;

namespace Events.Listeners
{
    [Serializable]
    public class CardPlayedUnityEvent : UnityEvent<CardPlayedEventPayload> { }

    [Serializable]
    public class TurnStartUnityEvent : UnityEvent<TurnStartEventPayload> { }

    [Serializable]
    public class SceneChangedUnityEvent : UnityEvent<SceneChangedEventPayload> { }

    [Serializable]
    public class WsStatusChangedUnityEvent : UnityEvent<WsStatusChangedEventPayload> { }
}
