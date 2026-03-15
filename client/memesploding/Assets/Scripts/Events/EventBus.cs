using UnityEngine;

namespace Events
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    namespace GameEvents
    {
        /// <summary>
        /// Singleton Event Bus.
        /// Subscribe, unsubscribe, and publish typed events anywhere in the project.
        ///
        /// Usage:
        ///   EventBus.Subscribe(EventType.PlayerDied, OnPlayerDied);
        ///   EventBus.Publish(EventType.PlayerDied, new PlayerDiedPayload(transform.position));
        ///   EventBus.Unsubscribe(EventType.PlayerDied, OnPlayerDied);
        /// </summary>
        public class EventBus : MonoBehaviour
        {
            private static EventBus _instance;
            public static EventBus Instance
            {
                get
                {
                    if (_instance != null) return _instance;

                    var go = new GameObject("[EventBus]");
                    _instance = go.AddComponent<EventBus>();
                    DontDestroyOnLoad(go);
                    return _instance;
                }
            }

            private void Awake()
            {
                if (_instance != null && _instance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            private void OnApplicationQuit()
            {
                ClearAllListeners();
                if (_instance != null)
                    Destroy(_instance.gameObject);
            }

            private readonly Dictionary<EventType, Delegate> _listeners
                = new Dictionary<EventType, Delegate>();

            public static void Subscribe<T>(EventType type, Action<T> listener)
                where T : BaseEventPayload => Instance.AddListener(type, listener);

            public static void Unsubscribe<T>(EventType type, Action<T> listener)
                where T : BaseEventPayload => Instance.RemoveListener(type, listener);

            public static void Publish<T>(EventType type, T payload)
                where T : BaseEventPayload => Instance.Dispatch(type, payload);

            /// <summary>Remove ALL listeners for an event type. Use sparingly.</summary>
            public static void Clear(EventType type) => Instance.ClearListeners(type);

            /// <summary>Remove every listener for every event type.</summary>
            public static void ClearAll() => Instance.ClearAllListeners();

            private void AddListener<T>(EventType type, Action<T> listener)
                where T : BaseEventPayload
            {
                if (_listeners.TryGetValue(type, out var existing))
                    _listeners[type] = Delegate.Combine(existing, listener);
                else
                    _listeners[type] = listener;
            }

            private void RemoveListener<T>(EventType type, Action<T> listener)
                where T : BaseEventPayload
            {
                if (!_listeners.TryGetValue(type, out var existing)) return;

                var updated = Delegate.Remove(existing, listener);
                if (updated == null)
                    _listeners.Remove(type);
                else
                    _listeners[type] = updated;
            }

            private void Dispatch<T>(EventType type, T payload)
                where T : BaseEventPayload
            {
                if (!_listeners.TryGetValue(type, out var del)) return;

                if (del is Action<T> action)
                {
                    action.Invoke(payload);
                }
                else
                {
                    Debug.LogWarning(
                        $"[EventBus] Type mismatch for {type}. " +
                        $"Expected Action<{typeof(T).Name}> but got {del.GetType().Name}.");
                }
            }

            private void ClearListeners(EventType type) => _listeners.Remove(type);
            private void ClearAllListeners() => _listeners.Clear();
        }
    }
}
