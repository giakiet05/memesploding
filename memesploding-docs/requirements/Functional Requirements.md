# Yêu cầu chức năng (Functional Requirements - FR) - Dự án Memesploding

Tài liệu này liệt kê chi tiết các tính năng cần có của trò chơi Memesploding.

## 1. Quản lý người dùng và tài khoản (User & Account)

* **FR 1.1: Chế độ Khách (Guest):** Hệ thống cho phép người chơi tham gia trải nghiệm game ngay lập tức mà không cần đăng ký. Dữ liệu (điểm số, cấp độ) của khách sẽ được lưu tạm thời trên thiết bị hiện tại.
* **FR 1.2: Đăng nhập và Đồng bộ:** Người chơi có thể đăng nhập thông qua các nền tảng mạng xã hội (Google, Facebook) để lưu trữ dữ liệu vĩnh viễn trên máy chủ. Hệ thống hỗ trợ tính năng liên kết dữ liệu từ tài khoản Khách vào tài khoản chính thức.
* **FR 1.3: Hồ sơ cá nhân (Profile):**
    * Hiển thị và cho phép thay đổi thông tin người chơi: Biệt danh (Nickname), Ảnh đại diện (Avatar - từ danh sách mặc định hoặc từ tài khoản mạng xã hội).
    * Thống kê trận đấu: Hiển thị tổng số trận đã chơi, số trận thắng, tỷ lệ thắng, cấp độ (Level), điểm kinh nghiệm (XP), điểm số và xếp hạng.
* **FR 1.4: Hệ thống Điểm số và Xếp hạng:** Hệ thống bảng xếp hạng người chơi dựa vào điểm số. Điểm số sẽ được cộng trừ linh hoạt sau mỗi ván chơi. 
* **FR 1.5: Danh sách Bạn bè:**
    * Cho phép tìm kiếm người chơi khác thông qua mã ID duy nhất.
    * Gửi, chấp nhận hoặc từ chối lời mời kết bạn.
    * Hiển thị trạng thái trực tuyến của bạn bè (Đang rảnh, Đang trong trận, Ngoại tuyến).
* **FR 1.6: Hệ thống Lời mời:** Hệ thống xử lý và thông báo các lời mời tham gia phòng chơi từ bạn bè hoặc thông qua các liên kết (Link) chia sẻ từ bên ngoài.

## 2. Quản lý phòng chơi và Ghép trận (Room & Matchmaking)

* **FR 2.1: Tìm trận nhanh (Quick Match):** Hệ thống tự động tìm kiếm các phòng chơi còn chỗ trống để đưa người chơi vào.  Người dùng có thể chọn tổ hợp bộ bài sử dụng (bộ gốc + các bộ mở rộng) để hệ thống xếp vào phòng phù hợp. Nếu không tìm thấy phòng phù hợp, hệ thống sẽ tự động tạo một phòng mới có tổ hợp bài như người chơi chọn.
* **FR 2.2: Tạo phòng tùy chỉnh (Custom Room):**
    * Cho phép người chơi thiết lập Tên phòng, Số người chơi (2-6 người)
    * Tùy chỉnh cấu hình trận đấu: Chọn sử dụng bộ bài Cơ bản hoặc kết hợp thêm các bộ mở rộng (Imploding, Barking, Streaking).
* **FR 2.3: Phòng chờ (Waiting Room):**
    * Hiển thị danh sách người chơi trong phòng cùng với ảnh đại diện.
    * Chủ phòng (Host) có quyền mời người chơi khác hoặc đuổi (Kick) người chơi khỏi phòng.
    * Tất cả người chơi phải xác nhận "Sẵn sàng" (Ready) trước khi Chủ phòng có thể bắt đầu (Start) trận đấu.
* **FR 2.4: Chia sẻ phòng:** Hỗ trợ tạo mã phòng hoặc liên kết (Link) để người chơi có thể gửi cho bạn bè tham gia trực tiếp vào phòng.

## 3. Cơ chế trò chơi (Gameplay Mechanics)

* **FR 3.1: Hệ thống Quản lý lượt đi (Turn Management):**
    * Tự động xác định người chơi bắt đầu trận đấu ngẫu nhiên.
    * Quản lý trình tự lượt đi (theo chiều kim đồng hồ hoặc ngược lại khi có lá bài Đảo chiều).
    * Phân định rõ hai giai đoạn trong một lượt: Giai đoạn hành động (người chơi có thể đánh bài hoặc không) và Giai đoạn rút bài (kết thúc lượt).
    * Xử lý cộng dồn số lượt phải thực hiện khi bị dính các lá bài Tấn công.
    * Bộ đếm thời gian (Timer) cho mỗi lượt chơi. Nếu người chơi không thực hiện hành động trước khi hết giờ, hệ thống sẽ tự động rút bài để kết thúc lượt.

* **FR 3.2: Hệ thống Phản ứng (Nope/Reaction System):**
    * Khi một lá bài có chức năng được đánh ra (trừ lá Mèo nổ và Giải bom), hệ thống sẽ mở một cửa sổ thời gian chờ (ví dụ: 3-5 giây) cho tất cả người chơi khác.
    * Cho phép người chơi đánh lá "Nope" để vô hiệu hóa hành động vừa xảy ra.
    * Hỗ trợ cơ chế "Nope chồng Nope": Nếu một lá Nope bị Nope, hành động ban đầu sẽ được khôi phục hiệu lực.
    * Chỉ thực thi hiệu ứng của lá bài sau khi cửa sổ thời gian chờ kết thúc mà không có lá Nope nào được đánh thêm.

* **FR 3.3: Hệ thống Tương tác Chồng bài (Deck Interaction):**
    * Tự động xáo trộn bộ bài khi bắt đầu trận đấu hoặc khi có yêu cầu từ lá bài "Xáo bài".
    * Hỗ trợ các hành động xem trước: Cho phép người chơi xem X lá đầu tiên của chồng bài rút mà không để người khác thấy.
    * Hỗ trợ sắp xếp lại bài: Cho phép người chơi thay đổi thứ tự X lá đầu tiên của chồng bài rút.
    * Xử lý rút bài đa dạng: Rút từ trên cùng, rút từ dưới đáy (lá Draw from Bottom) hoặc đặt lại bài vào vị trí bất kỳ (lá Giải bom/Mèo nổ sấp).

* **FR 3.4: Hệ thống Xử lý Bom (Explosion System):**
    * **Phát hiện Bom:** Hệ thống nhận diện ngay lập tức khi người chơi rút trúng lá Mèo nổ hoặc Mèo nổ sấp (Imploding Kitten).
    * **Trạng thái Gỡ bom:** Kích hoạt cửa sổ thời gian chờ (ví dụ: 10 giây) để người chơi chọn lá "Giải bom" (Defuse) từ tay.
    * **Xử lý khi Giải bom thành công:** Hệ thống cung cấp giao diện riêng tư để người chơi bí mật chọn vị trí chính xác trong chồng bài rút để đặt lại lá Mèo nổ. Đối với lá Mèo nổ sấp ở trạng thái chưa lật, người chơi phải đặt ngửa lá bài này vào một vị trí bất kỳ (không cần dùng Defuse).
    * **Xử lý khi Giải bom thất bại (Người chơi bị loại):** Người chơi bị loại khỏi trận đấu ngay lập tức, toàn bộ bài trên tay bị hủy và lá Mèo nổ gây ra cái chết sẽ bị loại bỏ hoàn toàn khỏi chồng bài rút.

* **FR 3.5: Hệ thống Chọn mục tiêu và Combo (Targeting & Combo):**
    * Cung cấp giao diện để người chơi chọn một đối tượng cụ thể khi đánh các lá bài có mục tiêu (như Xin bài, Tấn công có mục tiêu).
    * Tự động nhận diện và kiểm tra tính hợp lệ của các tổ hợp bài (Combo 2 lá giống nhau, 3 lá giống nhau, hoặc 5 lá khác nhau).
    * Thực hiện các hành động tương ứng: Rút ngẫu nhiên bài của đối thủ, yêu cầu lá bài cụ thể, hoặc hồi lại bài từ chồng bài hủy.

* **FR 3.6: Quản lý Trạng thái đặc biệt (Special States):**
    * Trạng thái "Mù" (Lời nguyền mông mèo): Úp toàn bộ bài trên tay người chơi và buộc họ chọn bài ngẫu nhiên để đánh.
    * Trạng thái "Bảo vệ" (Mũ tháp): Ngăn chặn các hành động lấy bài từ người khác hướng vào người chơi đang đội mũ.
    * Trạng thái "Cầm bom" (Mèo mặc quần xì): Cho phép người chơi giữ lá Mèo nổ trên tay mà không bị loại.

* **FR 3.7: Hệ thống Nhật ký hành động (Action Log):**
    * Ghi lại và hiển thị theo thời gian thực danh sách các hành động vừa xảy ra trong trận đấu (Ví dụ: "Người chơi A đã đánh lá Tấn công vào Người chơi B").
    * Lưu trữ lịch sử hành động để người chơi có thể xem lại khi cần thiết, giúp theo dõi diễn biến trận đấu dễ dàng hơn.

## 4. Tính năng xã hội và Tương tác (Social & Interaction)

* **FR 4.1: Hệ thống Chat (Chat System):**
    * Hỗ trợ nhắn tin văn bản trong phòng chờ (Lobby) và trong suốt quá trình diễn ra trận đấu.
    * **Chat nhanh (Quick Chat):** Cung cấp danh sách các câu nói "viral" hoặc các cụm từ meme phổ biến để người chơi tương tác nhanh chóng mà không cần gõ phím.
* **FR 4.2: Hệ thống Biểu cảm (Stickers & Emojis):**
    * Cho phép người chơi gửi các biểu tượng cảm xúc hoặc nhãn dán (Sticker) hình meme trực tiếp lên màn hình trận đấu.
    * Các Sticker này sẽ hiển thị đè lên khu vực đại diện của người chơi trong một khoảng thời gian ngắn để tạo không khí vui nhộn.
* **FR 4.3: Hệ thống Thông báo (Notification System):**
    * Thông báo thời gian thực khi nhận được lời mời kết bạn hoặc lời mời tham gia phòng từ người chơi khác.
    * Thông báo kết quả sau khi trận đấu kết thúc bao gồm: Thăng cấp (Level up), thay đổi điểm số và thứ hạng.
* **FR 4.4: Theo dõi trận đấu (Spectator Mode):** Cho phép người chơi trong danh sách bạn bè có thể tham gia xem các trận đấu đang diễn ra của nhau (nếu phòng chơi đó được thiết lập cho phép xem).

## 5. Khả năng phục hồi (Resilience)

* **FR 5.1: Kết nối lại (Reconnect):** Khi người chơi bị mất kết nối mạng hoặc thoát game đột ngột, hệ thống cho phép họ tham gia lại vào trận đấu đang diễn ra trong vòng một khoảng thời gian quy định (ví dụ: 60 giây).
* **FR 5.2: Tự động hóa khi vắng mặt (Auto-play/AFK):** Trong khoảng thời gian người chơi bị mất kết nối, hệ thống sẽ tự động thực hiện hành động rút bài khi hết lượt để đảm bảo trận đấu không bị gián đoạn cho những người chơi còn lại.
* **FR 5.3: Đồng bộ trạng thái (State Sync):** Đảm bảo bài trên tay và các trạng thái đặc biệt (như Mông mèo, Mũ tháp) của người chơi được khôi phục chính xác sau khi kết nối lại thành công.
* **FR 5.4: Xử lý sự cố máy chủ (Server Failure Handling):** Trong trường hợp máy chủ gặp sự cố nghiêm trọng khiến trận đấu bị hủy bỏ, hệ thống sẽ ghi nhận và đảm bảo không trừ điểm xếp hạng của người chơi một cách bất công.




