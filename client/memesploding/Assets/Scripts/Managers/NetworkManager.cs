using Events;
using Network.Websocket;
using System;
using System.Collections.Generic;

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

        public void SendPlayCardCommand(
            string cardCode,
            string targetUserId = null,
            int comboSize = 0,
            List<string> cardCodes = null,
            string requestedCardCode = null,
            string discardCardCode = null)
        {
            var cardData = new WsPlayCardData
            {
                cardCode = cardCode,
                targetUserId = targetUserId,
                comboSize = comboSize,
                cardCodes = cardCodes,
                requestedCardCode = requestedCardCode,
                discardCardCode = discardCardCode
            };

            WebsocketClient.SendCommandAsync(
                WsClientCommandType.PlayCard,
                cardData
            );
        }
        public void SendDrawCardCommand()
        {
            WebsocketClient.SendCommandAsync(WsClientCommandType.DrawCard, null);
        }

        public void SendDrawFromBottomCommand()
        {
            WebsocketClient.SendCommandAsync(WsClientCommandType.DrawFromBottom, null);
        }

        public void SendChooseBombInsertPositionCommand(int position)
        {
            WebsocketClient.SendCommandAsync(WsClientCommandType.ChooseBombInsertPosition, position);
        }
    }
}