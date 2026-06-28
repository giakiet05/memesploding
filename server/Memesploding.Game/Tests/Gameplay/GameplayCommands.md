# Memesploding Gameplay WebSocket Guide

Dùng tài liệu này để copy payload vào các GUI WebSocket Client (như WebSocketKing) hoặc để tự động hóa với curl + websocat.

## 🔗 Connection Info

**Từ file GameplaySetup.http:**

- **Lấy WS URL**: Sau khi chạy "Get Ticket" requests, sử dụng variables: `{{aliceWsUrl}}`, `{{bobWsUrl}}`, `{{charlieWsUrl}}`
- **Hoặc manual**: `ws://localhost:5217/ws?access_token={GAME_TICKET}`
- **Production**: `wss://api.memesploding.com/ws?access_token={GAME_TICKET}`

---

## 🛠️ Step 1: Handshake (BẮT BUỘC)

Sau khi kết nối thành công, mày PHẢI gửi message này đầu tiên để Server hiểu mày dùng giao thức JSON.

**Payload:** (Nhớ thêm ký tự `\x1e` ở cuối nếu tool không tự thêm)

```json
{ "protocol": "json", "version": 1 }
```

Server sẽ trả về `{}` nếu ok.

---

## 🃏 Step 2: Game Actions

Tất cả các lệnh game đều được gửi qua target `SendCommand`.

### 1. Bốc bài (Draw Card)

Dùng khi đến lượt của mày.

```json
{
  "type": 1,
  "target": "SendCommand",
  "arguments": [{ "Event": "drawcard", "Data": {} }]
}
```

### 2. Đánh một thẻ bài (Play Card)

Thay `cardCode` bằng mã thẻ mày muốn đánh (ví dụ: `Skip`, `Shuffle`, `SeeTheFuture`, `Attack`).

```json
{
  "type": 1,
  "target": "SendCommand",
  "arguments": [{ "Event": "playcard", "Data": { "cardCode": "Skip" } }]
}
```

### 3. Đánh thẻ bài có mục tiêu (Play Card with Target)

Ví dụ: `Favor`, `TargetedAttack`.

```json
{
  "type": 1,
  "target": "SendCommand",
  "arguments": [
    {
      "Event": "playcard",
      "Data": { "cardCode": "Favor", "targetUserId": "UUID_CUA_DANG_CHOI" }
    }
  ]
}
```

### 4. Dùng Nope (Để chặn đứa khác)

Chỉ dùng được trong cửa sổ `ReactionWindow`.

```json
{
  "type": 1,
  "target": "SendCommand",
  "arguments": [{ "Event": "nope", "Data": {} }]
}
```

### 5. Gỡ bom (Use Defuse)

Dùng khi mày vừa bốc phải `ExplodingKitten`.

```json
{
  "type": 1,
  "target": "SendCommand",
  "arguments": [{ "Event": "usedefuse", "Data": {} }]
}
```

### 6. Chọn vị trí nhét bom vào lại (Choose Bomb Position)

Sau khi Defuse, mày phải chọn vị trí nhét bom vào Deck (0 là trên cùng).

```json
{
  "type": 1,
  "target": "SendCommand",
  "arguments": [
    { "Event": "choosebombinsertposition", "Data": { "position": 3 } }
  ]
}
```

---

## 🧐 Step 3: Quản lý trạng thái

### Request State Snapshot

Nếu mày muốn xem lại toàn bộ trạng thái trận đấu (bài trên tay, ai còn sống...).

```json
{
  "type": 1,
  "target": "SendCommand",
  "arguments": [{ "Event": "requeststatesnapshot", "Data": {} }]
}
```

### Heartbeat (Giữ kết nối)

Gửi định kỳ nếu bị idle timeout.

```json
{
  "type": 1,
  "target": "SendCommand",
  "arguments": [{ "Event": "heartbeat", "Data": {} }]
}
```

---

## ⚠️ Lưu ý về SignalR Protocol

- Mọi message gửi đi đều phải có hậu tố `\x1e` (ký tự ASCII 30 / 0x1E).
- Server gửi về cũng sẽ có ký tự này ở cuối mỗi message JSON.
- Các sự kiện server gửi về (như `card_drawn`, `turn_started`, `explosion_triggered`) sẽ nằm trong target `ReceiveMessage`.

**Cấu trúc message server gửi mày:**

```json
{
  "type": 1,
  "target": "ReceiveMessage",
  "arguments": [
    {
      "event": "tên_sự_kiện",
      "data": { ... dữ liệu ... },
      "timestamp": "..."
    }
  ]
}
```

---

## 🤖 Tự động hóa với websocat + bash

### Install websocat:

```bash
# Option 1: Snap (recommend)
sudo snap install websocat

# Option 2: Cargo
cargo install websocat

# Option 3: Pre-built binary
wget https://github.com/vi/websocat/releases/download/v1.12.0/websocat.x86_64-unknown-linux-musl
chmod +x websocat && sudo mv websocat /usr/local/bin/
```

### Example: Auto draw card + check state

```bash
#!/bin/bash

# 1. Lấy WS URL từ GameplaySetup.http (chạy request GetAliceTicket trước)
ALICE_TOKEN="YOUR_TOKEN_HERE"
WS_URL="ws://localhost:5217/ws?access_token=$ALICE_TOKEN"

# 2. Handshake
echo '{"protocol":"json","version":1}' | websocat "$WS_URL"

# 3. Draw card
echo '{"type":1,"target":"SendCommand","arguments":[{"Event":"drawcard","Data":{}}]}' | websocat "$WS_URL"

# 4. Xem trạng thái
echo '{"type":1,"target":"SendCommand","arguments":[{"Event":"requeststatesnapshot","Data":{}}]}' | websocat "$WS_URL"
```

---

## 📚 Next Steps

Xem file `run-gameplay-tests.sh` để chạy toàn bộ flow tự động:

- Login (3 users)
- Tạo room
- Join room
- Set ready
- Start match
- Play game actions via WebSocket
