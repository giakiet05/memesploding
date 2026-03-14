# Base URL
- Local: `http://localhost:8080/api/v1`
- Remote: chưa biết

# Success response format

```json
{
	"message": "Successfully!", // for debugging
	"data": {} // data field can be any types, even null
}
```

# Error response format

```json
{
	"message": "Internal error happened!", // for debugging
	"error_code": "INTERNAL_ERROR"
}
```


# 1. Authentication

### POST /auth/google 
Đăng nhập bằng Google

**Request:**
```json
{
	"code": "string" // OAuth2 authorization code
}
```

**Response (200 OK):**
```json
{
	"message": "Login successful",
	"data": {
		"access_token": "string",
		"refresh_token": "string",
		"user": {
			"id": "uuid",
			"provider": "google",
			"provider_id": "string",
			"username": "string",
			"created_at": "timestamp",
			"updated_at": "timestamp"
		}
	}
}
```

### POST /auth/facebook
Đăng nhập Facebook

**Request:**
```json
{
	"access_token": "string"
}
```

**Response (200 OK):**
```json
{
	"message": "Login successful",
	"data": {
		"access_token": "string",
		"refresh_token": "string",
		"user": {
			"id": "uuid",
			"provider": "facebook",
			"provider_id": "string",
			"username": "string",
			"created_at": "timestamp",
			"updated_at": "timestamp"
		}
	}
}
```

### POST /auth/guest
Đăng nhập tài khoản Khách

**Request:**
```json
{
	"device_id": "string" 
}
```

**Response (200 OK):**
```json
{
	"message": "Login successful",
	"data": {
		"access_token": "string",
		"refresh_token": "string",
		"user": {
			"id": "uuid",
			"provider": "guest",
			"provider_id": "string",
			"username": null,
			"created_at": "timestamp",
			"updated_at": "timestamp"
		}
	}
}
```

### POST /auth/refresh
Làm mới Access Token khi hết hạn

**Request:**
```json
{
	"refresh_token": "string"
}
```

**Response (200 OK):**
```json
{
	"message": "Token refreshed",
	"data": {
		"access_token": "string",
		"refresh_token": "string"
	}
}
```


# 2. User Profile

### GET /users/me/profile
Lấy thông tin hồ sơ cơ bản của chính người chơi

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"user_id": "uuid",
		"nickname": "string",
		"avatar_url": "string",
		"bio": "string",
		"level": 1,
		"created_at": "timestamp"
	}
}
```

### PATCH /users/me/profile
Cập nhật thông tin hồ sơ cá nhân

**Request:**
```json
{
	"nickname": "string", // optional
	"avatar_url": "string", // optional
	"bio": "string" // optional
}
```

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"user_id": "uuid",
		"nickname": "string",
		"avatar_url": "string",
		"bio": "string",
		"level": 1,
		"created_at": "timestamp"
	}
}
```

### GET /users/me/stats
Lấy các thông số thống kê chi tiết của người chơi

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"xp": 540,
		"next_level_xp": 1000,
		"score": 1250,
		"highest_score": 1500,
		"total_matches": 50,
		"total_wins": 25,
		"win_rate": 50.0
	}
}
```

### GET /users/:id/profile
Lấy thông tin hồ sơ cơ bản của người chơi khác

**Response (200 OK):**
*(Giống GET /users/me/profile response)*

### GET /users/:id/stats
Lấy thông số thống kê chi tiết của người chơi khác

**Response (200 OK):**
*(Giống GET /users/me/stats response)*


# 3. Friends

### GET /users/me/friends 
Lấy danh sách bạn bè (bao gồm cả các yêu cầu đang chờ - status='pending')

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": [
		{
			"user_id": "uuid",
			"nickname": "string",
			"avatar_url": "string",
			"level": 1,
			"status": "accepted" // accepted, pending, blocked
		}
	]
}
```

### GET /users/search
Tìm kiếm người chơi theo nickname hoặc ID

**Query Params:** `?q=keyword`

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": [
		{
			"user_id": "uuid",
			"nickname": "string",
			"avatar_url": "string",
			"level": 1
		}
	]
}
```

### POST /friends/invitation
Gửi lời mời kết bạn

**Request:**
```json
{
	"user_id": "uuid"
}
```

**Response (200 OK):**
{ "message": "Friend request sent", "data": null }

### PATCH /friends/:id
Chấp nhận hoặc từ chối lời mời kết bạn

**Request:**
```json
{
	"action": "accept" // accept, reject
}
```

**Response (200 OK):**
{ "message": "Friend request processed", "data": null }

### POST /friends/block/:id
Chặn người chơi

**Response (200 OK):**
{ "message": "User blocked successfully", "data": null }

### DELETE /friends/:id
Hủy kết bạn (chỉ dành cho status='accepted')

**Response (200 OK):**
{ "message": "Friendship removed successfully", "data": null }


# 4. Rooms

### POST /rooms
Tạo phòng chơi mới

**Request:**
```json
{
	"password": "string", // optional
	"max_players": 6,
	"card_set_ids": ["uuid"]
}
```

**Response (201 Created):**
```json
{
	"message": "Room created successfully",
	"data": {
		"room": {
			"code": "ABC123",
			"host_id": "uuid",
			"status": "waiting",
			"settings": {
				"max_players": 6,
				"turn_timer": 15
			},
			"card_sets": [
				{ "id": "uuid", "name": "Base Set" }
			],
			"current_participants": [
				{ "user_id": "uuid", "nickname": "string", "avatar_url": "string", "role": "player", "is_ready": true }
			]
		},
		"connection": {
			"ws_url": "wss://game.memesploding.com/ws",
			"ws_access_token": "string"
		}
	}
}
```

### GET /rooms/:code
Xem thông tin phòng trước khi tham gia

**Response (200 OK):**
*(Trả về object "room" tương tự POST /rooms nhưng không có object "connection")*

### POST /rooms/:code/join
Tham gia vào phòng chơi

**Request:**
```json
{
	"password": "string" // optional
}
```

**Response (200 OK):**
*(Giống POST /rooms response format)*

### POST /rooms/:code/leave
Rời khỏi phòng (khi đang ở sảnh chờ)

**Response (200 OK):**
{ "message": "Left room successfully", "data": null }

### PATCH /rooms/:code
Cập nhật cấu hình phòng (chỉ dành cho Chủ phòng)

**Request:**
```json
{
	"max_players": 5, // optional
	"card_set_ids": ["uuid"], // optional
	"password": "string" // optional
}
```

**Response (200 OK):**
*(Trả về object "room" đã cập nhật)*

### DELETE /rooms/:code
Giải tán phòng (chỉ dành cho Chủ phòng)

**Response (200 OK):**
{ "message": "Room deleted successfully", "data": null }

### GET /users/me/room-invitations
Lấy danh sách lời mời vào phòng đang chờ xử lý

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": [
		{
			"invitation_id": "uuid",
			"sender": { "id": "uuid", "nickname": "string", "avatar_url": "string" },
			"room_code": "ABC123",
			"expires_at": "timestamp",
			"created_at": "timestamp"
		}
	]
}
```

### POST /rooms/:code/invite
Gửi lời mời tham gia phòng cho bạn bè (Host gọi)

**Request:**
```json
{
	"receiver_id": "uuid"
}
```

**Response (200 OK):**
{ "message": "Invitation sent", "data": null }

### PATCH /room-invitations/:id
Chấp nhận hoặc từ chối lời mời vào phòng (Người nhận gọi)

**Request:**
```json
{
	"action": "accept" // accept, reject
}
```

**Response (200 OK):**
*(Nếu accept, trả về object "room" và "connection" tương tự POST /rooms)*


# 5. Matchmaking

### POST /matchmaking/quick-play
Xếp trận nhanh dựa trên cấu hình bộ bài chọn sẵn

**Request:**
```json
{
	"card_set_ids": ["uuid"]
}
```

**Response (200 OK):**
*(Giống POST /rooms response format)*


# 6. Notification 

### GET /users/me/notifications
Lấy danh sách thông báo tin tức (lên cấp, kết thúc trận...)

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": [
		{
			"id": "uuid",
			"type": "level_up",
			"payload": { "old_level": 5, "new_level": 6 },
			"status": "unread",
			"created_at": "timestamp"
		}
	]
}
```

### GET /users/me/notifications/unread-count
Lấy số lượng thông báo chưa đọc

**Response (200 OK):**
{ "message": "Successfully!", "data": { "unread_count": 5 } }

### DELETE /notifications/:id
Xóa một thông báo

### DELETE /notifications/clear-all
Xóa tất cả thông báo


# 7. Leaderboard

### GET /leaderboard/global
Bảng xếp hạng toàn cầu

### GET /leaderboard/friends
Bảng xếp hạng trong danh sách bạn bè


# 8. Static Data

### GET /card-sets
Lấy danh sách các bộ bài

### GET /card-sets/:id/cards
Lấy danh sách các lá bài trong một bộ cụ thể
