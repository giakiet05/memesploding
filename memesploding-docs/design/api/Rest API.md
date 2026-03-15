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

### *List response format*
```json
{
	"message": "Successfully",
	"data": {
		"items": [],
		"pagination": {
			"page": 1,
			"page_size": 20,
			"total_count": 200,
			"has_more": true
		}
	}
}

```
# Error response format

```json
{
	"message": "Internal error happened!", // for debugging
	"error_code": "INTERNAL_ERROR"
}
```

# Authentication

Tất cả endpoints (trừ `/auth/*` endpoints) đều cần gửi access token qua header:

**Header:**
```
Authorization: Bearer <access_token>
```

Nếu access token hết hạn, gọi `POST /auth/refresh` để lấy token mới.

---

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

### POST /auth/logout
Logout user và hủy token

**Response (200 OK):**
```json
{
	"message": "Logged out successfully",
	"data": null
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
		"win_rate": 50.0,
		"global_rank": 42,
		"friend_rank": 5
	}
}
```

### GET /users/:id/profile
Lấy thông tin hồ sơ cơ bản của người chơi khác

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

### GET /users/:id/stats
Lấy thông số thống kê chi tiết của người chơi khác

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
		"win_rate": 50.0,
		"global_rank": 42,
		"friend_rank": 5
	}
}
```


# 3. Friends

### GET /users/me/friends 
Lấy danh sách bạn bè (bao gồm cả các yêu cầu đang chờ - status='pending')

**Note:** `is_online` field là snapshot tại thời điểm request (từ Redis). Để có real-time updates, client cần subscribe WebSocket channel `friend_status`. Xem ADR-0004 cho chi tiết.

**Query Params:** `?page=1&page_size=50&status=accepted|pending`

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"items": [
			{
				"user_id": "uuid",
				"nickname": "string",
				"avatar_url": "string",
				"level": 1,
				"status": "accepted",
				"is_online": true
			}
		],
		"pagination": {
			"page": 1,
			"page_size": 50,
			"total_count": 45,
			"has_more": false
		}
	}
}
```

### GET /users/search
Tìm kiếm người chơi theo nickname hoặc ID

**Query Params:** `?q=keyword&page=1&page_size=50`

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"items": [
			{
				"user_id": "uuid",
				"nickname": "string",
				"avatar_url": "string",
				"level": 1
			}
		],
		"pagination": {
			"page": 1,
			"page_size": 50,
			"total_count": 200,
			"has_more": true
		}
	}
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
```json
{
	"message": "Friend request sent",
	"data": null
}
```

### PATCH /friends/:id
Chấp nhận hoặc từ chối lời mời kết bạn

**Request:**
```json
{
	"action": "accept" // accept, reject
}
```

**Response (200 OK):**
```json
{
	"message": "Friend request processed",
	"data": null
}
```

### DELETE /friends/:id
Hủy kết bạn (chỉ dành cho status='accepted')

**Response (200 OK):**
```json
{
	"message": "Friendship removed successfully",
	"data": null
}
```

### DELETE /friends/:id
Hủy kết bạn (chỉ dành cho status='accepted')

**Response (200 OK):**
```json
{
	"message": "Friendship removed successfully",
	"data": null
}
```


# 4. Rooms

### POST /rooms
Tạo phòng chơi mới

**Request:**
```json
{
	"max_players": 6,
	"is_public": true,
	"card_set_ids": ["uuid"]
}
```

**Response (201 Created):**
```json
{
	"message": "Room created successfully",
	"data": {
		"code": "ABC123",
		"host_id": "uuid",
		"status": "waiting",
		"is_public": true,
		"settings": {
			"max_players": 6,
			"turn_timer": 15
		},
		"card_sets": [
			{ "id": "uuid", "name": "Base Set" }
		],
		"current_participants": [
			{ 
				"user_id": "uuid", 
				"nickname": "string", 
				"avatar_url": "string", 
				"role": "player", 
				"is_ready": true 
			}
		],
		"connection": {
			"ws_url": "wss://game.memesploding.com/ws",
			"ws_access_token": "string"
		}
	}
}
```


### GET /rooms
Lấy danh sách phòng chơi công khai (public)

**Query Params:** `?page=1&page_size=50&card_set_ids=uuid1,uuid2&max_players=4,6` (optional)
- `card_set_ids`: Filter by card set (comma-separated)
- `max_players`: Filter by player count (comma-separated)

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"items": [
			{
				"code": "ABC123",
				"host_id": "uuid",
				"host_nickname": "string",
				"max_players": 6,
				"current_players": 3,
				"status": "waiting",
				"card_sets": [
					{ "id": "uuid", "name": "Base Set" }
				],
				"is_public": true,
				"created_at": "timestamp"
			}
		],
		"pagination": {
			"page": 1,
			"page_size": 50,
			"total_count": 120,
			"has_more": true
		}
	}
}
```


### GET /rooms/:code
Xem thông tin phòng trước khi tham gia

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"code": "ABC123",
		"host_id": "uuid",
		"status": "waiting",
		"is_public": true,
		"settings": {
			"max_players": 6,
			"turn_timer": 15
		},
		"card_sets": [
			{ "id": "uuid", "name": "Base Set" }
		],
		"current_participants": [
			{ 
				"user_id": "uuid", 
				"nickname": "string", 
				"avatar_url": "string", 
				"role": "player", 
				"is_ready": true 
			}
		]
	}
}
```

### POST /rooms/:code/join
Tham gia vào phòng chơi

**Response (200 OK):**
```json
{
	"message": "Joined room successfully",
	"data": {
		"code": "ABC123",
		"host_id": "uuid",
		"status": "waiting",
		"is_public": true,
		"settings": {
			"max_players": 6,
			"turn_timer": 15
		},
		"card_sets": [
			{ "id": "uuid", "name": "Base Set" }
		],
		"current_participants": [
			{ 
				"user_id": "uuid", 
				"nickname": "string", 
				"avatar_url": "string", 
				"role": "player", 
				"is_ready": true 
			}
		],
		"connection": {
			"ws_url": "wss://game.memesploding.com/ws",
			"ws_access_token": "string"
		}
	}
}
```

### POST /rooms/:code/leave
Rời khỏi phòng (khi đang ở sảnh chờ)

**Response (200 OK):**
```json
{
	"message": "Left room successfully",
	"data": null
}
```

### PATCH /rooms/:code
Cập nhật cấu hình phòng (chỉ dành cho Chủ phòng)

**Request:**
```json
{
	"max_players": 5, // optional
	"is_public": true, // optional
	"card_set_ids": ["uuid"] // optional
}
```

**Response (200 OK):**
```json
{
	"message": "Room updated successfully",
	"data": {
		"code": "ABC123",
		"host_id": "uuid",
		"status": "waiting",
		"is_public": true,
		"settings": {
			"max_players": 5,
			"turn_timer": 15
		},
		"card_sets": [
			{ "id": "uuid", "name": "Base Set" }
		],
		"current_participants": [
			{ "user_id": "uuid", "nickname": "string", "avatar_url": "string", "role": "player", "is_ready": true }
		]
	}
}
```

### DELETE /rooms/:code
Giải tán phòng (chỉ dành cho Chủ phòng)

**Response (200 OK):**
```json
{
	"message": "Room deleted successfully",
	"data": null
}
```

### PATCH /rooms/:code/participants/me
Cập nhật trạng thái sẵn sàng của người chơi

**Request:**
```json
{
	"is_ready": true
}
```

**Response (200 OK):**
```json
{
	"message": "Ready status updated",
	"data": {
		"is_ready": true
	}
}
```

### DELETE /rooms/:code/participants/:user_id
Chủ phòng đuổi người chơi khỏi phòng

**Response (200 OK):**
```json
{
	"message": "Player kicked successfully",
	"data": null
}
```


# 5. Matchmaking

### POST /matchmaking/quick-play
Xếp trận nhanh, ngẫu nhiên.

**Request:**
```json
{
	"card_set_ids": ["uuid"]
}
```

**Response (200 OK):**
```json
{
	"message": "Matched successfully",
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


# 6. Notification 

### GET /users/me/notifications
Lấy danh sách thông báo tin tức (lên cấp, kết thúc trận...)

**Query Params:** `?page=1&page_size=50` (optional)

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"items": [
			{
				"id": "uuid",
				"type": "level_up",
				"payload": { "old_level": 5, "new_level": 6 },
				"status": "unread",
				"created_at": "timestamp",
				"sender": { // if sent by another player
					"id": "uuid",
					"nickname": "string",
					"avatar_url": "string"
				}
			}
		],
		"pagination": {
			"page": 1,
			"page_size": 50,
			"total_count": 120,
			"has_more": true
		}
	}
}
```

### GET /users/me/notifications/unread-count
Lấy số lượng thông báo chưa đọc

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"unread_count": 5
	}
}
```

### DELETE /notifications/:id
Xóa một thông báo

**Response (200 OK):**
```json
{
	"message": "Notification deleted successfully",
	"data": null
}
```

### PATCH /notifications/:id
Đánh dấu thông báo là đã đọc

**Request:**
```json
{
	"status": "read"
}
```

**Response (200 OK):**
```json
{
	"message": "Notification marked as read",
	"data": {
		"id": "uuid",
		"status": "read"
	}
}
```

### PATCH /notifications/mark-all-read
Đánh dấu tất cả thông báo là đã đọc

**Request:**
```json
{}
```

**Response (200 OK):**
```json
{
	"message": "All notifications marked as read",
	"data": null
}
```

### DELETE /notifications/clear-all
Xóa tất cả thông báo

**Response (200 OK):**
```json
{
	"message": "All notifications cleared",
	"data": null
}
```


# 7. Match History

### GET /users/me/match-history
Lấy lịch sử trận đấu của bản thân

**Query Params:** `?page=1&page_size=20` (optional)

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": [
		{
			"match_id": "uuid",
			"room_code": "ABC123",
			"started_at": "timestamp",
			"ended_at": "timestamp",
			"total_players": 4,
			"final_rank": 1,
			"xp_earned": 150,
			"score_change": 25,
			"card_sets": [
				{ "id": "uuid", "name": "Base Set" }
			],
			"players": [
				{ "user_id": "uuid", "nickname": "string", "avatar_url": "string", "final_rank": 1 }
			]
		}
	]
}
```

### GET /users/:id/match-history
Lấy lịch sử trận đấu của người chơi khác

**Query Params:** `?page=1&page_size=20` (optional)

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"items": [
			{
				"match_id": "uuid",
				"room_code": "ABC123",
				"started_at": "timestamp",
				"ended_at": "timestamp",
				"total_players": 4,
				"final_rank": 1,
				"xp_earned": 150,
				"score_change": 25,
				"card_sets": [
					{ "id": "uuid", "name": "Base Set" }
				],
				"players": [
					{ "user_id": "uuid", "nickname": "string", "avatar_url": "string", "final_rank": 1 }
				]
			}
		],
		"pagination": {
			"page": 1,
			"page_size": 20,
			"total_count": 85,
			"has_more": true
		}
	}
}
```

### GET /matches/:match_id
Lấy chi tiết một trận đấu cụ thể (bao gồm sự kiện và replay)

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"match_id": "uuid",
		"room_code": "ABC123",
		"started_at": "timestamp",
		"ended_at": "timestamp",
		"total_players": 4,
		"card_sets": [
			{ "id": "uuid", "name": "Base Set" }
		],
		"participants": [
			{
				"user_id": "uuid",
				"nickname": "string",
				"avatar_url": "string",
				"final_rank": 1,
				"xp_earned": 150,
				"score_change": 25
			}
		],
		"events": [
			{
				"sequence": 1,
				"event_type": "PLAY_CARD",
				"player_id": "uuid",
				"data": { "card_id": "uuid", "target_id": "uuid" },
				"created_at": "timestamp"
			}
		],
		"spectators": [
			{ "user_id": "uuid", "nickname": "string", "joined_at": "timestamp" }
		]
	}
}
```


# 8. Leaderboard

### GET /leaderboard/global
Bảng xếp hạng toàn cầu

**Query Params:** `?page=1&page_size=100` (optional)

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"items": [
			{
				"rank": 1,
				"user_id": "uuid",
				"nickname": "string",
				"avatar_url": "string",
				"level": 10,
				"score": 5000,
				"total_wins": 100
			}
		],
		"pagination": {
			"page": 1,
			"page_size": 100,
			"total_count": 50000,
			"has_more": true
		}
	}
}
```

### GET /leaderboard/friends
Bảng xếp hạng trong danh sách bạn bè

**Query Params:** `?page=1&page_size=50` (optional)

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"items": [
			{
				"rank": 1,
				"user_id": "uuid",
				"nickname": "string",
				"avatar_url": "string",
				"level": 10,
				"score": 5000,
				"total_wins": 100
			}
		],
		"pagination": {
			"page": 1,
			"page_size": 50,
			"total_count": 45,
			"has_more": false
		}
	}
}
```


# 9. Static Data

### GET /card-sets
Lấy danh sách các bộ bài

**Query Params:** `?page=1&page_size=50` (optional)

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"items": [
			{
				"id": "uuid",
				"name": "Base Set",
				"description": "string",
				"card_count": 56,
				"image_url": "string",
				"is_active": true,
				"created_at": "timestamp"
			}
		],
		"pagination": {
			"page": 1,
			"page_size": 50,
			"total_count": 12,
			"has_more": false
		}
	}
}
```

### GET /card-sets/:id/cards
Lấy danh sách các lá bài trong một bộ cụ thể

**Query Params:** `?page=1&page_size=100` (optional)

**Response (200 OK):**
```json
{
	"message": "Successfully!",
	"data": {
		"items": [
			{
				"id": "uuid",
				"code": "EXPLODING_KITTEN",
				"name": "Exploding Kitten",
				"description": "string",
				"type": "bomb",
				"image_url": "string",
				"icon_url": "string"
			}
		],
		"pagination": {
			"page": 1,
			"page_size": 100,
			"total_count": 56,
			"has_more": false
		}
	}
}
```
