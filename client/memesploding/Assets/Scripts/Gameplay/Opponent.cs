using Events;
using Managers;
using ScriptableObjects;
using System;
using System.Collections;
using Events.GameEvents;
using UnityEngine;
using EventType = Events.EventType;
using Random = UnityEngine.Random;

namespace Gameplay
{
    public class Opponent : MonoBehaviour
    {
        [SerializeField] private RectTransform playArea;
        [SerializeField] private RectTransform spawnPoint;

        [SerializeField] private float jumpHeight = 150f;
        [SerializeField] private float duration = 0.5f;

        //For testing only
        [SerializeField] private bool autoPlay = false;

        private void Start()
        {
            if (autoPlay)
                StartCoroutine(AutoPlay());
        }

        private IEnumerator AutoPlay()
        {
            while (true)
            {
                PlayCard("DEFUSE");
                yield return new WaitForSeconds(2f);
            }
        }


        public void PlayCard(string cardName)
        {
            StartCoroutine(PlayCardRoutine(cardName));
        }

        private IEnumerator PlayCardRoutine(string cardName)
        {
            Card card = CardManager.Instance.CreateCard(cardName, playArea.transform);

            RectTransform rect = card.RectTransform;
            rect.localScale = Vector3.zero;
            rect.position = spawnPoint.position;

            Vector2 target = RandomPosition(playArea);

            Vector2 start = rect.anchoredPosition;

            float time = 0f;

            while (time < duration)
            {
                float t = time / duration;

                Vector2 pos = Vector2.Lerp(start, target, t);

                float arc = Mathf.Sin(t * Mathf.PI) * jumpHeight;
                pos.y += arc;

                rect.anchoredPosition = pos;

                rect.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t);

                time += Time.deltaTime;
                yield return null;
            }

            rect.anchoredPosition = target;
            rect.localScale = Vector3.one;

            //Publish event
            //TODO: Change player name
            CardPlayedEventPayload payload = new CardPlayedEventPayload(card, "Vak0506");
            EventBus.Publish(EventType.CardPlayedEvent, payload);
        }

        private Vector2 RandomPosition(RectTransform rect)
        {
            float width = rect.rect.width;
            float height = rect.rect.height;

            float x = Random.Range(-width * 0.5f, width * 0.5f);
            float y = Random.Range(-height * 0.5f, height * 0.5f);

            return new Vector2(x, y);
        }
    }
}
