# WebSocket API - Shared Contract (Server <-> Client)

## Connection

- URL: `wss://<host>/ws?access_token=<jwt_access_token>`
- Nếu token invalid/expired -> kết nối bị từ chối.

## Ownership (API WS vs Game WS)

- **API WS**: social + room lifecycle ngoài trận (`invite/request/leave/kick/ready/start-request`).
- **Game WS**: gameplay in-match (`play/draw/nope/defuse/...`).
- Client có thể giữ đồng thời cả 2 kết nối, nhưng action gửi đúng owner.

---

## Common server push format

Mọi message server gửi cho client đều có format:

```json
{
  "event": "FriendStatusChanged",
  "data": {},
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## Client -> Server methods

Vì đây là SignalR, client invoke method theo tên + args.  
Trong docs này dùng JSON mô tả logical payload để frontend dễ map.

## 1) Heartbeat

**Client request**

```json
{
  "method": "Heartbeat",
  "data": {}
}
```

**Server response trực tiếp:** none  
**Server push có thể phát sinh sau đó:** `FriendStatusChanged`

---

## 2) InviteToRoom

**Client request**

```json
{
  "method": "InviteToRoom",
  "data": {
    "roomCode": "ABC123",
    "friendUserId": "guid"
  }
}
```

**Success push**

```json
{
  "event": "RoomInvitationReceived",
  "data": {
    "invitationId": "string",
    "roomCode": "ABC123",
    "inviterId": "guid",
    "inviterUsername": "alice",
    "inviterAvatarUrl": "https://...",
    "isPublic": true,
    "currentPlayers": 3,
    "maxPlayers": 6,
    "expiresAt": "2026-04-11T00:05:00Z"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

**Error push**

```json
{
  "event": "Error",
  "data": {
    "code": "NOT_IN_ROOM",
    "message": "You are not in this room"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## 3) RespondInvitation

**Client request**

```json
{
  "method": "RespondInvitation",
  "data": {
    "invitationId": "string",
    "accepted": true
  }
}
```

**Success push**

```json
{
  "event": "RoomInvitationResponse",
  "data": {
    "invitationId": "string",
    "accepted": true,
    "inviteeId": "guid",
    "inviteeUsername": "bob"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

**Error push**

```json
{
  "event": "Error",
  "data": {
    "code": "INVITATION_NOT_FOUND",
    "message": "Invitation not found or expired"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## 4) RequestJoinRoom

**Client request**

```json
{
  "method": "RequestJoinRoom",
  "data": {
    "roomCode": "ABC123"
  }
}
```

**Success push**

```json
{
  "event": "JoinRoomRequested",
  "data": {
    "requestId": "string",
    "roomCode": "ABC123",
    "requesterId": "guid",
    "requesterUsername": "charlie",
    "requesterAvatarUrl": "https://...",
    "expiresAt": "2026-04-11T00:05:00Z"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

**Error push**

```json
{
  "event": "Error",
  "data": {
    "code": "ROOM_PUBLIC",
    "message": "Room is public, join via REST"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## 5) RespondJoinRequest

**Client request**

```json
{
  "method": "RespondJoinRequest",
  "data": {
    "requestId": "string",
    "accepted": true
  }
}
```

**Success push**

```json
{
  "event": "JoinRoomResponse",
  "data": {
    "requestId": "string",
    "roomCode": "ABC123",
    "accepted": true,
    "responderId": "guid",
    "responderUsername": "alice"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

**Error push**

```json
{
  "event": "Error",
  "data": {
    "code": "REQUEST_NOT_FOUND",
    "message": "Expired"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## 6) LeaveRoom (API WS)

**Client request**

```json
{
  "method": "LeaveRoom",
  "data": {
    "roomCode": "ABC123"
  }
}
```

**Success push**

```json
{
  "event": "RoomMemberLeft",
  "data": {
    "roomCode": "ABC123",
    "userId": "guid"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## 7) KickRoomMember (API WS)

**Client request**

```json
{
  "method": "KickRoomMember",
  "data": {
    "roomCode": "ABC123",
    "targetUserId": "guid"
  }
}
```

**Success push**

```json
{
  "event": "RoomMemberKicked",
  "data": {
    "roomCode": "ABC123",
    "targetUserId": "guid",
    "kickedByUserId": "guid"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## 8) SetReadyStatus (API WS)

**Client request**

```json
{
  "method": "SetReadyStatus",
  "data": {
    "roomCode": "ABC123",
    "isReady": true
  }
}
```

**Success push**

```json
{
  "event": "RoomReadyStatusChanged",
  "data": {
    "roomCode": "ABC123",
    "userId": "guid",
    "isReady": true
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## 9) StartRoomMatch (API WS)

**Client request**

```json
{
  "method": "StartRoomMatch",
  "data": {
    "roomCode": "ABC123"
  }
}
```

**Success push**

```json
{
  "event": "RoomMatchStarting",
  "data": {
    "roomCode": "ABC123",
    "startedByUserId": "guid",
    "connection": {
      "wsUrl": "ws://localhost:5217/ws",
      "wsAccessToken": "jwt-game-ticket"
    }
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

Server push riêng cho từng user, mỗi user nhận ticket của chính nó trong event này và dùng luôn để connect Game WS.
Client không cần gọi thêm `GET /rooms/{code}` chỉ để lấy game ticket; REST room detail chỉ là fallback/resync.

---

## 10) Host handover on LeaveRoom

- Không có method `DissolveRoom` thủ công.
- Khi host gọi `LeaveRoom`:
  - nếu còn người trong phòng -> server tự chuyển host ngẫu nhiên và push `RoomHostChanged`;
  - nếu host là người cuối cùng -> room tự xóa và push `RoomDissolved` (nếu còn recipient online).
- Nếu user bị timeout reconnect grace (mặc định 120s), server cũng xử lý như `LeaveRoom` và push `RoomMemberLeft`.

**Host changed push**

```json
{
  "event": "RoomHostChanged",
  "data": {
    "roomCode": "ABC123",
    "previousHostUserId": "guid",
    "newHostUserId": "guid"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## Server -> Client events

## 1) FriendRequestReceived

```json
{
  "event": "FriendRequestReceived",
  "data": {
    "senderId": "guid",
    "senderUsername": "alice",
    "senderAvatarUrl": "https://...",
    "message": "User alice sent you a friend request."
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 2) FriendRequestAccepted

```json
{
  "event": "FriendRequestAccepted",
  "data": {
    "senderId": "guid",
    "senderUsername": "bob",
    "senderAvatarUrl": "",
    "message": "bob accepted your friend request."
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 3) FriendStatusChanged

```json
{
  "event": "FriendStatusChanged",
  "data": {
    "userId": "guid",
    "username": "alice",
    "avatarUrl": "https://...",
    "online": true,
    "lastSeen": null,
    "activity": {
      "type": "in_room",
      "room": {
        "code": "ABC123",
        "status": "waiting",
        "isPublic": true,
        "currentPlayers": 3,
        "maxPlayers": 6
      }
    }
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 4) RoomInvitationReceived

```json
{
  "event": "RoomInvitationReceived",
  "data": {
    "invitationId": "string",
    "roomCode": "ABC123",
    "inviterId": "guid",
    "inviterUsername": "alice",
    "inviterAvatarUrl": "https://...",
    "isPublic": true,
    "currentPlayers": 3,
    "maxPlayers": 6,
    "expiresAt": "2026-04-11T00:05:00Z"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 5) RoomInvitationResponse

```json
{
  "event": "RoomInvitationResponse",
  "data": {
    "invitationId": "string",
    "accepted": true,
    "inviteeId": "guid",
    "inviteeUsername": "bob"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 6) JoinRoomRequested

```json
{
  "event": "JoinRoomRequested",
  "data": {
    "requestId": "string",
    "roomCode": "ABC123",
    "requesterId": "guid",
    "requesterUsername": "charlie",
    "requesterAvatarUrl": "https://...",
    "expiresAt": "2026-04-11T00:05:00Z"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 7) JoinRoomResponse

```json
{
  "event": "JoinRoomResponse",
  "data": {
    "requestId": "string",
    "roomCode": "ABC123",
    "accepted": true,
    "responderId": "guid",
    "responderUsername": "alice"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 8) RoomMemberJoined

```json
{
  "event": "RoomMemberJoined",
  "data": {
    "roomCode": "ABC123",
    "userId": "guid",
    "nickname": "bob",
    "avatarUrl": "https://...",
    "role": "player",
    "isReady": false
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

Event này được broadcast cho toàn bộ members khi có người join room (kể cả join qua REST `/rooms/{code}/join`).

## 9) Error

```json
{
  "event": "Error",
  "data": {
    "code": "RATE_LIMITED",
    "message": "Please wait"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 10) RoomHostChanged

```json
{
  "event": "RoomHostChanged",
  "data": {
    "roomCode": "ABC123",
    "previousHostUserId": "guid",
    "newHostUserId": "guid"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 11) RoomDissolved

```json
{
  "event": "RoomDissolved",
  "data": {
    "roomCode": "ABC123",
    "dissolvedByUserId": "guid"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 12) MatchEnded (from Game WS)

```json
{
  "event": "MatchEnded",
  "data": {
    "roomCode": "ABC123",
    "matchId": "guid",
    "winnerId": "guid"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

## 13) ReturnedToRoom (from API WS)

```json
{
  "event": "ReturnedToRoom",
  "data": {
    "roomCode": "ABC123",
    "status": "waiting"
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## Notes cho client

- JSON dùng camelCase.
- Enum gửi dạng string.
- Các `code` hiện tại hay gặp: `UNAUTHORIZED`, `NOT_IN_ROOM`, `NOT_FRIENDS`, `ROOM_IS_FULL`, `RATE_LIMITED`, `INVITATION_NOT_FOUND`, `ROOM_PUBLIC`, `REQUEST_NOT_FOUND`, `FORBIDDEN`.
- `RoomMatchStarting` do API WS phát chỉ là tín hiệu handoff. `MatchStarted/MatchEnded` thuộc Game WS.
- Reconnect policy ở API room/lobby: client reconnect WS xong phải gọi lại `GET /api/v1/rooms/{code}` để resync snapshot; server không replay missed events.
- Broadcast chỉ tới các connection đang online tại thời điểm gửi; client offline sẽ không nhận replay và phải dựa vào REST resync.
