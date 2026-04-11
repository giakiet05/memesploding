# WebSocket API - Shared Contract (Server <-> Client)

## Connection

- URL: `wss://<host>/ws?access_token=<jwt_access_token>`
- Nếu token invalid/expired -> kết nối bị từ chối.

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

## 8) Error

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

---

## Notes cho client

- JSON dùng camelCase.
- Enum gửi dạng string.
- Các `code` hiện tại hay gặp: `UNAUTHORIZED`, `NOT_IN_ROOM`, `NOT_FRIENDS`, `ROOM_IS_FULL`, `RATE_LIMITED`, `INVITATION_NOT_FOUND`, `ROOM_PUBLIC`, `REQUEST_NOT_FOUND`, `FORBIDDEN`.
