# WebSocket API - API Server

**Base URL:** `wss://api.memesploding.com/ws`

**⚠️ SCOPE:** API Server WebSocket CHỈ xử lý:
1. **Friend Presence** - Online/offline/activity status của friends
2. **Pre-Join Coordination** - Invitations và join requests TRƯỚC KHI join room

**Room lifecycle events** (player joined/left/ready/kicked, game events) → **Game Server WebSocket** xử lý

---

## 1. Connection & Authentication

### Establishing Connection
Client connect với JWT access token:

```
wss://api.memesploding.com/ws?access_token=<jwt_token>
```

**Authentication:**
- Server verify JWT token
- Nếu invalid/expired → reject connection với 401
- Nếu valid → connection established

### Heartbeat
Client PHẢI gửi heartbeat mỗi **60 giây** để duy trì presence:

**Client → Server:**
```json
{
  "action": "heartbeat"
}
```

**Consequence:** Nếu không heartbeat trong 10 phút → server coi như offline và broadcast tới friends.

### Reconnection Strategy
- Automatic reconnect: exponential backoff (1s, 2s, 4s, 8s, max 30s)
- Max retry: 10 lần
- Nếu thất bại 10 lần → user quay về login screen

---

## 2. Message Format

### Client → Server (Actions)
```json
{
  "action": "action_name",
  "data": {
    "field1": "value1",
    "field2": "value2"
  }
}
```

### Server → Client (Events)
```json
{
  "event": "event_name",
  "data": {
    "field1": "value1",
    "field2": "value2"
  },
  "timestamp": "2026-04-02T15:50:00Z"
}
```

---

## 3. Events (Server → Client)

### 3.1 Friend Status (Rich Presence)
**Khi nào:** Friend online/offline hoặc activity thay đổi (vào/rời phòng, bắt đầu/kết thúc trận)

**Server → Client (broadcast tới tất cả bạn bè):**
```json
{
  "event": "friend_status",
  "data": {
    "user_id": "uuid",
    "username": "PlayerOne",
    "avatar_url": "https://...",
    "online": true,
    "last_seen": "2026-04-02T14:30:00Z",
    "activity": {
      "type": "idle",
      "room": null
    }
  },
  "timestamp": "2026-04-02T15:50:00Z"
}
```

**Activity Types:**

**1. Idle (online nhưng không trong phòng):**
```json
{
  "activity": {
    "type": "idle"
  }
}
```
→ UI: "Online" + nút **[Invite to Room]** (nếu mình đang trong phòng)

**2. In Room - Public (đang trong phòng chờ public):**
```json
{
  "activity": {
    "type": "in_room",
    "room": {
      "code": "ABC123",
      "is_public": true,
      "current_players": 3,
      "max_players": 6,
      "status": "waiting"
    }
  }
}
```
→ UI: "Trong phòng ABC123 (3/6)" + nút **[Join Room]**

**3. In Room - Private (đang trong phòng chờ private):**
```json
{
  "activity": {
    "type": "in_room",
    "room": {
      "code": "XYZ789",
      "is_public": false,
      "current_players": 2,
      "max_players": 4,
      "status": "waiting"
    }
  }
}
```
→ UI: "Trong phòng riêng tư (2/4)" + nút **[Request Join]**

**4. In Match (đang chơi trận):**
```json
{
  "activity": {
    "type": "in_match",
    "room": {
      "code": "ABC123",
      "is_public": true,
      "current_players": 4,
      "max_players": 6,
      "status": "playing"
    }
  }
}
```
→ UI: "Đang chơi trận (4/6)" + **không có nút action**

**5. Offline:**
```json
{
  "online": false,
  "last_seen": "2026-04-02T14:30:00Z",
  "activity": {
    "type": "idle"
  }
}
```
→ UI: "Offline - 2 giờ trước"

→ UI: "Offline - 2 giờ trước"

---

### 3.2 Room Invitation Received
**Khi nào:** Member trong phòng mời mình vào phòng

**Server → Invited Player:**
```json
{
  "event": "room_invitation",
  "data": {
    "invitation_id": "uuid",
    "room_code": "ABC123",
    "inviter": {
      "user_id": "uuid",
      "username": "Alice",
      "avatar_url": "https://..."
    },
    "room_info": {
      "is_public": true,
      "current_players": 3,
      "max_players": 6,
      "card_sets": [
        {
          "id": "uuid",
          "name": "Original",
          "image_url": "https://..."
        }
      ]
    },
    "expires_at": "2026-04-02T15:55:00Z"
  },
  "timestamp": "2026-04-02T15:50:00Z"
}
```

**Expiration:** 5 phút. Sau đó invitation tự động xóa và server gửi event `invitation_expired`.

---

### 3.3 Room Join Request Received
**Khi nào:** Ai đó xin vào phòng private mà mình đang ở

**Server → Room Member:**
```json
{
  "event": "room_join_request",
  "data": {
    "request_id": "uuid",
    "room_code": "XYZ789",
    "requester": {
      "user_id": "uuid",
      "username": "Bob",
      "avatar_url": "https://..."
    },
    "expires_at": "2026-04-02T15:55:00Z"
  },
  "timestamp": "2026-04-02T15:50:00Z"
}
```

**Expiration:** 5 phút.

---

### 3.4 Invitation Response (Inviter nhận phản hồi)
**Khi nào:** Người được mời accept/decline invitation

**Server → Inviter:**
```json
{
  "event": "invitation_response",
  "data": {
    "invitation_id": "uuid",
    "accepted": true,
    "invitee": {
      "user_id": "uuid",
      "username": "Charlie"
    }
  },
  "timestamp": "2026-04-02T15:52:00Z"
}
```

**Note:** Nếu accept, invitee sẽ tự động call REST API `POST /rooms/:code/join` để vào phòng thật.

---

### 3.5 Join Request Response (Requester nhận phản hồi)
**Khi nào:** Member accept/decline join request

**Server → Requester:**
```json
{
  "event": "join_request_response",
  "data": {
    "request_id": "uuid",
    "accepted": true,
    "responder": {
      "user_id": "uuid",
      "username": "Alice"
    }
  },
  "timestamp": "2026-04-02T15:52:00Z"
}
```

---

### 3.6 Invitation/Request Expired
**Khi nào:** Invitation hoặc request hết hạn (5 phút không phản hồi)

**Server → Both Parties:**
```json
{
  "event": "invitation_expired",
  "data": {
    "invitation_id": "uuid",
    "reason": "No response within 5 minutes"
  },
  "timestamp": "2026-04-02T15:55:00Z"
}
```

---

## 4. Actions (Client → Server)

### 4.1 Heartbeat
**Purpose:** Duy trì presence status

**Client → Server:**
```json
{
  "action": "heartbeat"
}
```

**Frequency:** Mỗi 60 giây

---

### 4.2 Invite Friend to Room
**Prerequisites:** 
- User phải đang trong phòng
- Target phải là friend
- Room không full

**Client → Server:**
```json
{
  "action": "invite_to_room",
  "data": {
    "room_code": "ABC123",
    "friend_user_id": "uuid"
  }
}
```

**Rate Limit:** 1 invitation per friend per 5 giây

**Success Response:** Không có response. Friend sẽ nhận `room_invitation` event.

**Error Response:**
```json
{
  "event": "error",
  "data": {
    "code": "NOT_IN_ROOM",
    "message": "You are not in a room"
  },
  "timestamp": "2026-04-02T15:50:00Z"
}
```

**Error Codes:**
- `NOT_IN_ROOM`: User không trong phòng
- `NOT_FRIENDS`: Target không phải friend
- `ROOM_FULL`: Phòng đã đầy
- `RATE_LIMITED`: Gửi quá nhanh
- `ALREADY_INVITED`: Đã mời trước đó (invitation chưa expire)

---

### 4.3 Respond to Invitation
**Client → Server:**
```json
{
  "action": "respond_invitation",
  "data": {
    "invitation_id": "uuid",
    "accepted": true
  }
}
```

**If accepted:**
- Server gửi `invitation_response` tới inviter
- Client phải tự call REST `POST /rooms/:code/join` để vào phòng
- Sau khi join xong, connect tới Game Server WebSocket

**If declined:**
- Server gửi `invitation_response` tới inviter
- Invitation bị xóa

---

### 4.4 Request Join Room
**Prerequisites:**
- Room phải private
- User chưa trong phòng đó
- Room không full

**Client → Server:**
```json
{
  "action": "request_join_room",
  "data": {
    "room_code": "XYZ789"
  }
}
```

**Server behavior:**
- Broadcast `room_join_request` tới **TẤT CẢ members** trong phòng (không chỉ 1 người)
- Bất kỳ member nào cũng có thể accept

**Error Codes:**
- `ROOM_NOT_FOUND`: Room không tồn tại
- `ROOM_PUBLIC`: Room là public, không cần xin (dùng REST join trực tiếp)
- `ROOM_FULL`: Phòng đã đầy
- `ALREADY_IN_ROOM`: User đã trong phòng rồi
- `RATE_LIMITED`: Gửi quá nhanh

---

### 4.5 Respond to Join Request
**Prerequisites:** User phải là member của room được request

**Client → Server:**
```json
{
  "action": "respond_join_request",
  "data": {
    "request_id": "uuid",
    "accepted": true
  }
}
```

**If accepted:**
- Server gửi `join_request_response` tới requester
- Requester phải tự call REST `POST /rooms/:code/join`

**If declined:**
- Server gửi `join_request_response` tới requester
- Request bị xóa

**Error Codes:**
- `REQUEST_NOT_FOUND`: Request không tồn tại hoặc đã expire
- `NOT_AUTHORIZED`: User không phải member của room
- `ROOM_FULL`: Phòng đã đầy (race condition: room full sau khi request được tạo)

---

## 5. Error Handling

### General Error Format
```json
{
  "event": "error",
  "data": {
    "code": "ERROR_CODE",
    "message": "Human-readable error message",
    "retry_after": 5
  },
  "timestamp": "2026-04-02T15:50:00Z"
}
```

### Common Error Codes
- `INVALID_ACTION`: Action không hợp lệ
- `INVALID_DATA`: Data thiếu field hoặc sai format
- `RATE_LIMITED`: Gửi requests quá nhanh
- `UNAUTHORIZED`: Token invalid hoặc expired
- `NOT_FOUND`: Resource không tồn tại
- `FORBIDDEN`: Không có quyền thực hiện action

### Connection Rejected
```
Status: 401 Unauthorized
Body: JWT token invalid or expired
```

---

## 6. Example Flows

### Flow 1: Friend Goes Online
```
1. User A connects WebSocket
   ↓
2. Server sets presence:{A} = online + activity:idle
   ↓
3. Server queries friends của A từ DB
   ↓
4. Server broadcasts friend_status event tới WebSocket connections của tất cả friends
   ↓
5. Friends' UI updates: "User A is now online"
```

---

### Flow 2: Friend Joins Room
```
1. User A calls REST POST /rooms/ABC123/join
   ↓
2. API Server updates presence:{A}.activity = in_room
   ↓
3. API Server broadcasts friend_status (with room info) tới friends
   ↓
4. Friend B sees: "User A is in room ABC123 (3/6)" + [Join Room] button
   ↓
5. Friend B clicks [Join Room] → calls REST POST /rooms/ABC123/join (không qua WebSocket)
```

---

### Flow 3: Member Invites Friend
```
1. User A (in room ABC123) sends: invite_to_room action via WebSocket
   ↓
2. Server validates: A in room? B is friend? Room not full?
   ↓
3. Server creates invitation (Redis, TTL 5min)
   ↓
4. Server sends room_invitation event tới User B's WebSocket
   ↓
5. User B sees popup, clicks Accept
   ↓
6. User B sends: respond_invitation (accepted: true)
   ↓
7. Server sends invitation_response tới User A
   ↓
8. User B calls REST POST /rooms/ABC123/join
   ↓
9. Join successful → User B connects to Game Server WebSocket
```

---

### Flow 4: Request Join Private Room
```
1. User B sees friend User A (via friend_status) is in private room XYZ789
   ↓
2. User B clicks [Request Join]
   ↓
3. User B sends: request_join_room action
   ↓
4. Server creates request (Redis, TTL 5min)
   ↓
5. Server broadcasts room_join_request tới TẤT CẢ members trong XYZ789
   ↓
6. User A (member) sees popup, clicks Accept
   ↓
7. User A sends: respond_join_request (accepted: true)
   ↓
8. Server sends join_request_response tới User B
   ↓
9. User B calls REST POST /rooms/XYZ789/join
   ↓
10. Join successful → User B connects to Game Server WebSocket
```

---

## 7. Redis Pub/Sub Integration

API Server subscribe Redis channel `room:updates` để nhận updates từ Game Server:

**Game Server publishes:**
```json
{
  "type": "room_status_changed",
  "room_code": "ABC123",
  "status": "playing",
  "current_players": 4
}
```

**API Server subscribes và:**
1. Update `room:{code}:info` cache
2. For each player in room: update `presence:{userId}.activity`
3. Broadcast updated `friend_status` tới friends của các players

---

## 8. Implementation Notes

### Presence Cache (Redis)
```
Key: presence:{userId}
Value: JSON {
  online: boolean,
  activity: { type, room? },
  updated_at: ISO8601
}
TTL: 10 minutes (refreshed by heartbeat)
```

### Invitation/Request Tracking (Redis)
```
Key: invitation:{invitationId}
Value: JSON {
  room_code: string,
  inviter_id: uuid,
  invitee_id: uuid,
  created_at: ISO8601
}
TTL: 5 minutes

Key: join_request:{requestId}
Value: JSON {
  room_code: string,
  requester_id: uuid,
  created_at: ISO8601
}
TTL: 5 minutes
```

### SignalR Groups
- `user:{userId}`: Individual user (for friend_status broadcasts)
