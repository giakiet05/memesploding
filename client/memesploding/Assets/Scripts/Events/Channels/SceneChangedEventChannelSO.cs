using UnityEngine;

namespace Events.Channels
{
    [CreateAssetMenu(menuName = "Events/Channels/Scene Changed", fileName = "SceneChangedChannel")]
    public class SceneChangedEventChannelSO : BaseEventChannelSO<SceneChangedEventPayload> { }
}
