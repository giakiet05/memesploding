# WebSocket API - API Server

**Base URL:** `wss://api.memesploding.com/ws`

## 1. Connection & Authentication

### Establishing Connection
Client cần gửi access token khi kết nối:

```
wss://api.memesploding.com/ws?token=<access_token>
```

### Token Refresh

### Reconnection Strategy
- Automatic reconnect: exponential backoff (1s, 2s, 4s, 8s, 30s max)
- Max retry: 10 lần
- Nếu thất bại 10 lần → user quay về login screen

---

## 2. Message Format

### Client → Server
```json
{
  "type": "event_type",
  "payload": {
    "field1": "value1",
    "field2": "value2"
  }
}
```

### Server → Client
```json
{
  "type": "event_type",
  "payload": {
    "field1": "value1",
    "field2": "value2"
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

## 3. Events

### 3.1 Friend Status (Real-time)
**Khi nào:** Người dùng online/offline

**Server → Client (gửi tới tất cả bạn bè của user):**
```json
{
  "type": "friend_status",
  "payload": {
    "user_id": "uuid",
    "nickname": "string",
    "status": "online|offline",
    "last_seen": "timestamp (chỉ nếu offline)"
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

### 3.2 Room Invitation
**Khi nào:** Bất cứ member nào trong phòng mời player vào phòng (không cần REST API)

**Server → Invited Player:**
```json
{
  "type": "room_invitation",
  "payload": {
    "invitation_id": "uuid",
    "room_code": "ABC123",
    "inviter_id": "uuid",
    "inviter_nickname": "string",
    "inviter_avatar_url": "string",
    "room_settings": {
      "max_players": 6,
      "card_sets": [
        { "id": "uuid", "name": "Base Set" }
      ]
    },
    "expires_at": "timestamp (hết hạn sau 5 giây)"
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

**Rate Limit:** 1 lời mời per 5 giây per inviter (server enforces)

---

### 3.3 Room Join Request
**Khi nào:** Player xin vào phòng private (không cần REST API)

**Client → Server:**
```json
{
  "type": "room_join_request",
  "payload": {
    "room_code": "ABC123",
    "target_id": "uuid"
  }
}
```

**Server → Target Member (chỉ gửi tới member được chỉ định):**
```json
{
  "type": "room_join_request",
  "payload": {
    "request_id": "uuid",
    "room_code": "ABC123",
    "requester_id": "uuid",
    "requester_nickname": "string",
    "requester_avatar_url": "string",
    "expires_at": "timestamp (hết hạn sau 30 giây)"
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

**Rate Limit:** 1 yêu cầu per user per target member per 5 giây (server enforces)

---

### 3.4 Room Join Request Response
**Khi nào:** Bất cứ member nào accept/reject lời xin vào

**Client (Member) → Server:**
```json
{
  "type": "room_join_response",
  "payload": {
    "request_id": "uuid",
    "action": "accept|reject"
  }
}
```

**If Accept - Server → Requester:**
```json
{
  "type": "room_join_response",
  "payload": {
    "action": "accept",
    "room_code": "ABC123",
    "room_data": {
      "code": "ABC123",
      "host_id": "uuid",
      "status": "waiting",
      "is_public": false,
      "settings": {
        "max_players": 6,
        "turn_timer": 15
      },
      "card_sets": [
        { "id": "uuid", "name": "Base Set" }
      ],
      "current_participants": [
        { "user_id": "uuid", "nickname": "string", "avatar_url": "string", "role": "player", "is_ready": false }
      ]
    },
    "connection": {
      "ws_url": "wss://game.memesploding.com/ws",
      "ws_access_token": "string"
    }
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

**If Reject - Server → Requester:**
```json
{
  "type": "room_join_response",
  "payload": {
    "action": "reject",
    "reason": "Member declined your request"
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

### 3.5 Room Invitation Response
**Khi nào:** Player accept/reject lời mời vào phòng

**Client → Server:**
```json
{
  "type": "room_invitation_response",
  "payload": {
    "invitation_id": "uuid",
    "action": "accept|reject"
  }
}
```

**If Accept - Server → Player:**
```json
{
  "type": "room_invitation_response",
  "payload": {
    "action": "accept",
    "room_data": {
      "code": "ABC123",
      "host_id": "uuid",
      "status": "waiting",
      "is_public": false,
      "settings": {
        "max_players": 6,
        "turn_timer": 15
      },
      "card_sets": [
        { "id": "uuid", "name": "Base Set" }
      ],
      "current_participants": [
        { "user_id": "uuid", "nickname": "string", "avatar_url": "string", "role": "player", "is_ready": false }
      ]
    },
    "connection": {
      "ws_url": "wss://game.memesploding.com/ws",
      "ws_access_token": "string"
    }
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

**If Reject - Server → Inviter:**
```json
{
  "type": "room_invitation_response",
  "payload": {
    "action": "reject",
    "player_id": "uuid",
    "player_nickname": "string"
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

### 3.6 Player Joined
**Khi nào:** Player vào phòng (qua POST /rooms/:code/join hoặc accept room invitation)

**Server → All Room Members:**
```json
{
  "type": "player_joined",
  "payload": {
    "room_code": "ABC123",
    "user_id": "uuid",
    "nickname": "string",
    "avatar_url": "string",
    "total_participants": 4
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

### 3.7 Player Left
**Khi nào:** Player rời phòng (qua POST /rooms/:code/leave)

**Server → All Room Members:**
```json
{
  "type": "player_left",
  "payload": {
    "room_code": "ABC123",
    "user_id": "uuid",
    "nickname": "string",
    "total_participants": 2
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

### 3.8 Player Kicked
**Khi nào:** Host đuổi player khỏi phòng (qua DELETE /rooms/:code/participants/:user_id)

**Server → Kicked Player:**
```json
{
  "type": "player_kicked",
  "payload": {
    "room_code": "ABC123",
    "reason": "Host kicked you out"
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

**Server → Other Room Members:**
```json
{
  "type": "player_kicked",
  "payload": {
    "room_code": "ABC123",
    "user_id": "uuid",
    "nickname": "string",
    "total_participants": 3
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

### 3.9 Participant Ready Changed
**Khi nào:** Player bấm ready/not ready (qua PATCH /rooms/:code/participants/me)

**Server → All Room Members:**
```json
{
  "type": "participant_ready_changed",
  "payload": {
    "room_code": "ABC123",
    "user_id": "uuid",
    "nickname": "string",
    "is_ready": true
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

### 3.10 Room State Updated
**Khi nào:** Host cập nhật cấu hình phòng (qua PATCH /rooms/:code)

**Server → All Room Members:**
```json
{
  "type": "room_state_updated",
  "payload": {
    "room_code": "ABC123",
    "changes": {
      "max_players": 5,
      "is_public": false,
      "card_sets": [
        { "id": "uuid", "name": "Base Set" }
      ]
    }
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

### 3.11 Room Dissolved
**Khi nào:** Host giải tán phòng (qua DELETE /rooms/:code)

**Server → All Room Members:**
```json
{
  "type": "room_dissolved",
  "payload": {
    "room_code": "ABC123",
    "reason": "Host dissolved the room"
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

### Invalid Message Format
```json
{
  "type": "error",
  "payload": {
    "code": "INVALID_MESSAGE",
    "message": "Message phải chứa 'type' và 'payload' fields"
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

### Unauthorized Connection
```
WebSocket handshake rejected with 401 Unauthorized
(Server đóng connection ngay lập tức)
```

### Rate Limit Exceeded
```json
{
  "type": "error",
  "payload": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Quá nhiều lời mời. Thử lại sau 5 giây.",
    "retry_after": 5
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

### Event Timeout
```json
{
  "type": "event_expired",
  "payload": {
    "event_id": "uuid",
    "event_type": "room_invitation|room_join_request",
    "reason": "Hết hạn đợi phản hồi"
  },
  "timestamp": "2026-03-15T03:40:00Z"
}
```

---

## 5. Connection Lifecycle

### 1. User Login
```
POST /auth/login → access_token + refresh_token
```

### 2. WebSocket Connect
```
wss://api.memesploding.com/ws?token=<access_token>
Connection established → Client sẵn sàng nhận events
```

### 3. Receive Events
```
Server gửi: friend_status, room_invitation, room_join_request, etc
```

### 4. Send Actions
```
Client gửi: room_invitation_response, room_join_response, etc
```

### 5. Disconnect
```
User logout hoặc đóng app → Client đóng WebSocket
(Server tự động cleanup pending invitations/requests)
```

---

## 6. Example Flows

### Flow 1: Member Mời Player Vào Phòng
```
1. Member gửi request mời (từ trong phòng, details TBD)
   ↓
2. Server gửi room_invitation event via WebSocket chỉ tới invited player
   ↓
3. Invited player nhận room_invitation, hiển thị popup
   ↓
4. Player gửi room_invitation_response (accept/reject) via WebSocket
   ↓
5. If accept:
   - Server gửi room_data + connection info tới player
   - Server tự động thêm player vào phòng (không cần REST API)
   ↓
6. Player kết nối tới Game Server dùng ws_url + ws_access_token
```

### Flow 2: Player Xin Vào Phòng Private
```
1. Player xem danh sách members trong phòng (từ REST API GET /rooms/:code)
   ↓
2. Player chọn 1 member cụ thể để gửi request
   ↓
3. Player gửi room_join_request với target_id via WebSocket
   ↓
4. Server gửi room_join_request chỉ tới target member (những member khác không thấy)
   ↓
5. Target member nhận notification, có thể accept/reject
   ↓
6. Nếu accept:
   - Server gửi room_data + connection info tới requester
   - Server tự động thêm requester vào phòng (không cần REST API)
   ↓
7. Requester kết nối tới Game Server dùng ws_url + ws_access_token
```

### Flow 3: Friend Status Update
```
1. User A goes online
   ↓
2. Server updates Redis: user:A:status = "online"
   ↓
3. Server gửi friend_status event via WebSocket tới tất cả bạn bè của User A
   ↓
4. Bạn bè nhận update, UI tự refresh
```

---

## 7. Notes

- Tất cả timestamps sử dụng ISO 8601 format
- `expires_at` cho invitations/requests là 5-30 giây (client nên show countdown)
- Nếu WebSocket disconnect lúc đang có pending invitation/request → expiration tự động clean up
- Rate limiting là server-side enforcement (reject duplicate events)
- Reconnect attempt phải dùng token mới nếu access token cũ hết hạn
- Room invitation details (inviter có quyền mời hay không) sẽ được define sau
