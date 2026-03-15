# ADR 0005: Kiến trúc Tham gia Phòng (Room Join Architecture)

## Trạng thái (Status)
Đã chấp thuận (Accepted)

## Bối cảnh (Context)
Phòng chơi có 2 loại (public/private) với 4 cách tham gia khác nhau. Cần rõ ràng khi nào dùng REST API, khi nào dùng WebSocket để tránh confusion.

## Quyết định (Decision)
**4 cách vào phòng với 2 method:**

### Method 1: REST API (Direct Join)
Dùng cho trường hợp không cần approval:

1. **Browse public room list**
   - Client: `GET /rooms?page=1&page_size=50` (lấy danh sách public rooms)
   - Client: `POST /rooms/:code/join` (empty body `{}`)
   - Server: Check `is_public=true` → Allow join
   - Response: Room info + WebSocket connection

2. **Nhập mã phòng (code/link)**
   - Client: `POST /rooms/:code/join` (empty body `{}`)
   - Server: Check room exists → Allow join
   - Response: Room info + WebSocket connection

### Method 2: WebSocket (Request-based, with auto-add)
Dùng cho trường hợp cần xử lý từ server:

3. **Host mời Friend vào (Invite)**
   ```
   Host → WS: { "action": "send_invite", "friend_id": "uuid", "room_code": "ABC123" }
     ↓
   Server: Broadcast to Friend: { "type": "room_invitation", "from": "host_name", "room_code": "ABC123" }
     ↓
   Friend → WS: { "action": "invite_response", "room_code": "ABC123", "response": "accept" }
     ↓
   Server: Auto-add friend to room → Broadcast { "type": "player_joined", "user": {...} }
   ```
   ⚠️ **NO POST /rooms/:code/join needed**

4. **Player xin vào phòng (Request to Join)**
   ```
   Player → WS: { "action": "request_join", "room_code": "ABC123" }
     ↓
   Server: Broadcast to Host: { "type": "join_request", "from": "player_name", "room_code": "ABC123" }
     ↓
   Host → WS: { "action": "approve_request", "player_id": "uuid", "response": "accept" }
     ↓
   Server: Auto-add player to room → Broadcast { "type": "player_joined", "user": {...} }
   ```
   ⚠️ **NO POST /rooms/:code/join needed**

## Hệ quả (Consequences)
* **Ưu điểm (Pros):**
    * Rõ ràng: REST cho join trực tiếp, WebSocket cho request-based
    * Không cần gọi POST /rooms/:code/join thêm lần nữa trong WebSocket flows
    * Consistent behavior: Cả invite và request đều auto-add (không duyệt phức tạp)
    * Simple & clean architecture
* **Nhược điểm (Cons):**
    * Clients phải implement 2 paths khác nhau
    * WebSocket rate limiting cần implement (5s/invite, prevent spam)
