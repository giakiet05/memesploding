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
	"code": "string"
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
			"id": "string",
			"google_id": "string",
			"display_name": "string",
			"email": "string",
			"avatar_url": "string",
			"monthly_budget": 1000,
			"currency": "string",
			"created_at": "timestamp",
			"updated_at": "timestamp"
		}
	}
}
```


### POST /auth/facebook
Đăng nhập Facebook

### POST /auth/guest
Đăng nhập khách

### POST /auth/refresh
Refresh token


# 2. User Profile
### GET /users/me/profile
Lấy profile cá nhân

### PATCH /users/me/profile
Sửa profile cá nhân

### GET /users/me/stats
Lấy thông số chi tiết của user

# 3. Friends
### GET /users/me/friends 
Lấy danh sách bạn bè

### GET /users/:id
Xem thông tin của 1 người chơi

### GET /users/search
Tìm kiếm users

### POST /friends/invitation
Gửi lời mời kết bạn

### POST /friends/block/:id
Block trẻ trâu

### DELETE /friends/:id
Hủy kết bạn 

**Endpoint đồng ý/ từ chối kết bạn ở phần notification**

# 4. Rooms

### POST /rooms
Tạo phòng

### GET /rooms/:code
Xem thông tin phòng

### POST /rooms/:code/invite
Gửi lời mời vào phòng 

### POST /rooms/:code/join
Vào phòng

### POST /rooms/:code/leave
Rời phòng 

### PATCH /rooms/:code
Sửa thông tin phòng

### DELETE /rooms/:code
Xóa phòng

# 5. Matchmaking

### POST /matchmaking/quick-play
Xếp trận nhanh


# 6. Notification 

### GET /users/me/notifications
Lấy thông báo của người chơi

### GET /users/me/notifications/unread-count
Lấy số lượng thông báo chưa đọc

### PATCH /notifications/:id
Tương tác với thông báo (đồng ý, từ chối kết bạn,...)

### DELETE /notifications/:id
Xóa 1 thông báo

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