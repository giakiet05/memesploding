using System;
using System.Collections.Generic;
using UnityEngine;

namespace Events.Channels
{
    public abstract class BaseEventChannelSO<TPayload> : ScriptableObject
    {
        private readonly List<Action<TPayload>> _listeners = new List<Action<TPayload>>();

        public void Raise(TPayload payload)
        {
            var listeners = _listeners.ToArray();
            for (var i = 0; i < listeners.Length; i++)
            {
                listeners[i]?.Invoke(payload);
            }
        }

        public void Register(Action<TPayload> listener)
        {
            if (listener == null || _listeners.Contains(listener))
            {
                return;
            }

            _listeners.Add(listener);
        }

        public void Unregister(Action<TPayload> listener)
        {
            if (listener == null)
            {
                return;
            }

            _listeners.Remove(listener);
        }
    }
}
