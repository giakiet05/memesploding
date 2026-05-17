using Events;
using Gameplay;
using Network.Websocket;
using System;
using System.Collections.Generic;
using Events.GameEvents;
using UI;  
using UI.Gameplay;
  
using UnityEngine;
using EventType = Events.EventType;

namespace Managers
{
    public class GameplayUIManager : MonoBehaviour
    {
        public static GameplayUIManager Instance;
        public static event Action<string> OnOpponentProfileSelected;   

        private void Awake()
        {
            if (Instance != null && Instance != this)
                Destroy(gameObject);
            else
                Instance = this;
        }

        [SerializeField] private Canvas canvas;
        [SerializeField] private RectTransform playingArea;
        [SerializeField] private RectTransform uiArea;

        [SerializeField] private CardDisplayer cardDisplayer;
        [SerializeField] private CardSelector cardSelector;
        [SerializeField] private DrawnCardDisplayer drawnCardDisplayer;

        [Header("Opponent Organization")]
        [SerializeField] private OpponentProfile opponentProfilePrefab;
        [SerializeField] private RectTransform fanCenter;   // assign in inspector
        [SerializeField] private float radiusX = 400f; // horizontal spread
        [SerializeField] private float radiusY = 200f; // vertical height
        [SerializeField, Range(0f, 360f)] private float totalAngle = 180f;
        [SerializeField] private bool autoCenter = true;
        [SerializeField] private float startAngle = -90f;

        private Dictionary<string, OpponentProfile> _opponentsUI;

        //TODO: Add loading screen

        // For testing only
        //private void Start()
        //{
        //    var players = new List<WsPlayerPublicStateDto>();

        //    // Fake current player
        //    string myId = "P0";

        //    // Inject into your GameManager for the test
        //    //GameManager.Instance.Player = new Player { ID = myId };

        //    // Create dummy players (including yourself)
        //    for (int i = 0; i <= 3; i++)
        //    {
        //        players.Add(new WsPlayerPublicStateDto
        //        {
        //            userId = "P" + i,
        //            // add other fields if your UI needs them
        //        });
        //    }

        //    InitOpponentUI(players);
        //}

        private void Start()
        {
            EventBus.Subscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
            
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<CardPlayedEventPayload>(EventType.CardPlayedEvent, OnCardPlayed);
        }

        public void ResetUI()
        {
            uiArea.gameObject.SetActive(false);
            cardDisplayer.gameObject.SetActive(false);
            cardSelector.gameObject.SetActive(false);
            drawnCardDisplayer.gameObject.SetActive(false);
        }

        //TODO: Handle player used card effect
        private void OnCardPlayed(CardPlayedEventPayload obj)
        {
            //Call this function to send command to server and actually play the card
            
            // Handle UI and effect for card
            switch (obj.PlayedCard.Data.cardCode)
            {
                case "Defuse":

               // case "ExplodingKitten":

                case "Shuffle":

                case "Skip":

                case "SeeTheFuture":
               
                case "Attack":               

                case "Nope":
                    GameManager.Instance.PlayCard(
                        cardCodes: new List<string> { obj.PlayedCard.Data.cardCode });
                    break;

                case "Favor":
                    string targetUserId = OpenTargetUserSelector(GameManager.Instance.GetAlivePlayers());
                    GameManager.Instance.PlayCard(
                        targetUserId: targetUserId,
                        cardCodes: new List<string> { obj.PlayedCard.Data.cardCode });
                    break;
                default:
                    Debug.LogWarning($"Unhandled card: {obj.PlayedCard.Data.cardCode}");
                    break;
            }
        }
        public void HandleProfileClicked(string userID)
        {
            Debug.Log($"Profile clicked: {userID}");
            OnOpponentProfileSelected?.Invoke(userID);
        }
        public string OpenTargetUserSelector(List<WsPlayerPublicStateDto> targetUsers)
        {
            //
            //TODO: Open a UI to let player select target user, then return the selected user's ID
            return null;
        }
        public void OpenBombReinsertWindow()
        {
            int drawPileCount = GameManager.Instance.GetDrawPileCount();
            // TODO: Mở slider để chọn vị trí đặt bomb (từ 0 đến drawPileCount)
            int selectedPosition = 0; // Lấy giá trị từ slider

            //Sau khi chọn vị trí, gọi API để đặt bomb vào vị trí đã chọn
            GameManager.Instance.ChooseBombInsertPosition(selectedPosition); 
        }
        
        //Draw Card
        public void DisplayDrawnCard()
        {
            uiArea.gameObject.SetActive(true);
            drawnCardDisplayer.gameObject.SetActive(true);
        }

        //Card Displayer
        public void DisplayCards(List<string> cardCodes)
        {
            uiArea.gameObject.SetActive(true);
            cardDisplayer.gameObject.SetActive(true);

            cardDisplayer.Clear();
            foreach (var cardCode in cardCodes)
            {
                CardManager.Instance.CreateDisplayCard(cardCode, cardDisplayer.transform);
            }
        }

        public void CloseCardDisplayer()
        {
            ResetUI();
        }

        //Opponent UI
        public void InitOpponentUI(List<WsPlayerPublicStateDto> players)
        {
            if (players == null || players.Count == 0) 
                return;

            string myId = GameManager.Instance.Player.ID;
            int myIndex = players.FindIndex(p => p.userId == myId);
            if (myIndex == -1) return;

            int count = players.Count;

            var rects = new List<RectTransform>();

            for (int offset = 1; offset < count; offset++)
            {
                int index = (myIndex + offset) % count;
                var opponent = players[index];

                var ui = Instantiate(opponentProfilePrefab, playingArea.transform);
                //TODO: pass in user profile
                ui.Init(opponent, null);
                ui.OnProfileClickedEvent += HandleProfileClicked;

                rects.Add(ui.GetComponent<RectTransform>());
            }

            LayoutOpponent(rects);
        }

        private void LayoutOpponent(List<RectTransform> items)
        {
            if (items == null || items.Count == 0) return;

            int count = items.Count;

            float effectiveStart = autoCenter
                ? -totalAngle * 0.5f
                : startAngle;

            float step = count > 1 ? totalAngle / (count - 1) : 0f;

            // Convert center
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                playingArea,
                RectTransformUtility.WorldToScreenPoint(null, fanCenter.position),
                null,
                out var centerLocalPos
            );

            for (int i = 0; i < count; i++)
            {
                float angle = effectiveStart + step * i;
                float rad = angle * Mathf.Deg2Rad;

                // Key difference from circle:
                float x = Mathf.Sin(rad) * radiusX;
                float y = Mathf.Cos(rad) * radiusY;

                var rect = items[i];
                rect.SetParent(playingArea, false);
                rect.anchoredPosition = centerLocalPos + new Vector2(x, y);
            }
        }

        public void PlayOpponentCard(string userID, string cardCode)
        {
            if (!_opponentsUI.TryGetValue(userID, out var opponent))
                return;

            if (opponent == null)
                return;

            opponent.PlayCard(cardCode);
        }
    }
}