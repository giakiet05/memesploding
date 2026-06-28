# ADR 0004: Theo dõi Trạng thái Online (Online Status Tracking)

## Trạng thái (Status)
Đã chấp thuận (Accepted)

## Bối cảnh (Context)
Cần theo dõi trạng thái online/offline/in_match của bạn bè để hiển thị trong danh sách bạn bè. Trạng thái phải cập nhật gần như real-time khi bạn bè login/logout/vào trận.

## Quyết định (Decision)
1. **Lưu trữ trạng thái (Storage):** Sử dụng **Redis** với TTL 5 phút để lưu `user:{user_id}:status`. Không cần Postgres table (trạng thái là ephemeral).
   ```
   Redis Key: user:{user_id}:status
   Values: "online" | "offline" | "in_match"
   TTL: 300 giây (5 phút, auto-expire)
   ```

2. **Cập nhật trạng thái:**
   * Khi user **connect WebSocket** → `SETEX user:{user_id}:status 300 "online"`
   * Khi user **disconnect** → `DEL user:{user_id}:status`
   * Khi user **vào match** → `SETEX user:{user_id}:status 300 "in_match"` (từ game server)

3. **Kiến trúc Hybrid (REST + WebSocket):**
   * **REST API - GET /users/me/friends:**
     - Query DB friendships
     - For each friend: Check Redis `user:{friend_id}:status`
     - Nếu key tồn tại → `is_online: true`, không tồn tại → `is_online: false`
     - Return snapshot tại thời điểm request
   
   * **WebSocket - Real-time channel:**
     - Client subscribe channel `friend_status`
     - Khi friend online/offline/in_match → Server broadcast event:
       ```json
       {
         "type": "friend_status_changed",
         "friend_id": "uuid",
         "status": "online|offline|in_match",
         "timestamp": "ISO8601"
       }
       ```
     - Client nhận event → Update UI instantly

4. **Error handling:**
   * Nếu Redis down → REST endpoint trả về `is_online: false` (fallback)
   * Nếu mất WebSocket → Client vẫn có snapshot từ REST, có thể poll if needed

## Hệ quả (Consequences)
* **Ưu điểm (Pros):**
    * Hiệu năng cao (Redis in-memory, không query DB).
    * Real-time updates via WebSocket pubsub.
    * REST API cung cấp snapshot ban đầu cho page load.
* **Nhược điểm (Cons):**
    * Yêu cầu WebSocket connection để có real-time (REST polling chậm).
    * Status mất nếu Redis crash (acceptable cho ephemeral data).

