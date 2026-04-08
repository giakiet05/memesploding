# WebSocket Manual Testing Guide

## Prerequisites

### Install wscat (Node.js WebSocket client)
```bash
npm install -g wscat
```

**OR** use websocat (Rust WebSocket client):
```bash
cargo install websocat
# or
sudo snap install websocat
```

### Get JWT Tokens

Run the integration test script to create users and get tokens:
```bash
./run-integration-tests.sh
```

Or register users manually and copy tokens from responses.

---

## Test Scenarios

### 1. Connection & Authentication

**Connect as Alice:**
```bash
wscat -c "ws://localhost:5217/ws?access_token=YOUR_ALICE_TOKEN_HERE"
```

**Expected:** Connection established successfully

**Invalid token (should fail):**
```bash
wscat -c "ws://localhost:5217/ws?access_token=INVALID"
```

**Expected:** Connection refused (401 Unauthorized)

---

### 2. Heartbeat

**After connecting, send:**
```json
{"action":"heartbeat"}
```

**Expected:** No response (heartbeat refreshes presence TTL silently)

**Auto-heartbeat script:**
```bash
# Send heartbeat every 60 seconds
while true; do 
  echo '{"action":"heartbeat"}' | websocat "ws://localhost:5217/ws?access_token=$TOKEN"
  sleep 60
done
```

---

### 3. Presence Broadcast

**Scenario:** Alice connects → Bob (friend) receives friend_status event

**Step 1: Bob connects first:**
```bash
wscat -c "ws://localhost:5217/ws?access_token=BOB_TOKEN"
```

**Step 2: Alice connects (in separate terminal):**
```bash
wscat -c "ws://localhost:5217/ws?access_token=ALICE_TOKEN"
```

**Expected (Bob receives):**
```json
{
  "event": "friend_status",
  "data": {
    "user_id": "alice_uuid",
    "username": "alice_integration",
    "avatar_url": null,
    "online": true,
    "activity": {
      "type": "idle"
    }
  },
  "timestamp": "2026-04-06T07:00:00Z"
}
```

**Step 3: Alice disconnects:**
```
Ctrl+C to disconnect
```

**Expected (Bob receives):**
```json
{
  "event": "friend_status",
  "data": {
    "user_id": "alice_uuid",
    "username": "alice_integration",
    "online": false,
    "last_seen": "2026-04-06T07:05:00Z",
    "activity": {
      "type": "idle"
    }
  },
  "timestamp": "2026-04-06T07:05:00Z"
}
```

---

### 4. Rich Presence (User in Room)

**Setup:** Simulate Game Server setting user in room (use Redis CLI)

```bash
redis-cli

# Set Alice in room
SET room:player:ALICE_USER_ID '{"room_code":"ABC123","status":"waiting"}'

# Set room info
SET room:ABC123:info '{"is_public":true,"current_players":2,"max_players":4}'
```

**Then Alice reconnects:**
```bash
wscat -c "ws://localhost:5217/ws?access_token=ALICE_TOKEN"
```

**Expected (Bob receives updated presence):**
```json
{
  "event": "friend_status",
  "data": {
    "user_id": "alice_uuid",
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

---

### 5. Room Invitation

**Prerequisites:**
1. Alice and Bob are friends
2. Alice creates a room (REST API: POST /rooms)
3. Get room code from response

**Alice (in room) invites Bob:**
```json
{
  "action": "invite_to_room",
  "data": {
    "room_code": "ABC123",
    "friend_user_id": "bob_user_id_here"
  }
}
```

**Expected (Bob receives):**
```json
{
  "event": "room_invitation",
  "data": {
    "invitation_id": "uuid",
    "room_code": "ABC123",
    "inviter": {
      "user_id": "alice_uuid",
      "username": "alice_integration",
      "avatar_url": null
    },
    "room_info": {
      "is_public": false,
      "current_players": 1,
      "max_players": 4,
      "card_sets": [
        {
          "id": "cardset_uuid",
          "name": "Exploding Kittens Original",
          "image_url": "https://..."
        }
      ]
    },
    "expires_at": "2026-04-06T07:10:00Z"
  },
  "timestamp": "2026-04-06T07:05:00Z"
}
```

**Bob accepts invitation:**
```json
{
  "action": "respond_invitation",
  "data": {
    "invitation_id": "invitation_uuid_from_above",
    "accepted": true
  }
}
```

**Expected (Alice receives):**
```json
{
  "event": "invitation_response",
  "data": {
    "invitation_id": "uuid",
    "accepted": true,
    "invitee": {
      "user_id": "bob_uuid",
      "username": "bob_integration"
    }
  },
  "timestamp": "2026-04-06T07:06:00Z"
}
```

**Bob declines invitation:**
```json
{
  "action": "respond_invitation",
  "data": {
    "invitation_id": "uuid",
    "accepted": false
  }
}
```

---

### 6. Join Request (Private Room)

**Prerequisites:**
1. Alice creates a PRIVATE room
2. Bob knows the room code (from friend, etc.)

**Bob requests to join:**
```json
{
  "action": "request_join_room",
  "data": {
    "room_code": "XYZ789"
  }
}
```

**Expected (ALL room members receive):**
```json
{
  "event": "room_join_request",
  "data": {
    "request_id": "uuid",
    "room_code": "XYZ789",
    "requester": {
      "user_id": "bob_uuid",
      "username": "bob_integration",
      "avatar_url": null
    },
    "expires_at": "2026-04-06T07:10:00Z"
  },
  "timestamp": "2026-04-06T07:05:00Z"
}
```

**Alice (or any member) accepts:**
```json
{
  "action": "respond_join_request",
  "data": {
    "request_id": "request_uuid_from_above",
    "accepted": true
  }
}
```

**Expected (Bob receives):**
```json
{
  "event": "join_request_response",
  "data": {
    "request_id": "uuid",
    "room_code": "XYZ789",
    "accepted": true,
    "responder": {
      "user_id": "alice_uuid",
      "username": "alice_integration"
    }
  },
  "timestamp": "2026-04-06T07:06:00Z"
}
```

**After acceptance, Bob must call REST API to actually join:**
```bash
curl -X POST http://localhost:5217/api/v1/rooms/XYZ789/join \
  -H "Authorization: Bearer $BOB_TOKEN"
```

---

### 7. Error Cases

**Error: Not in room (invite without being in room):**
```json
{
  "action": "invite_to_room",
  "data": {
    "room_code": "ABC123",
    "friend_user_id": "uuid"
  }
}
```

**Expected:**
```json
{
  "event": "error",
  "data": {
    "code": "NOT_IN_ROOM",
    "message": "You are not in a room"
  },
  "timestamp": "2026-04-06T07:05:00Z"
}
```

**Error: Not friends:**
```json
{
  "action": "invite_to_room",
  "data": {
    "room_code": "ABC123",
    "friend_user_id": "stranger_uuid"
  }
}
```

**Expected:**
```json
{
  "event": "error",
  "data": {
    "code": "NOT_FRIENDS",
    "message": "Target user is not your friend"
  }
}
```

**Error: Room full:**
```json
{
  "action": "invite_to_room",
  "data": {
    "room_code": "FULL_ROOM",
    "friend_user_id": "uuid"
  }
}
```

**Expected:**
```json
{
  "event": "error",
  "data": {
    "code": "ROOM_FULL",
    "message": "Room is full"
  }
}
```

**Error: Rate limited (send invites too fast):**

Send 2 invitations to same friend within 5 seconds.

**Expected:**
```json
{
  "event": "error",
  "data": {
    "code": "RATE_LIMITED",
    "message": "Please wait before sending another invitation"
  }
}
```

---

### 8. Multi-Device Test

**Scenario:** User connects from 2 devices, stays online until both disconnect

**Terminal 1 (Alice Device 1):**
```bash
wscat -c "ws://localhost:5217/ws?access_token=ALICE_TOKEN"
```

**Terminal 2 (Alice Device 2):**
```bash
wscat -c "ws://localhost:5217/ws?access_token=ALICE_TOKEN"
```

**Terminal 3 (Bob - watching):**
```bash
wscat -c "ws://localhost:5217/ws?access_token=BOB_TOKEN"
```

**Expected:**
- Bob receives ONE friend_status (Alice online) when first device connects
- Bob receives NO event when second device connects (Alice already online)
- Disconnect Terminal 1 → No event (still has device 2)
- Disconnect Terminal 2 → Bob receives friend_status (Alice offline)

---

## Testing Checklist

### Connection & Auth
- [ ] Connect with valid token → Success
- [ ] Connect with invalid token → Connection refused
- [ ] Connect without token → Connection refused

### Heartbeat
- [ ] Send heartbeat → No response (silent success)
- [ ] Presence TTL refreshed (check Redis: `TTL presence:USER_ID`)

### Presence Broadcast
- [ ] User connects → Friends receive friend_status (online)
- [ ] User disconnects → Friends receive friend_status (offline)
- [ ] User in room → Friends see in_room activity
- [ ] User in match → Friends see in_match activity

### Room Invitation
- [ ] Send invitation → Friend receives room_invitation
- [ ] Accept invitation → Inviter receives invitation_response
- [ ] Decline invitation → Inviter receives invitation_response
- [ ] Duplicate invitation → Error ALREADY_INVITED
- [ ] Invite non-friend → Error NOT_FRIENDS
- [ ] Invite when not in room → Error NOT_IN_ROOM

### Join Request
- [ ] Request join private room → All members receive room_join_request
- [ ] Accept request → Requester receives join_request_response
- [ ] Decline request → Requester receives join_request_response
- [ ] Request join public room → Error ROOM_PUBLIC
- [ ] Request join full room → Error ROOM_FULL

### Multi-Device
- [ ] Connect 2 devices → Stays online
- [ ] Disconnect 1 device → Still online
- [ ] Disconnect both → Offline

### Rate Limiting
- [ ] Send 2 invites within 5s → Error RATE_LIMITED
- [ ] Send 2 join requests within 10s → Error RATE_LIMITED

---

## Troubleshooting

### Connection fails
- Check server is running: `curl http://localhost:5217/api/v1/card-sets`
- Check JWT token is valid (not expired)
- Check Redis is running: `redis-cli ping`

### No events received
- Ensure users are friends (check: GET /me/friendships)
- Check presence in Redis: `redis-cli GET presence:USER_ID`
- Verify both users connected to WebSocket

### Invitation not working
- Verify inviter is in room: check `room:player:USER_ID` in Redis
- Verify room info cached: check `room:ROOM_CODE:info` in Redis
- Check room not full

### Room info not showing
- Game Server should publish to Redis Pub/Sub: `room:updates`
- API Server listens and caches room info
- Check RoomUpdateListenerService logs

---

## Advanced: Scripted Multi-Client Test

**Create test-presence.sh:**
```bash
#!/bin/bash

ALICE_TOKEN="..."
BOB_TOKEN="..."
WS_URL="ws://localhost:5217/ws"

# Terminal 1: Alice
websocat "$WS_URL?access_token=$ALICE_TOKEN" &
ALICE_PID=$!

sleep 2

# Terminal 2: Bob (should receive friend_status)
websocat "$WS_URL?access_token=$BOB_TOKEN" > bob-output.txt &
BOB_PID=$!

sleep 5

# Check output
if grep -q "friend_status" bob-output.txt; then
  echo "✅ Presence broadcast working"
else
  echo "❌ No presence event received"
fi

# Cleanup
kill $ALICE_PID $BOB_PID
```

---

## Summary

**Total WebSocket Actions:** 5
1. `heartbeat` - Refresh presence TTL
2. `invite_to_room` - Invite friend to current room
3. `respond_invitation` - Accept/decline invitation
4. `request_join_room` - Request to join private room
5. `respond_join_request` - Accept/decline join request

**Total Server Events:** 6
1. `friend_status` - Friend online/offline/activity updates
2. `room_invitation` - Received invitation
3. `invitation_response` - Invitation accepted/declined
4. `room_join_request` - Someone wants to join room
5. `join_request_response` - Join request accepted/declined
6. `error` - Error occurred

**Coverage:** All WebSocket features tested ✅
