# WebSocket API - Shared Contract (Server <-> Client)

## Connection

- URL: `wss://<host>/api/v1/ws?access_token=<jwt_access_token>`
- Nếu token invalid/expired -> kết nối bị từ chối.

## Ownership (API WS vs Game WS)

- **API WS**: social + room lifecycle ngoài trận (`invite/request/kick/ready/start-request`). `leave` dùng REST `POST /rooms/{code}/leave`.
- **Game WS**: gameplay in-match (`play/draw/nope/defuse/...`).
- Client có thể giữ đồng thời cả 2 kết nối, nhưng action gửi đúng owner.

---

## 1. Cơ chế SignalR (Kỹ thuật)

Nếu client **không dùng** thư viện SignalR chuẩn (ví dụ dùng WebSocket thô), bạn cần tuân thủ các quy tắc sau:

1. **Handshake**: Ngay sau khi kết nối thành công, client **BẮT BUỘC** gửi message mở đầu:
   ```json
   { "protocol": "json", "version": 1 }
   ```
   Kèm theo ký tự đặc biệt `\u001e` (Record Separator - mã hex `0x1E`) ở cuối.

2. **Ký tự kết thúc**: Mọi message (cả gửi và nhận) đều phải có ký tự `\u001e` ở cuối cùng. Nếu thiếu, server sẽ không xử lý message.

3. **Packaging (Giao tiếp)**: 
   - Server gửi về: `{"type":1,"target":"ReceiveMessage","arguments":[{"event":"...","data":{}}]}`
   - Client gửi lên: `{"type":1,"target":"MethodName","arguments":[arg1, arg2, ...]}`

---

## 2. Common server push format

Mọi message server gửi cho client đều được bọc trong hàm `ReceiveMessage` với format:

```json
{
  "event": "EventName", // Tên sự kiện (định dạng PascalCase)
  "data": {}, // Dữ liệu đi kèm (Payload)
  "timestamp": "2026-04-11T00:00:00Z" // Thời gian tạo sự kiện (ISO 8601)
}
```

---

## 3. Error response format

Khi một yêu cầu từ client bị lỗi hoặc có sự cố hệ thống, server sẽ bắn về sự kiện `Error` cho chính client đó:

```json
{
  "event": "Error",
  "data": {
    "code": "ERROR_CODE", // Mã lỗi quy chuẩn (vd: ROOM_IS_FULL, UNAUTHORIZED)
    "message": "Câu thông báo lỗi chi tiết" // Giải thích lỗi bằng ngôn ngữ dễ hiểu
  },
  "timestamp": "2026-04-11T00:00:00Z"
}
```

---

## 4. Client -> Server methods

Vì đây là SignalR, client invoke method theo tên + args.  
Trong docs này dùng JSON mô tả logical payload để frontend dễ map.

### 4.1) Heartbeat

Client gửi tín hiệu định kỳ để duy trì kết nối.

**Client request**
```json
{
  "method": "Heartbeat",
  "data": {}
}
```

**Các lỗi có thể gặp:**
- *(Không có lỗi cụ thể)*

---

### 4.2) InviteToRoom

Mời bạn bè vào phòng hiện tại.

**Client request**
```json
{
  "method": "InviteToRoom",
  "data": {
    "roomCode": "ABC123", // Mã phòng hiện tại
    "friendUserId": "guid" // ID của người bạn muốn mời
  }
}
```

**Success push (gửi cho người được mời)**
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
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_IN_ROOM`, `NOT_FRIENDS`, `ROOM_IS_FULL`

---

### 4.3) RespondInvitation

Phản hồi lời mời vào phòng.

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

**Success push (gửi cho người mời)**
```json
{
  "event": "RoomInvitationResponse",
  "data": {
    "invitationId": "string",
    "accepted": true,
    "inviteeId": "guid",
    "inviteeUsername": "bob"
  }
}
```

**Các lỗi có thể gặp:**
- `INVITATION_NOT_FOUND`

---

### 4.4) RequestJoinRoom

Xin vào một phòng (thường dùng khi phòng riêng tư).

**Client request**
```json
{
  "method": "RequestJoinRoom",
  "data": {
    "roomCode": "ABC123"
  }
}
```

**Success push (gửi cho chủ phòng)**
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
  }
}
```

**Các lỗi có thể gặp:**
- `ROOM_PUBLIC`, `ROOM_IS_FULL`, `NOT_FOUND`

---

### 4.5) RespondJoinRequest

Chủ phòng phản hồi yêu cầu xin vào phòng.

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

**Success push (gửi cho người xin vào)**
```json
{
  "event": "JoinRoomResponse",
  "data": {
    "requestId": "string",
    "roomCode": "ABC123",
    "accepted": true,
    "responderId": "guid",
    "responderUsername": "alice"
  }
}
```

**Các lỗi có thể gặp:**
- `NOT_ROOM_HOST`, `REQUEST_NOT_FOUND`, `ROOM_IS_FULL`

---

### 4.6) LeaveRoom (API WS, deprecated)

Rời phòng hiện tại. Client mới phải dùng REST `POST /api/v1/rooms/{code}/leave`. Method WebSocket này chỉ giữ để tương thích client cũ.

**Client request**
```json
{
  "method": "LeaveRoom",
  "data": {
    "roomCode": "ABC123"
  }
}
```

**Success push (gửi cho những người còn lại)**
```json
{
  "event": "RoomMemberLeft",
  "data": {
    "roomCode": "ABC123",
    "userId": "guid"
  }
}
```

**Các lỗi có thể gặp:**
- *(Không có mã lỗi cụ thể)*

---

### 4.7) KickRoomMember (API WS)

Chủ phòng đuổi thành viên khác khỏi phòng.

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

**Success push (gửi cho cả phòng)**
```json
{
  "event": "RoomMemberKicked",
  "data": {
    "roomCode": "ABC123",
    "targetUserId": "guid",
    "kickedByUserId": "guid"
  }
}
```

**Các lỗi có thể gặp:**
- `NOT_ROOM_HOST`, `NOT_IN_ROOM`

---

### 4.8) SetReadyStatus (API WS)

Đánh dấu bản thân đã sẵn sàng.

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

**Success push (gửi cho cả phòng)**
```json
{
  "event": "RoomReadyStatusChanged",
  "data": {
    "roomCode": "ABC123",
    "userId": "guid",
    "isReady": true
  }
}
```

**Các lỗi có thể gặp:**
- `NOT_IN_ROOM`

---

### 4.9) StartRoomMatch (API WS)

Chủ phòng yêu cầu bắt đầu trận đấu.

**Client request**
```json
{
  "method": "StartRoomMatch",
  "data": {
    "roomCode": "ABC123"
  }
}
```

**Success push (gửi cho từng user)**
```json
{
  "event": "RoomMatchStarting",
  "data": {
    "roomCode": "ABC123",
    "startedByUserId": "guid",
    "connection": {
      "wsUrl": "ws://localhost:5217/api/v1/ws",
      "wsAccessToken": "jwt-game-ticket"
    }
  }
}
```

**Các lỗi có thể gặp:**
- `NOT_ROOM_HOST`, `MATCH_ALREADY_STARTED`, `VALIDATION_FAILED`

---

## Server -> Client events

Dưới đây là các sự kiện do Server chủ động push về Client.

### 1) FriendRequestReceived
```json
{
  "event": "FriendRequestReceived",
  "data": {
    "senderId": "guid",
    "senderUsername": "alice",
    "senderAvatarUrl": "https://...",
    "message": "User alice sent you a friend request."
  }
}
```

### 2) FriendRequestAccepted
```json
{
  "event": "FriendRequestAccepted",
  "data": {
    "senderId": "guid",
    "senderUsername": "bob",
    "senderAvatarUrl": "",
    "message": "bob accepted your friend request."
  }
}
```

### 3) FriendStatusChanged
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
      "type": "in_room", // Enum: "idle", "in_room", "in_match"
      "room": {
        "code": "ABC123",
        "status": "waiting",
        "isPublic": true,
        "currentPlayers": 3,
        "maxPlayers": 6
      }
    }
  }
}
```

### 4) RoomMemberJoined
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
  }
}
```

### 5) RoomHostChanged
```json
{
  "event": "RoomHostChanged",
  "data": {
    "roomCode": "ABC123",
    "previousHostUserId": "guid",
    "newHostUserId": "guid"
  }
}
```

### 6) RoomDissolved
```json
{
  "event": "RoomDissolved",
  "data": {
    "roomCode": "ABC123",
    "dissolvedByUserId": "guid"
  }
}
```

### 7) MatchEnded (from API WS)
```json
{
  "event": "MatchEnded",
  "data": {
    "roomCode": "ABC123",
    "matchId": "guid",
    "winnerId": "guid"
  }
}
```

---

## Danh sách Error Codes quy chuẩn (WebSocket)

| Error Code | Mô tả |
| :--- | :--- |
| `UNAUTHORIZED` | Token không hợp lệ hoặc hết hạn. |
| `NOT_IN_ROOM` | Bạn không ở trong phòng này. |
| `ROOM_IS_FULL` | Phòng đã đủ số lượng người tối đa. |
| `NOT_FRIENDS` | Hai người chưa kết bạn. |
| `INVITATION_NOT_FOUND` | Lời mời không tồn tại hoặc đã hết hạn. |
| `REQUEST_NOT_FOUND` | Yêu cầu không tồn tại hoặc đã hết hạn. |
| `ROOM_PUBLIC` | Phòng công khai, hãy dùng REST API `/join`. |
| `NOT_ROOM_HOST` | Chỉ chủ phòng mới được thực hiện hành động này. |
| `MATCH_ALREADY_STARTED` | Trận đấu đã bắt đầu rồi. |
| `VALIDATION_FAILED` | Điều kiện chưa thoả mãn (vd: chưa đủ người, chưa ready). |
| `PLAYER_ALREADY_IN_ROOM` | Bạn đang ở trong một phòng khác. |
| `RATE_LIMITED` | Thao tác quá nhanh. |
| `NOT_FOUND` | Tài nguyên không tồn tại. |

---

## Notes cho client

- JSON dùng `camelCase`.
- Enum gửi dạng `string`.
- **Reconnect policy**: client reconnect WS xong phải gọi lại `GET /api/v1/rooms/{code}` để resync snapshot.
