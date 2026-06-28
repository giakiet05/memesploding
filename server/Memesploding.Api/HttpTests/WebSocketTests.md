# WebSocket Testing Guide

## Prerequisites
- Server running with Redis
- Valid JWT token from login

## 1. Connection Test

### Connect to WebSocket
```bash
# Get JWT token first
TOKEN="your_jwt_token_here"

# Connect using wscat (install: npm install -g wscat)
wscat -c "ws://localhost:5000/ws?access_token=$TOKEN"
```

### Expected Response
```json
{
  "event": "friend_status",
  "data": {
    "user_id": "uuid",
    "username": "alice",
    "avatar_url": "https://...",
    "online": true,
    "activity": {
      "type": "idle"
    }
  },
  "timestamp": "2026-04-03T02:30:00Z"
}
```

## 2. Heartbeat Test

### Send Heartbeat
```json
{
  "action": "heartbeat"
}
```

### Expected: No response (refreshes presence TTL silently)

## 3. Presence Test

### Scenario: User connects → Friends see online status

1. User A connects → User B (friend) receives:
```json
{
  "event": "friend_status",
  "data": {
    "user_id": "userA_id",
    "username": "alice",
    "online": true,
    "activity": {
      "type": "idle"
    }
  }
}
```

2. User A disconnects → User B receives:
```json
{
  "event": "friend_status",
  "data": {
    "user_id": "userA_id",
    "online": false,
    "last_seen": "2026-04-03T02:35:00Z"
  }
}
```

## 4. Rich Presence Test

### Scenario: User A in room → Friend B sees room activity

**Setup Redis manually:**
```bash
redis-cli

# Simulate Game Server setting user A in room
SET room:player:{userA_id} '{"room_code":"ABC123","status":"waiting"}'
SET room:ABC123:info '{"is_public":true,"current_players":2,"max_players":4}'

# Trigger presence update
# (In real system, this happens via Pub/Sub from Game Server)
```

**Friend B receives:**
```json
{
  "event": "friend_status",
  "data": {
    "user_id": "userA_id",
    "username": "alice",
    "online": true,
    "activity": {
      "type": "in_room",
      "room": {
        "code": "ABC123",
        "is_public": true,
        "current_players": 2,
        "max_players": 4,
        "status": "waiting"
      }
    }
  }
}
```

## 5. Invitation Test

### Send Invitation
```json
{
  "action": "invite_to_room",
  "data": {
    "room_code": "ABC123",
    "friend_user_id": "friend_uuid"
  }
}
```

### Friend Receives
```json
{
  "event": "room_invitation",
  "data": {
    "invitation_id": "uuid",
    "room_code": "ABC123",
    "inviter": {
      "user_id": "inviter_uuid",
      "username": "alice",
      "avatar_url": "https://..."
    },
    "room_info": {
      "is_public": true,
      "current_players": 2,
      "max_players": 4,
      "card_sets": [...]
    },
    "expires_at": "2026-04-03T02:40:00Z"
  }
}
```

### Friend Responds (Accept)
```json
{
  "action": "respond_invitation",
  "data": {
    "invitation_id": "uuid",
    "accepted": true
  }
}
```

### Inviter Receives Response
```json
{
  "event": "invitation_response",
  "data": {
    "invitation_id": "uuid",
    "accepted": true,
    "invitee": {
      "user_id": "friend_uuid",
      "username": "bob"
    }
  }
}
```

## 6. Join Request Test

### Request Join Private Room
```json
{
  "action": "request_join_room",
  "data": {
    "room_code": "XYZ789"
  }
}
```

### All Room Members Receive
```json
{
  "event": "room_join_request",
  "data": {
    "request_id": "uuid",
    "room_code": "XYZ789",
    "requester": {
      "user_id": "requester_uuid",
      "username": "charlie",
      "avatar_url": "https://..."
    },
    "expires_at": "2026-04-03T02:40:00Z"
  }
}
```

### Member Responds (Accept)
```json
{
  "action": "respond_join_request",
  "data": {
    "request_id": "uuid",
    "accepted": true
  }
}
```

### Requester Receives Response
```json
{
  "event": "join_request_response",
  "data": {
    "request_id": "uuid",
    "room_code": "XYZ789",
    "accepted": true,
    "responder": {
      "user_id": "responder_uuid",
      "username": "alice"
    }
  }
}
```

## 7. Error Cases

### Error: Not in room
```json
{
  "action": "invite_to_room",
  "data": {
    "room_code": "INVALID",
    "friend_user_id": "uuid"
  }
}
```

**Response:**
```json
{
  "event": "error",
  "data": {
    "code": "NOT_IN_ROOM",
    "message": "You are not in a room"
  },
  "timestamp": "2026-04-03T02:30:00Z"
}
```

## 8. Redis Pub/Sub Test

### Simulate Game Server Publishing Room Update
```bash
redis-cli

PUBLISH room:updates '{
  "type": "room_status_changed",
  "room_code": "ABC123",
  "status": "playing",
  "is_public": true,
  "current_players": 4,
  "max_players": 4,
  "player_ids": ["uuid1", "uuid2", "uuid3", "uuid4"]
}'
```

### Expected: All friends of players receive updated friend_status with in_match activity

## Manual Testing Checklist

- [ ] Connect with valid JWT → Success
- [ ] Connect without JWT → Connection fails
- [ ] Heartbeat every 60s → Presence TTL refreshed
- [ ] User connects → Friends receive friend_status online
- [ ] User disconnects → Friends receive friend_status offline
- [ ] User in room → Friends see in_room activity
- [ ] User in match → Friends see in_match activity
- [ ] Invite friend → Friend receives room_invitation
- [ ] Accept invitation → Inviter receives invitation_response
- [ ] Decline invitation → Inviter receives invitation_response
- [ ] Request join private room → All members receive room_join_request
- [ ] Accept join request → Requester receives join_request_response
- [ ] Decline join request → Requester receives join_request_response
- [ ] Rate limit: Send invites too fast → Error RATE_LIMITED
- [ ] Invite non-friend → Error NOT_FRIENDS
- [ ] Invite when not in room → Error NOT_IN_ROOM
- [ ] Request join public room → Error ROOM_PUBLIC
- [ ] Multi-device: Connect 2 devices → Both receive messages
- [ ] Multi-device: Disconnect 1 → Still online
- [ ] Multi-device: Disconnect both → Offline

## Notes
- Invitation/Request expire after 5 minutes
- Presence expires after 10 minutes without heartbeat
- Rate limits: 5s for invites, 10s for join requests
- Multi-connection support: User stays online until ALL connections close
