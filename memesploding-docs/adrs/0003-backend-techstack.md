# ADR 0003: Lựa chọn Tech Stack cho Backend (Backend Tech Stack)

## Trạng thái (Status)
Đã chấp thuận (Accepted)

## Bối cảnh (Context)
Cần xác định các công cụ và thư viện cụ thể để xây dựng hệ thống Backend (API & Game Server) đảm bảo tính nhất quán, hiệu năng và dễ bảo trì.

## Quyết định (Decision)
1. **Ngôn ngữ lập trình (Programming Language):** Sử dụng **Go (Golang)** cho toàn bộ hệ thống Backend. Tận dụng Goroutines và Channels để triển khai Actor Model cho máy chủ Game.
2. **Cơ sở dữ liệu chính (Primary Database):** Sử dụng **PostgreSQL** để lưu trữ các dữ liệu có tính bền vững (Persistent Data) như: Thông tin người dùng, Bạn bè, Lịch sử trận đấu, Tiền tệ.
3. **Cơ sở dữ liệu trung gian (Message Broker & Cache):** Sử dụng **Redis** để:
    * Triển khai Pub/Sub cho giao tiếp liên dịch vụ (Inter-service communication).
    * Lưu trữ tạm thời trạng thái các phòng chơi (Transient Room States) để truy cập nhanh.
4. **API Framework:** Sử dụng **Echo** để xây dựng RESTful API Server nhờ vào thiết kế Context mạnh mẽ và khả năng xử lý lỗi tập trung, phù hợp với Clean Architecture.
5. **Real-time Communication:** Sử dụng thư viện **Gorilla WebSocket** cho kết nối giữa Client và Game Server.
6. **Truy vấn dữ liệu (Database Layer):** Sử dụng **sqlc** để sinh mã nguồn Go từ SQL thuần, đảm bảo tính Type-safe và hiệu năng tối ưu.
7. **Quản lý Migration:** Sử dụng **golang-migrate** để quản lý các thay đổi cấu trúc bảng trong PostgreSQL.

## Hệ quả (Consequences)
* **Ưu điểm (Pros):**
    * Hiệu năng cực cao, tiêu tốn ít tài nguyên nhờ vào đặc tính của Go.
    * Actor Model được hỗ trợ tự nhiên bởi ngôn ngữ (Goroutines/Channels).
    * `sqlc` giúp giữ mã nguồn sạch, dễ kiểm soát SQL và hiệu năng cao hơn các ORM truyền thống.
* **Nhược điểm (Cons):**
    * Go yêu cầu lập trình viên phải tự xử lý nhiều logic hơn so với các framework "mì ăn liền" của Node.js hay Python.
    * Đòi hỏi sự hiểu biết sâu về concurrency (đồng thời) để tránh các lỗi rò rỉ bộ nhớ (goroutine leaks).
