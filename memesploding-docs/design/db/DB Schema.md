# Tài liệu Thiết kế Cơ sở Dữ liệu - Dự án Memesploding

## `users`
Lưu trữ thông tin định danh và xác thực cốt lõi.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | UUID | PK | Mã định danh duy nhất của người dùng. |
| `username` | VARCHAR(50) | UNIQUE | Tên đăng nhập (có thể để trống cho Khách). |
| `provider` | VARCHAR(20) | NOT NULL | Nền tảng xác thực: `guest`, `google`, `facebook`. |
| `provider_id` | VARCHAR(255) | | ID định danh từ nền tảng bên ngoài. |
| `created_at` | TIMESTAMP | NOT NULL | Thời điểm tạo bản ghi. |
| `updated_at` | TIMESTAMP | NOT NULL | Thời điểm cập nhật cuối cùng. |

## `profiles`
Lưu trữ thông tin hồ sơ công khai và các chỉ số thăng tiến của người chơi.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `user_id` | UUID | PK, FK (users.id) | Liên kết 1:1 tới tài khoản User. |
| `nickname` | VARCHAR(50) | NOT NULL | Tên hiển thị trong trò chơi. |
| `avatar_url` | TEXT | | Đường dẫn tới ảnh đại diện của người chơi. |
| `level` | INT | DEFAULT 1 | Cấp độ hiện tại của người chơi. |
| `xp` | BIGINT | DEFAULT 0 | Điểm kinh nghiệm tích lũy. |
| `score` | INT | DEFAULT 1000 | Điểm số xếp hạng (ELO). |
| `total_matches`| INT | DEFAULT 0 | Tổng số trận đã chơi. |
| `total_wins` | INT | DEFAULT 0 | Tổng số trận thắng. |
| `bio` | VARCHAR(255) | | Câu giới thiệu ngắn về bản thân. |

## `friendships`
Quản lý mối quan hệ bạn bè và chặn giữa những người dùng.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `user_id_1` | UUID | PK, FK (users.id) | ID của người gửi lời mời. |
| `user_id_2` | UUID | PK, FK (users.id) | ID của người nhận lời mời. |
| `status` | VARCHAR(20) | NOT NULL | Trạng thái: `pending` (chờ), `accepted` (bạn bè), `blocked` (chặn). |
| `created_at` | TIMESTAMP | NOT NULL | Thời điểm tạo yêu cầu kết bạn. |
| `updated_at` | TIMESTAMP | NOT NULL | Thời điểm thay đổi trạng thái cuối cùng. |

## `card_sets`
Định nghĩa các bộ sưu tập bài hiện có (Bộ Gốc và các bộ Mở rộng).

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | SERIAL | PK | Mã định danh duy nhất của bộ bài. |
| `name` | VARCHAR(50) | NOT NULL | Tên bộ bài (VD: Gốc, Nổ Sấp). |
| `description` | TEXT | | Mô tả chung về nội dung bộ bài. |
| `is_active` | BOOLEAN | DEFAULT TRUE | Trạng thái bộ bài có đang được sử dụng hay không. |
| `created_at` | TIMESTAMP | NOT NULL | Thời điểm tạo bản ghi. |

## `cards`
Định nghĩa thuộc tính và mã logic của từng lá bài cụ thể.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | SERIAL | PK | Mã định danh duy nhất của lá bài. |
| `card_set_id` | INT | FK (card_sets.id) | Thuộc bộ bài nào. |
| `code` | VARCHAR(50) | UNIQUE, NOT NULL| Mã định danh cho Game Engine (VD: NOPE, SKIP). |
| `name` | VARCHAR(100) | NOT NULL | Tên hiển thị của lá bài. |
| `description` | TEXT | NOT NULL | Mô tả chức năng của lá bài. |
| `type` | VARCHAR(20) | NOT NULL | Phân loại: `action`, `bomb`, `defuse`, `cat`. |
| `image_url` | TEXT | | Đường dẫn tới ảnh meme chính của lá bài. |
| `icon_url` | TEXT | | Đường dẫn tới biểu tượng nhỏ ở góc lá bài. |

## `rooms`
Quản lý các phòng chơi đang hoạt động.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | UUID | PK | Mã định danh duy nhất của phòng. |
| `host_id` | UUID | FK (users.id) | ID của người chủ phòng. |
| `code` | VARCHAR(10) | UNIQUE, NOT NULL| Mã 6-8 ký tự dùng để chia sẻ phòng. |
| `password` | VARCHAR(50) | | Mật khẩu tùy chọn cho phòng riêng tư. |
| `status` | VARCHAR(20) | NOT NULL | Trạng thái: `waiting` (chờ), `playing` (đang chơi), `finished` (kết thúc). |
| `settings` | JSONB | NOT NULL | Cấu hình: `max_players`, `selected_card_set_ids`, v.v. |
| `created_at` | TIMESTAMP | NOT NULL | Thời điểm tạo phòng. |

## `room_participants`
Theo dõi người chơi và khán giả hiện đang có mặt trong một phòng.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `room_id` | UUID | PK, FK (rooms.id) | Liên kết tới phòng chơi. |
| `user_id` | UUID | PK, FK (users.id) | Liên kết tới người dùng. |
| `role` | VARCHAR(20) | NOT NULL | Vai trò: `player` (người chơi), `spectator` (khán giả). |
| `is_ready` | BOOLEAN | DEFAULT FALSE | Trạng thái sẵn sàng để bắt đầu trận đấu. |
| `joined_at` | TIMESTAMP | NOT NULL | Thời điểm tham gia phòng. |

## `matches`
Lưu trữ thông tin tổng quát của các trận đấu đã kết thúc.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | UUID | PK | Mã định danh duy nhất của trận đấu. |
| `room_code` | VARCHAR(10) | NOT NULL | Tham chiếu tới mã phòng gốc. |
| `settings` | JSONB | NOT NULL | Bản sao cấu hình phòng tại thời điểm bắt đầu trận. |
| `total_players`| INT | NOT NULL | Số lượng người chơi trong trận đấu. |
| `winner_id` | UUID | FK (users.id) | ID của người thắng cuộc. |
| `started_at` | TIMESTAMP | NOT NULL | Thời điểm bắt đầu trận đấu. |
| `ended_at` | TIMESTAMP | | Thời điểm kết thúc trận đấu. |

## `match_participants`
Lưu kết quả cuối cùng cho từng người chơi trong một trận đấu.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `match_id` | UUID | PK, FK (matches.id) | Liên kết tới trận đấu. |
| `user_id` | UUID | PK, FK (users.id) | Liên kết tới người dùng. |
| `final_rank` | INT | NOT NULL | Thứ hạng cuối cùng (1-6). |
| `xp_earned` | INT | DEFAULT 0 | Kinh nghiệm nhận được từ trận đấu này. |
| `score_change` | INT | DEFAULT 0 | Điểm Rank thay đổi (+/-) từ trận đấu này. |

## `match_spectators`
Ghi lại danh sách những người đã xem trận đấu.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `match_id` | UUID | PK, FK (matches.id) | Liên kết tới trận đấu. |
| `user_id` | UUID | PK, FK (users.id) | Liên kết tới khán giả. |
| `joined_at` | TIMESTAMP | NOT NULL | Thời điểm khán giả bắt đầu xem. |

## `match_events`
Nhật ký chi tiết từng hành động trong trận đấu phục vụ Replay.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | UUID | PK | Mã định danh duy nhất của sự kiện. |
| `match_id` | UUID | FK (matches.id) | Liên kết tới trận đấu. |
| `player_id` | UUID | FK (users.id) | Người thực hiện hành động (có thể trống nếu là hệ thống). |
| `sequence` | INT | NOT NULL | Thứ tự thời gian của sự kiện (1, 2, 3...). |
| `event_type` | VARCHAR(50) | NOT NULL | Loại: `PLAY_CARD`, `DRAW_CARD`, `EXPLODE`, v.v. |
| `data` | JSONB | | Dữ liệu chi tiết: card_id, target_id, v.v. |
| `created_at` | TIMESTAMP | NOT NULL | Thời điểm xảy ra sự kiện. |

## `match_snapshots`
Lưu trữ toàn bộ trạng thái bàn chơi để phục vụ kết nối lại (Reconnect).

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | UUID | PK | Mã định danh duy nhất của bản chụp. |
| `match_id` | UUID | FK (matches.id) | Liên kết tới trận đấu. |
| `sequence` | INT | NOT NULL | Khớp với số thứ tự trong `match_events`. |
| `state` | JSONB | NOT NULL | Trạng thái đầy đủ: Bài trên tay, bộ bài, bộ đếm giờ... |
| `created_at` | TIMESTAMP | NOT NULL | Thời điểm tạo bản chụp. |

## `notifications`
Hộp thư chung cho các tin nhắn, lời mời và cảnh báo.

| Cột (Column) | Kiểu dữ liệu | Ràng buộc | Mô tả |
| :--- | :--- | :--- | :--- |
| `id` | UUID | PK | Mã định danh duy nhất của thông báo. |
| `receiver_id` | UUID | FK (users.id) | Người nhận thông báo. |
| `sender_id` | UUID | FK (users.id) | Người gửi thông báo (có thể trống). |
| `type` | VARCHAR(50) | NOT NULL | Loại: `friend_request`, `room_invite`, v.v. |
| `payload` | JSONB | | Dữ liệu động đi kèm (room_id, tên người mời). |
| `status` | VARCHAR(20) | DEFAULT 'unread' | Trạng thái xem: `unread` (chưa đọc), `read` (đã đọc). |
| `action_status`| VARCHAR(20) | DEFAULT 'pending' | Trạng thái hành động: `pending`, `accepted`, `rejected`. |
| `expires_at` | TIMESTAMP | | Thời điểm hết hạn của lời mời (nếu có). |
| `created_at` | TIMESTAMP | NOT NULL | Thời điểm tạo thông báo. |
