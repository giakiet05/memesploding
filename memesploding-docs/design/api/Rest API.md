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
  "message": "string",
  "data": {}
}
```

### Success (list)

```json
{
  "message": "string",
  "data": {
    "items": [],
    "pagination": {
      "page": 1,
      "pageSize": 20,
      "totalCount": 0,
      "hasMore": false
    }
  }
}
```

### Error

```json
{
  "message": "string",
  "errorCode": "INTERNAL_ERROR"
}
```

---

## 1) Authentication

### POST `/auth/guest`

**Request**

```json
{
  "deviceId": "device-unique-id"
}
```

**Response 200**

```json
{
  "message": "Login successful",
  "data": {
    "user": {
      "id": "guid",
      "username": "string",
      "email": "string",
      "provider": "Guest",
      "avatarUrl": "string",
      "bio": "string",
      "level": 1,
      "score": 0,
      "createdAt": "2026-04-11T00:00:00Z",
      "updatedAt": "2026-04-11T00:00:00Z"
    },
    "accessToken": "jwt",
    "refreshToken": "jwt",
    "isNewUser": true
  }
}
```

### POST `/auth/google`

**Request**

```json
{
  "idToken": "google-id-token"
}
```

**Response 200:** cùng format với `/auth/guest`.

### POST `/auth/refresh`

**Request**

```json
{
  "refreshToken": "refresh-token"
}
```

**Response 200:** cùng format với `/auth/guest`.

### POST `/auth/logout`

**Request**

```json
{
  "refreshToken": "refresh-token"
}
```

**Response 200**

```json
{
  "message": "Logged out successfully",
  "data": null
}
```

---

## 2) Users

### GET `/users/me/profile`

**Query params:** none

**Response 200**

```json
{
  "message": "Profile retrieved successfully",
  "data": {
    "id": "guid",
    "username": "string",
    "email": "string",
    "provider": "Google",
    "avatarUrl": "string",
    "bio": "string",
    "level": 5,
    "score": 420,
    "createdAt": "2026-04-11T00:00:00Z",
    "updatedAt": "2026-04-11T00:00:00Z"
  }
}
```

### PATCH `/users/me/profile`

**Request**

```json
{
  "username": "optional",
  "bio": "optional",
  "avatarUrl": "optional"
}
```

**Response 200:** cùng format data như `GET /users/me/profile`.

### GET `/users/me/stats`

**Query params:** none

**Response 200**

```json
{
  "message": "User stats retrieved successfully",
  "data": {
    "xp": 1200,
    "nextLevelXp": 2000,
    "score": 450,
    "level": 5,
    "highestScore": 700,
    "totalMatches": 42,
    "totalWins": 20,
    "winRate": 47.62,
    "globalRank": 12
  }
}
```

### GET `/users/{userId}`

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
    "relationship": "Accepted"
  }
}
```

### GET `/users/{userId}/stats`

**Path params**

- `userId` (guid)

**Response 200:** cùng format `GET /users/me/stats`.

### GET `/users`

**Query params**

- `searchQuery` (string, optional)
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
        "relationship": "None"
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

### GET `/users/leaderboard`

**Query params**

- `page` (int, optional, default 1)
- `pageSize` (int, optional, default 20)

**Response 200:** cùng format list user ở trên.

---

## 3) Friends

### GET `/me/friends`

**Query params**

- `status` (optional)
- `pagination.page` (int, optional)
- `pagination.pageSize` (int, optional)

**Response 200:** format list user profile.

### POST `/me/friends/invitations`

**Request**

```json
{
  "userId": "guid"
}
```

**Response 200**

```json
{
  "message": "Invitation sent successfully",
  "data": {
    "id": "guid",
    "username": "string",
    "avatarUrl": "string",
    "bio": "string",
    "level": 1,
    "score": 0,
    "relationship": "PendingSent"
  }
}
```

### PATCH `/me/friends/invitations/{requesterId}`

**Path params**

- `requesterId` (guid)

**Request**

```json
{
  "accept": true
}
```

**Response 200:** profile của user còn lại với relationship mới.

### DELETE `/me/friends/{friendId}`

**Path params**

- `friendId` (guid)

**Response 200**

```json
{
  "message": "Friend removed successfully",
  "data": null
}
```

---

## 4) Rooms

### POST `/rooms`

**Request**

```json
{
  "maxPlayers": 6,
  "isPublic": true,
  "cardSetIds": ["guid"]
}
```

**Response 201**

```json
{
  "message": "Room created successfully",
  "data": {
    "code": "ABC123",
    "hostId": "guid",
    "status": "waiting",
    "isPublic": true,
    "settings": {
      "maxPlayers": 6,
      "turnTimer": 15
    },
    "cardSets": [
      {
        "id": "guid",
        "name": "Base Set"
      }
    ],
    "currentParticipants": [
      {
        "userId": "guid",
        "nickname": "string",
        "avatarUrl": "string",
        "role": "player",
        "isReady": true
      }
    ],
    "connection": {
      "wsUrl": "wss://game.memesploding.com/ws",
      "wsAccessToken": "jwt"
    }
  }
}
```

### POST `/rooms/{code}/join`

**Path params**

- `code` (string)

**Request body:** none

**Response 200:** cùng format room detail ở trên.

### GET `/rooms`

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
        "code": "ABC123",
        "hostId": "guid",
        "hostNickname": "string",
        "maxPlayers": 6,
        "currentPlayers": 3,
        "status": "waiting",
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

### GET `/rooms/{code}`

**Path params**

- `code` (string)

**Response 200:** room detail giống `POST /rooms`.

---

## 5) Matchmaking

### POST `/matchmaking/quick-play`

**Request body:** none

**Response 200:** room detail (như `/rooms/{code}` hoặc `/rooms/{code}/join`).

---

## 6) Notifications

### GET `/me/notifications`

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
        "id": "guid",
        "type": "System",
        "payload": "{\"k\":\"v\"}",
        "isRead": false,
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

### GET `/me/notifications/unread-count`

**Query params:** none

**Response 200**

```json
{
  "message": "Successfully!",
  "data": {
    "count": 3
  }
}
```

### PATCH `/notifications/{id}`

**Path params**

- `id` (guid)

**Request body:** none

**Response 200:** trả về object notification đã cập nhật `isRead`.

### DELETE `/notifications/{id}`

**Path params**

- `id` (guid)

**Response 200**

```json
{
  "message": "Notification deleted successfully",
  "data": {}
}
```

### PATCH `/notifications/mark-all-read`

**Request body:** none

**Response 200**

```json
{
  "message": "All notifications marked as read",
  "data": {}
}
```

### DELETE `/notifications/clear-all`

**Response 200**

```json
{
  "message": "All notifications cleared successfully",
  "data": {}
}
```

---

## 7) Match history

### GET `/me/match-history`

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
        "matchId": "guid",
        "roomCode": "ABC123",
        "startedAt": "2026-04-11T00:00:00Z",
        "endedAt": "2026-04-11T00:30:00Z",
        "totalPlayers": 4,
        "finalRank": 1,
        "xpEarned": 100,
        "scoreChange": 20,
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

### GET `/users/{id}/match-history`

**Path params**

- `id` (guid)

**Query params**

- `page` (int, optional)
- `pageSize` (int, optional)

**Response 200:** cùng format với `/me/match-history`.

### GET `/matches/{matchId}`

**Path params**

- `matchId` (guid)

**Response 200**

```json
{
  "message": "Successfully!",
  "data": {
    "matchId": "guid",
    "roomCode": "ABC123",
    "startedAt": "2026-04-11T00:00:00Z",
    "endedAt": "2026-04-11T00:30:00Z",
    "winnerId": "guid",
    "cardSets": [{ "id": "guid", "name": "Base Set" }],
    "participants": [
      {
        "userId": "guid",
        "nickname": "string",
        "avatarUrl": "string",
        "finalRank": 1,
        "xpEarned": 100,
        "scoreChange": 20
      }
    ],
    "stats": {
      "totalTurns": 40,
      "totalCards": 120,
      "events": {}
    }
  }
}
```

---

## 8) Card sets

### GET `/card-sets`

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
        "id": "guid",
        "name": "Base Set",
        "description": "string",
        "cardCount": 56,
        "imageUrl": "https://...",
        "isActive": true,
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

### GET `/card-sets/{id}/cards`

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
        "id": "guid",
        "code": "SeeTheFuture",
        "name": "See The Future",
        "description": "string",
        "type": "Action",
        "imageUrl": "https://...",
        "iconUrl": "https://..."
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
