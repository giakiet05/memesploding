using System;
using System.Collections.Generic;

namespace Network.Websocket
{
    [Serializable]
    public class WsClientCommand
    {
        public string @event;
        public object data;
    }

    [Serializable]
    public class WsPlayCardData
    {
        public string cardCode;
        public string targetUserId;
        public int comboSize;
        public List<string> cardCodes;
        public string requestedCardCode;
        public string discardCardCode;
    }

    [Serializable]
    public class WsChooseBombInsertPositionData
    {
        public int position;
    }

    [Serializable]
    public class WsChooseFavorCardData
    {
        public string cardCode;
    }

    [Serializable]
    public class WsEmptyCommandData
    {
    }
}
