# Yêu cầu phi chức năng (Non-Functional Requirements - NFR) - Dự án Memesploding

Tài liệu này quy định các tiêu chuẩn kỹ thuật, hiệu năng và bảo mật cho hệ thống trò chơi Memesploding.

## 1. Hiệu năng và Đồng bộ (Performance & Synchronization)

* **NFR 1.1: Độ trễ phản hồi (Latency):** Thời gian phản hồi từ máy chủ cho các hành động của người chơi (đánh bài, rút bài) phải đạt dưới 200ms trong điều kiện mạng ổn định để đảm bảo trải nghiệm real-time mượt mà.
* **NFR 1.2: Đồng bộ hóa thời gian (Time Sync):** Hệ thống phải có cơ chế đồng bộ đồng hồ giữa máy chủ và các máy trạm (Client) với sai số không quá 50ms. Điều này đảm bảo tính công bằng tuyệt đối cho các cửa sổ thời gian phản ứng (như lá Nope hoặc trạng thái Gỡ bom).
* **NFR 1.3: Khả năng chịu tải (Scalability):** Cấu trúc máy chủ phải hỗ trợ tối thiểu 1.000 người chơi hoạt động đồng thời (Concurrent Users - CCU) mà không gây hiện tượng giật lag hoặc gián đoạn dịch vụ.

## 2. Bảo mật và Công bằng (Security & Fairness)

* **NFR 2.1: Quyền quyết định tại Máy chủ (Server-Side Authoritative):** Toàn bộ trạng thái trận đấu, logic xử lý bài và thứ tự các lá bài trong chồng bài rút phải được quản lý hoàn toàn tại máy chủ. Máy trạm chỉ đóng vai trò hiển thị giao diện và gửi yêu cầu hành động.
* **NFR 2.2: Bảo mật dữ liệu bài (Data Privacy):** Thông tin về các lá bài trong chồng bài rút và bài trên tay của người chơi khác tuyệt đối không được gửi về máy trạm của người chơi hiện tại (trừ khi có các hiệu ứng xem bài cụ thể).
* **NFR 2.3: Kiểm chứng hành động (Action Validation):** Mọi hành động từ máy trạm gửi lên phải được máy chủ kiểm tra tính hợp lệ về mặt logic (ví dụ: kiểm tra quyền sở hữu lá bài, trạng thái lượt đi, tổ hợp bài hợp lệ) trước khi thực thi và cập nhật trạng thái game.

## 3. Tối ưu hóa và Tương thích (Optimization & Compatibility)

* **NFR 3.1: Dung lượng tải ban đầu (Initial Load):** Phiên bản Web (WebGL) cần được tối ưu hóa tài nguyên để dung lượng tải về lần đầu không vượt quá 30MB, giúp người chơi nhanh chóng tham gia trận đấu qua các liên kết chia sẻ.
* **NFR 3.2: Khả năng tương thích đa nền tảng:** Hệ thống máy chủ phải hỗ trợ kết nối đồng nhất từ nhiều loại máy trạm khác nhau (Trình duyệt Web, Android, iOS) trong cùng một phòng chơi mà không gặp trở ngại về định dạng dữ liệu.

## 4. Khả năng mở rộng và Bảo trì (Maintainability & Scalability)

* **NFR 4.1: Kiến trúc hướng Module (Modular Architecture):** Logic của các loại bài và các bộ mở rộng phải được thiết kế dưới dạng các Module/Plugin tách biệt. Hệ thống phải cho phép bổ sung các loại bài Meme mới thông qua cấu hình dữ liệu (Data-driven) hoặc thêm lớp (Class) mới mà không cần can thiệp vào mã nguồn cốt lõi (Core Engine).
* **NFR 4.2: Hệ thống Nhật ký và Giám sát (Logging & Monitoring):** Máy chủ phải ghi nhận đầy đủ nhật ký các hành động trong trận đấu (Match Logs) và các lỗi phát sinh (Error Logs) để phục vụ công tác gỡ lỗi, phân tích dữ liệu và hỗ trợ người dùng.
* **NFR 4.3: Khả năng phục hồi (Resilience):** Hệ thống phải có cơ chế lưu trữ trạng thái trận đấu định kỳ để hỗ trợ tính năng kết nối lại (Reconnect) và đảm bảo dữ liệu không bị mất mát khi có sự cố mạng cục bộ.
