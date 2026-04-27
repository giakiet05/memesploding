# REST API - Shared Contract (Server <-> Client)

## Base URL

- Local: `http://localhost:8080/api/v1`
- Remote: `https://api.memesploding.com/api/v1`

---

## Auth

Trừ nhóm `/auth/*`, tất cả endpoint cần header:

```http
Authorization: Bearer <access_token>
```

---

## Response format

### Success (single)

```json
{
  "message": "string", // Thông báo từ server
  "data": {} // Dữ liệu trả về (Payload)
}
```

### Success (list)

```json
{
  "message": "string", // Thông báo từ server
  "data": {
    "items": [], // Danh sách dữ liệu
    "pagination": {
      "page": 1, // Trang hiện tại
      "pageSize": 20, // Số lượng phần tử trên mỗi trang
      "totalCount": 0, // Tổng số phần tử
      "hasMore": false // Còn dữ liệu ở các trang tiếp theo không
    }
  }
}
```

### Error

```json
{
  "message": "string", // Mô tả chi tiết lỗi
  "errorCode": "INTERNAL_ERROR" // Mã lỗi hệ thống định nghĩa
}
```

---

## 1) Authentication

### POST `/auth/guest`

Đăng nhập nhanh dưới chế độ khách.

**Request**

```json
{
  "deviceId": "device-unique-id" // ID duy nhất của thiết bị (vd: UUID do client tự sinh)
}
```

**Response 200**

```json
{
  "message": "Login successful",
  "data": {
    "user": {
      "id": "guid", // ID người dùng
      "username": "string", // Tên hiển thị
      "email": "string", // Email người dùng (có thể null với guest)
      "provider": "Guest", // Nguồn đăng nhập. Enum: "Guest", "Google"
      "avatarUrl": "string", // Đường dẫn ảnh đại diện
      "bio": "string", // Tiểu sử ngắn
      "level": 1, // Cấp độ hiện tại của người dùng
      "score": 0, // Điểm xếp hạng (Elo/MMR)
      "createdAt": "2026-04-11T00:00:00Z", // Thời gian tạo tài khoản
      "updatedAt": "2026-04-11T00:00:00Z" // Thời gian cập nhật thông tin lần cuối
    },
    "accessToken": "jwt", // JWT dùng để gọi các API yêu cầu Auth
    "refreshToken": "jwt", // Token dùng để xin cấp lại accessToken khi hết hạn
    "isNewUser": true // Bằng true nếu là người dùng mới tinh (thường dùng để hiện màn hình đổi tên)
  }
}
```

**Các lỗi có thể gặp:**
- `VALIDATION_FAILED`, `INTERNAL_ERROR`

### POST `/auth/google`

Đăng nhập qua Google.

**Request**

```json
{
  "idToken": "google-id-token" // JWT ID token nhận được từ Google Sign-In
}
```

**Response 200:** cùng format với `/auth/guest`.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `VALIDATION_FAILED`

### POST `/auth/refresh`

Làm mới Access Token khi bị hết hạn.

**Request**

```json
{
  "refreshToken": "refresh-token" // Refresh token còn hiệu lực
}
```

**Response 200:** cùng format với `/auth/guest`.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `VALIDATION_FAILED`

### POST `/auth/logout`

Đăng xuất và thu hồi token.

**Request**

```json
{
  "refreshToken": "refresh-token" // Refresh token cần thu hồi
}
```

**Response 200**

```json
{
  "message": "Logged out successfully",
  "data": null // Không có dữ liệu trả về
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `VALIDATION_FAILED`

---

## 2) Users

### GET `/users/me/profile`

Lấy thông tin cá nhân của người dùng hiện tại.

**Query params:** none

**Response 200**

```json
{
  "message": "Profile retrieved successfully",
  "data": {
    "id": "guid", // User ID
    "username": "string", // Display name
    "email": "string", // User email
    "provider": "Google", // Auth provider
    "avatarUrl": "string", // Avatar URL
    "bio": "string", // Biography
    "level": 5, // User level
    "score": 420, // Ranking score
    "createdAt": "2026-04-11T00:00:00Z",
    "updatedAt": "2026-04-11T00:00:00Z"
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

### PATCH `/users/me/profile`

Cập nhật thông tin cá nhân.

**Request**

```json
{
  "username": "optional", // Tên hiển thị mới
  "bio": "optional", // Tiểu sử mới
  "avatarUrl": "optional" // Đường dẫn ảnh đại diện mới
}
```

**Response 200:** cùng format data như `GET /users/me/profile`.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `VALIDATION_FAILED`

### GET `/users/me/stats`

Lấy số liệu thống kê của người dùng hiện tại.

**Query params:** none

**Response 200**

```json
{
  "message": "User stats retrieved successfully",
  "data": {
    "xp": 1200, // Điểm kinh nghiệm hiện tại
    "nextLevelXp": 2000, // Điểm kinh nghiệm cần để lên cấp tiếp theo
    "score": 450,
    "level": 5,
    "highestScore": 700, // Mức điểm cao nhất từng đạt được
    "totalMatches": 42, // Tổng số trận đã chơi
    "totalWins": 20, // Tổng số trận thắng
    "winRate": 47.62, // Tỉ lệ thắng (phần trăm)
    "globalRank": 12 // Thứ hạng trên bảng xếp hạng toàn cầu
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

### GET `/users/{userId}`

Lấy thông tin của một người dùng khác.

**Path params**

- `userId` (guid)

**Response 200**

```json
{
  "message": "Information retrieved successfully",
  "data": {
    "id": "guid",
    "username": "string",
    "avatarUrl": "string",
    "bio": "string",
    "level": 3,
    "score": 120,
    "relationship": "Accepted" // Mối quan hệ bạn bè. Enum: "None", "PendingSent", "PendingReceived", "Accepted"
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_FOUND`

### GET `/users/{userId}/stats`

Lấy số liệu thống kê của một người dùng khác.

**Path params**

- `userId` (guid)

**Response 200:** cùng format `GET /users/me/stats`.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_FOUND`

### GET `/users`

Tìm kiếm người dùng (phân trang).

**Query params**

- `searchQuery` (string, optional): Tên người dùng cần tìm
- `pagination.page` (int, optional, default 1)
- `pagination.pageSize` (int, optional, default 20)

**Response 200**

```json
{
  "message": "Users retrieved successfully",
  "data": {
    "items": [
      {
        "id": "guid",
        "username": "string",
        "avatarUrl": "string",
        "bio": "string",
        "level": 1,
        "score": 0,
        "relationship": "None" // Friendship status (None, Accepted, PendingSent, PendingReceived)
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalCount": 100,
      "hasMore": true
    }
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

---

## 3) Friends

### GET `/me/friends`

Lấy danh sách bạn bè.

**Query params**

- `status` (optional): Lọc theo trạng thái (vd: "Accepted", "Pending")
- `pagination.page` (int, optional)
- `pagination.pageSize` (int, optional)

**Response 200:** format list user profile.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

### POST `/me/friends/invitations`

Gửi lời mời kết bạn.

**Request**

```json
{
  "userId": "guid" // ID của người dùng muốn kết bạn
}
```

**Response 200**

```json
{
  "message": "Invitation sent successfully",
  "data": {
    "id": "guid", // ID của người được mời
    "username": "string",
    "avatarUrl": "string",
    "bio": "string",
    "level": 1,
    "score": 0,
    "relationship": "PendingSent" // Trạng thái cập nhật (đã gửi yêu cầu)
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_FOUND`, `ALREADY_FRIENDS`, `VALIDATION_FAILED`

### PATCH `/me/friends/invitations/{requesterId}`

Phản hồi (Chấp nhận/Từ chối) lời mời kết bạn.

**Path params**

- `requesterId` (guid)

**Request**

```json
{
  "accept": true // true để chấp nhận, false để từ chối
}
```

**Response 200:** profile của user còn lại với relationship mới.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_FOUND`

### DELETE `/me/friends/{friendId}`

Xoá bạn bè (Hủy kết bạn).

**Path params**

- `friendId` (guid)

**Response 200**

```json
{
  "message": "Friend removed successfully",
  "data": null // Không có payload trả về
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_FOUND`

---

## 4) Rooms

### POST `/rooms`

Tạo phòng chờ mới.

**Request**

```json
{
  "maxPlayers": 6, // Số lượng người chơi tối đa (2-6)
  "isPublic": true, // Phòng có hiển thị công khai ở lobby không
  "cardSetIds": ["guid"] // Danh sách ID các bộ bài được dùng trong phòng
}
```

**Response 201**

```json
{
  "message": "Room created successfully",
  "data": {
    "code": "ABC123", // Mã code duy nhất của phòng (để join nhanh)
    "hostId": "guid", // ID của chủ phòng
    "status": "waiting", // Trạng thái phòng. Enum: "waiting", "playing"
    "isPublic": true,
    "settings": {
      "maxPlayers": 6
    },
    "cardSets": [
      {
        "id": "guid", // ID bộ bài
        "name": "Base Set" // Tên bộ bài
      }
    ],
    "currentParticipants": [
      {
        "userId": "guid", // ID người tham gia
        "nickname": "string",
        "avatarUrl": "string",
        "role": "player", // Vai trò. Enum: "host", "player"
        "isReady": true // Trạng thái sẵn sàng
      }
    ],
    "connection": {
      "wsUrl": "wss://game.memesploding.com/ws", // URL để nối tới Game WebSocket
      "wsAccessToken": "jwt" // Token dùng cho Game WebSocket (chỉ có khi trận bắt đầu)
    }
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `VALIDATION_FAILED`, `PLAYER_ALREADY_IN_ROOM`

### POST `/rooms/{code}/join`

Vào một phòng thông qua mã code.

**Path params**

- `code` (string)

**Request body:** none

**Response 200:** cùng format room detail ở trên.

**Idempotent behavior:**
- Nếu user đã ở đúng room `{code}`, API vẫn trả `200` với room detail hiện tại.
- Nếu user đang ở room khác, API trả lỗi validation.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_FOUND`, `ROOM_IS_FULL`, `MATCH_ALREADY_STARTED`, `PLAYER_ALREADY_IN_ROOM`

### PATCH `/rooms/{code}`

Cập nhật thông tin cài đặt của phòng (Chỉ dành cho chủ phòng).

**Path params**

- `code` (string)

**Request**

```json
{
  "maxPlayers": 6, // (Tuỳ chọn) Đổi số lượng tối đa
  "isPublic": true, // (Tuỳ chọn) Đổi chế độ hiển thị
  "cardSetIds": ["guid"] // (Tuỳ chọn) Đổi bộ bài
}
```

**Response 200:** cùng format room detail ở trên.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `FORBIDDEN`, `NOT_FOUND`, `MATCH_ALREADY_STARTED`, `VALIDATION_FAILED`

### GET `/rooms`

Lấy danh sách phòng công khai.

**Query params**

- `page` (int, optional)
- `pageSize` (int, optional)
- `cardSetIds` (list guid, optional)
- `maxPlayers` (list int, optional)

**Response 200**

```json
{
  "message": "Successfully!",
  "data": {
    "items": [
      {
        "code": "ABC123", // Unique room code
        "hostId": "guid", // Host user ID
        "hostNickname": "string", // Host display name
        "maxPlayers": 6, // Maximum number of players
        "currentPlayers": 3, // Current number of players in the room
        "status": "waiting", // Room status (waiting, playing)
        "cardSets": [{ "id": "guid", "name": "Base Set" }],
        "isPublic": true,
        "createdAt": "2026-04-11T00:00:00Z"
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalCount": 30,
      "hasMore": true
    }
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

### GET `/rooms/{code}`

Lấy chi tiết thông tin của phòng.

**Path params**

- `code` (string)

**Response 200:** room detail giống `POST /rooms`.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_FOUND`

---

## 5) Matchmaking

### POST `/matchmaking/quick-play`

Tự động tìm phòng công khai phù hợp.

**Request body:** none

**Response 200:** room detail.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `PLAYER_ALREADY_IN_ROOM`

---

## 6) Notifications

### GET `/me/notifications`

Lấy danh sách thông báo của người dùng.

**Query params**

- `page` (int, optional)
- `pageSize` (int, optional)

**Response 200**

```json
{
  "message": "Successfully!",
  "data": {
    "items": [
      {
        "id": "guid", // ID của thông báo
        "type": "System", // Loại thông báo. Enum: "System", "FriendRequest", "RoomInvite"
        "payload": "{\"k\":\"v\"}", // Dữ liệu JSON đi kèm
        "isRead": false, // Trạng thái đã đọc hay chưa
        "createdAt": "2026-04-11T00:00:00Z",
        "sender": {
          "id": "guid",
          "username": "string",
          "avatarUrl": "string"
        }
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalCount": 10,
      "hasMore": false
    }
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

### GET `/me/notifications/unread-count`

Đếm số thông báo chưa đọc.

**Query params:** none

**Response 200**

```json
{
  "message": "Successfully!",
  "data": {
    "count": 3 // Số lượng thông báo chưa đọc
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

### PATCH `/notifications/{id}`

Đánh dấu thông báo là đã đọc.

**Path params**

- `id` (guid)

**Request body:** none

**Response 200:** trả về object notification đã cập nhật `isRead: true`.

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_FOUND`

### DELETE `/notifications/{id}`

Xoá một thông báo.

**Path params**

- `id` (guid)

**Response 200**

```json
{
  "message": "Notification deleted successfully",
  "data": {} // Empty payload
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_FOUND`

### PATCH `/notifications/mark-all-read`

Đánh dấu tất cả là đã đọc.

**Request body:** none

**Response 200**

```json
{
  "message": "All notifications marked as read",
  "data": {} // Empty payload
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

### DELETE `/notifications/clear-all`

Xoá tất cả thông báo.

**Response 200**

```json
{
  "message": "All notifications cleared successfully",
  "data": {} // Empty payload
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

---

## 7) Match history

### GET `/me/match-history`

Xem lịch sử đấu của bản thân.

**Query params**

- `page` (int, optional)
- `pageSize` (int, optional)

**Response 200**

```json
{
  "message": "Successfully!",
  "data": {
    "items": [
      {
        "matchId": "guid", // ID trận đấu
        "roomCode": "ABC123",
        "startedAt": "2026-04-11T00:00:00Z",
        "endedAt": "2026-04-11T00:30:00Z",
        "totalPlayers": 4, // Tổng số người chơi
        "finalRank": 1, // Vị trí (hạng) đạt được của bạn
        "xpEarned": 100, // Số XP kiếm được
        "scoreChange": 20, // Số điểm xếp hạng thay đổi (+/-)
        "cardSets": [{ "id": "guid", "name": "Base Set" }],
        "players": [
          {
            "userId": "guid",
            "nickname": "string",
            "avatarUrl": "string",
            "finalRank": 1
          }
        ]
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalCount": 50,
      "hasMore": true
    }
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

---

## 8) Card sets

### GET `/card-sets`

Danh sách tất cả các bộ bài có trong game.

**Query params**

- `page` (int, optional)
- `pageSize` (int, optional)

**Response 200**

```json
{
  "message": "Card sets retrieved successfully",
  "data": {
    "items": [
      {
        "id": "guid", // ID của bộ bài
        "name": "Base Set", // Tên bộ bài
        "description": "string", // Mô tả về bộ bài
        "cardCount": 56, // Số lượng bài trong bộ này
        "imageUrl": "https://...", // URL ảnh bìa của bộ bài
        "isActive": true, // Trạng thái kích hoạt (true thì mới được chọn chơi)
        "createdAt": "2026-04-11T00:00:00Z"
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalCount": 3,
      "hasMore": false
    }
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`

### GET `/card-sets/{id}/cards`

Danh sách các lá bài cụ thể trong một bộ bài.

**Path params**

- `id` (guid)

**Query params**

- `page` (int, optional)
- `pageSize` (int, optional)

**Response 200**

```json
{
  "message": "Cards retrieved successfully",
  "data": {
    "items": [
      {
        "id": "guid", // ID lá bài
        "code": "SeeTheFuture", // Mã định danh code của lá bài
        "name": "See The Future", // Tên hiển thị của lá bài
        "description": "string", // Mô tả hiệu ứng
        "type": "Action", // Phân loại bài. Enum: "Action", "Cat", "Nope", "Defuse", "Bomb"
        "imageUrl": "https://...", // URL ảnh minh hoạ lá bài
        "iconUrl": "https://..." // URL biểu tượng
      }
    ],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalCount": 56,
      "hasMore": true
    }
  }
}
```

**Các lỗi có thể gặp:**
- `UNAUTHORIZED`, `NOT_FOUND`

---

## Danh sách Error Codes quy chuẩn (REST API)

| Error Code | Mô tả |
| :--- | :--- |
| `INTERNAL_ERROR` | Lỗi hệ thống không xác định. |
| `UNAUTHORIZED` | Chưa đăng nhập hoặc token đã hết hạn / không hợp lệ. |
| `FORBIDDEN` | Bạn không có quyền thực hiện hành động này. |
| `NOT_FOUND` | Tài nguyên yêu cầu (người dùng, phòng, thông báo...) không tồn tại. |
| `VALIDATION_FAILED` | Dữ liệu gửi lên không hợp lệ (vd: tên quá dài, ID sai định dạng). |
| `PLAYER_ALREADY_IN_ROOM` | Bạn đã ở trong một phòng khác, vui lòng rời đi trước khi tạo/join phòng mới. |
| `ALREADY_FRIENDS` | Hai người đã là bạn bè hoặc đã có lời mời kết bạn đang chờ. |
| `ROOM_IS_FULL` | Phòng đã đủ số lượng người chơi tối đa, không thể vào thêm. |
| `MATCH_ALREADY_STARTED` | Trận đấu trong phòng này đã bắt đầu, không thể thực hiện hành động. |
| `RATE_LIMITED` | Bạn thao tác quá nhanh, vui lòng thử lại sau ít giây. |
| `ACCOUNT_LOCKED` | Tài khoản đã bị khoá do vi phạm điều khoản. |
