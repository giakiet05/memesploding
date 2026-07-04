# ADR 0002: Kiến trúc hệ thống và Máy chủ Game (System & Game Server Architecture)

## Trạng thái (Status)
Đã chấp thuận (Accepted)

## Bối cảnh (Context)
Cần một kiến trúc đảm bảo tính real-time, ổn định và dễ dàng mở rộng cho hệ thống game online Memesploding.

## Quyết định (Decision)
1. **Kiến trúc phân tán (Distributed Architecture):** Tách biệt API Server (REST) và Game Server (WebSocket).
2. **Giao tiếp giữa các Service (Inter-svicere Communication):** Sử dụng **Redis Pub/Sub** để trao đổi các sự kiện (Events) giữa API và Game Server.
3. **Kiến trúc API Server:** Sử dụng **Clean Architecture (Hexagonal)** để tách biệt logic nghiệp vụ khỏi Database và Framework.
4. **Kiến trúc Game Server:** Sử dụng **Actor Model** cho việc quản lý các phòng chơi (Rooms).
    * Mỗi Phòng chơi (Room) là một **Actor** độc lập.
    * Mọi yêu cầu hành động từ người chơi được đưa vào **Mailbox (Queue)** của Room và xử lý tuần tự (Sequential) để tránh tranh chấp dữ liệu (Race Condition).
5. **Cơ chế xử lý logic game:** Sử dụng **Event-Driven nội bộ** bên trong mỗi Actor để kích hoạt hiệu ứng của các lá bài.

## Hệ quả (Consequences)
* **Ưu điểm (Pros):**
    * Loại bỏ hoàn toàn lỗi tranh chấp dữ liệu (Race Condition) mà không cần dùng Lock phức tạp.
    * API Server có thể mở rộng (Scale) độc lập với Game Server.
    * Dễ dàng Unit Test các logic bài riêng biệt.
* **Nhược điểm (Cons):**
    * Tăng độ phức tạp khi cần đồng bộ trạng thái giữa các Service.
    * Cần quản lý vòng đời (Lifecycle) của các Actor (Room) khi người chơi thoát hoặc phòng trống.
