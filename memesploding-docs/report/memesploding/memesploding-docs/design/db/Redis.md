# Redis Schema

## Naming Convention
`{entity}:{id}` hoặc `{entity}:{id}:{attribute}`

---

## 1. Room Management (Ephemeral Data)

### Room Metadata
| Key                             | Type | Value                                                               | Mục đích                         |
| ------------------------------- | ---- | ------------------------------------------------------------------- | -------------------------------- |
| `room:{room_code}`              | Hash | `{host_id, status, is_public, max_players, turn_timer, created_at}` | Metadata phòng                   |
| `room:{room_code}:participants` | Hash | `{user_id: {nickname, avatar_url, is_ready}}`                       | Danh sách members + ready status |
| `room:{room_code}:card_sets`    | Set  | `[card_set_uuid1, card_set_uuid2]`                                  | Bộ bài được chọn                 |

### Room Lifecycle
- **Create:** Host gọi `POST /rooms` → Redis HSET `room:{code}`
- **Update:** Host gọi `PATCH /rooms/:code` → Redis HSET (update fields)
- **Member Join:** Gọi `POST /rooms/:code/join` → Redis HSET `room:{code}:participants`
- **Ready Status:** Gọi `PATCH /rooms/:code/participants/me` → Redis HSET `room:{code}:participants:{user_id}:is_ready`
- **Dissolve:** Host gọi `DELETE /rooms/:code` → Redis DEL (toàn bộ room keys)

**Example Data:**
```
room:ABC123 = {
  "host_id": "user-uuid-1",
  "status": "waiting",
  "is_public": true,
  "max_players": 6,
  "turn_timer": 15,
  "created_at": "2026-03-19T13:55:00Z"
}

room:ABC123:participants = {
  "user-uuid-1": {
    "nickname": "Alice",
    "avatar_url": "https://...",
    "is_ready": false
  },
  "user-uuid-2": {
    "nickname": "Bob",
    "avatar_url": "https://...",
    "is_ready": true
  }
}

room:ABC123:card_sets = ["uuid-baseset", "uuid-expansion1"]
```

---

## Notes
- Không sử dụng TTL - chỉ xóa khi host giải tán hoặc toàn bộ members rời phòng
- Participants dùng Hash để dễ update từng member (HSET/HDEL)
- Card sets dùng Set vì chỉ cần list, không cần value
- Auto-delete room keys khi participants = empty (triggered by leave/kick operations)
