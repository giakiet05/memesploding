using System;
using Events;
using Network.Websocket;

namespace Managers
{
    public class NetworkManager
    {
        private static NetworkManager _instance;
        public static NetworkManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new NetworkManager();

                return _instance;
            }
        }

        private GameWebsocketClient WebsocketClient { get; set; }

        public async void ConnectWs(string wsUrl, string wsAccessToken, string roomCode)
        {
            try
            {
                await WebsocketClient.ConnectAsync(wsUrl, wsAccessToken, roomCode);
            }
            catch (Exception e)
            {
                throw; // TODO handle exception
            }
        }

        public void SendPlayCardCommand(CardPlayedEventPayload payload)
        {
            WsPlayCardData cardData = new WsPlayCardData
            {
                //TODO Add additional information needed
                cardCode = payload.PlayedCard.Id,
                targetUserId = payload.TargetID

            };

            // Can add await and handle error
            WebsocketClient.SendCommandAsync(WsClientCommandType.PlayCard, cardData);
        }

        public void SendDrawCardCommand()
        {
            WebsocketClient.SendCommandAsync(WsClientCommandType.DrawCard, null);
        }

        public void SendDrawFromBottomCommand()
        {
            WebsocketClient.SendCommandAsync(WsClientCommandType.DrawFromBottom, null);
        }
    }
}