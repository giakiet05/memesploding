# Game Server WebSocket Specification (Match Gameplay)

Tài liệu này đặc tả các kết nối và message của Game Server. Đây là nơi xử lý toàn bộ logic trong trận đấu.

- **URL**: `wss://game.memesploding.com/ws`
- **Giao thức**: SignalR (JSON)
- **Token**: Truyền qua query string: `?access_token=<wsAccessToken>` (nhận từ API Server khi trận đấu bắt đầu).

---

## 1. Cơ chế SignalR (Kỹ thuật)

Nếu client **không dùng** thư viện SignalR chuẩn (ví dụ dùng WebSocket thô trong Unity), bạn cần tuân thủ các quy tắc sau:

1. **Handshake**: Ngay sau khi kết nối thành công, client **BẮT BUỘC** gửi message mở đầu:
   ```json
   { "protocol": "json", "version": 1 }
   ```
   Kèm theo ký tự đặc biệt `\u001e` (Record Separator - mã hex `0x1E`) ở cuối.

2. **Ký tự kết thúc**: Mọi message (cả gửi và nhận) đều phải có ký tự `\u001e` ở cuối cùng. Nếu thiếu, server sẽ không xử lý message.

3. **Packaging (Gửi lệnh)**: Lệnh gửi đi phải bọc trong object SignalR:
   ```json
   {
     "type": 1,
     "target": "SendCommand",
     "arguments": [
       {
         "event": "CommandName",
         "data": { ... }
       }
     ]
   }
   ```

---

## 2. Định dạng Message chuẩn (Server -> Client)

Mọi message server gửi về đều được bọc trong hàm `ReceiveMessage` của client với cấu trúc JSON sau:

```json
{
  "event": "EventName", // Tên sự kiện (vd: StateSnapshot, Connected) - PascalCase
  "data": { ... }, // Dữ liệu đi kèm (Payload)
  "timestamp": "2026-04-25T15:00:00Z" // Thời gian tạo sự kiện (ISO 8601)
}
```

---

## 3. Error response format (ActionRejected)

Khi một lệnh từ client bị từ chối do sai logic hoặc sai lượt, server sẽ bắn về sự kiện `ActionRejected` cho chính client đó:

```json
{
  "event": "ActionRejected",
  "data": {
    "reason": "REASON_CODE", // Mã lỗi quy chuẩn (In hoa)
    "userId": "guid", // ID của người chơi thực hiện hành động bị lỗi
    "message": "Câu thông báo chi tiết (nếu có)" 
  },
  "timestamp": "2026-04-25T15:00:00Z"
}
```

---

## 4. Client -> Server Commands

Để gửi lệnh, client gọi hàm `SendCommand` của SignalR. Tên lệnh (`event`) có thể gửi dưới dạng `PascalCase` hoặc `lowercase`.

### 4.1. `DrawCard`

Bốc lá bài trên cùng của xấp bài rút. Hành động này sẽ kết thúc lượt chơi của bạn.

**Client request**
```json
{
  "event": "DrawCard",
  "data": {}
}
```

**Các lỗi có thể gặp:**
- `NOT_TURN`, `INVALID_PHASE`, `REACTION_IN_PROGRESS`, `BOMB_RESOLUTION_PENDING`

---

### 4.2. `DrawFromBottom`

Bốc lá bài dưới cùng của xấp bài rút (thường do hiệu ứng thẻ bài đặc biệt).

**Client request**
```json
{
  "event": "DrawFromBottom",
  "data": {}
}
```

**Các lỗi có thể gặp:**
- Tương tự như `DrawCard`.

---

### 4.3. `PlayCard`

Đánh một lá bài hoặc thực hiện một combo thẻ bài từ trên tay.

**Client request (Đánh 1 lá bình thường)**
```json
{
  "event": "PlayCard",
  "data": {
    "cardCode": "Skip", // Mã thẻ bài (vd: Skip, Attack, Favor...)
    "targetUserId": "guid" // ID mục tiêu (bắt buộc với các lá như Favor)
  }
}
```

**Client request (Đánh Combo 2 lá - Trộm ngẫu nhiên)**
```json
{
  "event": "PlayCard",
  "data": {
    "comboSize": 2, // Khai báo combo 2 lá
    "cardCode": "Cat1", // Mã của cặp bài giống nhau
    "targetUserId": "guid" // ID mục tiêu muốn trộm bài
  }
}
```

**Client request (Đánh Combo 3 lá - Đòi đích danh)**
```json
{
  "event": "PlayCard",
  "data": {
    "comboSize": 3, // Khai báo combo 3 lá
    "cardCode": "Cat2", // Mã của 3 lá giống nhau
    "targetUserId": "guid", // ID mục tiêu
    "requestedCardCode": "Defuse" // Mã lá bài bạn muốn lấy
  }
}
```

**Client request (Đánh Combo 5 lá - Nhặt từ Discard Pile)**
```json
{
  "event": "PlayCard",
  "data": {
    "comboSize": 5, // Khai báo combo 5 lá
    "cardCodes": ["Skip", "Cat1", "Attack", "Favor", "Defuse"], // Mảng 5 lá KHÁC NHAU trên tay
    "discardCardCode": "Attack" // Mã lá bài muốn lấy lại từ xấp bài bỏ (Discard Pile)
  }
}
```

**Các lỗi có thể gặp:**
- `NOT_TURN`, `INVALID_PHASE`, `CARD_NOT_OWNED`, `INVALID_TARGET`, `REACTION_IN_PROGRESS`, `BOMB_RESOLUTION_PENDING`, `INSUFFICIENT_COMBO_CARDS`, `COMBO5_REQUIRES_5_DISTINCT_CARDS`, `MISSING_REQUESTED_CARD`

---

### 4.4. `Nope`

Sử dụng lá bài Nope để chặn đứng hành động vừa được đánh ra của đối thủ.

**Client request**
```json
{
  "event": "Nope",
  "data": {}
}
```

**Các lỗi có thể gặp:**
- `NO_REACTION_WINDOW`, `CARD_NOT_OWNED`, `INVALID_PHASE`

---

### 4.5. `UseDefuse`

Sử dụng lá bài Gỡ Bom (Defuse) ngay sau khi bạn vừa bốc phải lá Mèo Nổ.

**Client request**
```json
{
  "event": "UseDefuse",
  "data": {}
}
```

**Các lỗi có thể gặp:**
- `NOT_BOMB_RESOLUTION`, `CARD_NOT_OWNED`

---

### 4.6. `ChooseBombInsertPosition`

Chọn vị trí để đặt lại lá bài Mèo Nổ vào chồng bài rút (sau khi đã dùng Defuse thành công).

**Client request**
```json
{
  "event": "ChooseBombInsertPosition",
  "data": {
    "position": 0 // 0 là trên cùng, -1 là dưới cùng
  }
}
```

**Các lỗi có thể gặp:**
- `NOT_BOMB_RESOLUTION`, `INVALID_POSITION`

---

### 4.7. `ChooseFavorCard`

Chọn một lá bài từ tay mình để đưa cho người chơi vừa dùng lá bài Favor lên bạn.

**Client request**
```json
{
  "event": "ChooseFavorCard",
  "data": {
    "cardCode": "Skip" // Mã lá bài bạn muốn "tặng" đối thủ
  }
}
```

**Các lỗi có thể gặp:**
- `NOT_FAVOR_RESOLUTION`, `CARD_NOT_OWNED`

---

### 4.8. `ReconnectMatch`

Yêu cầu server gửi lại thông tin trận đấu khi bạn vừa kết nối lại sau khi rớt mạng.

**Client request**
```json
{
  "event": "ReconnectMatch",
  "data": {}
}
```

---

### 4.9. `RequestStateSnapshot`

Chủ động yêu cầu server gửi lại toàn bộ trạng thái bàn chơi hiện tại.

**Client request**
```json
{
  "event": "RequestStateSnapshot",
  "data": {}
}
```

---

### 4.10. `RequestServerTime`

Yêu cầu Game Server trả về thời gian hiện tại của chính Game Server. Dùng event này để client ước lượng lệch giờ giữa máy client và Game Server khi render countdown như `turnEndsAt`, `reactionWindowEndsAt`, `defuseWindowEndsAt`.

Event này không thay đổi game state, không tạo `Ack`, và server chỉ trả kết quả cho chính connection đã gửi request.

**Client request**
```json
{
  "event": "RequestServerTime",
  "data": {
    "clientSentAtMs": 1788888888000
  }
}
```

`clientSentAtMs` là Unix time milliseconds theo đồng hồ local của client tại thời điểm gửi message. Trường này optional nhưng nên gửi để client tính RTT/offset chính xác hơn.

**Server push: `ServerTime`**
```json
{
  "event": "ServerTime",
  "data": {
    "serverTimeUtc": "2026-04-25T15:00:00.123Z",
    "serverUnixTimeMs": 1788888888123,
    "clientSentAtMs": 1788888888000
  },
  "timestamp": "2026-04-25T15:00:00.123Z"
}
```

**Công thức client ước lượng server time**
```text
clientSentAtMs = data.clientSentAtMs
clientReceivedAtMs = local Unix time milliseconds lúc nhận ServerTime

rttMs = clientReceivedAtMs - clientSentAtMs
oneWayDelayMs = rttMs / 2

estimatedServerNowMs = data.serverUnixTimeMs + oneWayDelayMs
serverOffsetMs = estimatedServerNowMs - clientReceivedAtMs
```

Ý nghĩa:
- `rttMs`: tổng thời gian message đi từ client tới server rồi quay lại client.
- `oneWayDelayMs`: độ trễ một chiều ước lượng. Giả định đường đi và đường về gần tương đương.
- `serverOffsetMs`: độ lệch giữa đồng hồ client và Game Server. Client nên cache giá trị này.

Sau đó khi render countdown từ timestamp server gửi trong snapshot/event như `turnEndsAt`, `reactionWindowEndsAt`, `defuseWindowEndsAt`:

```text
serverNowApproxMs = localNowMs + serverOffsetMs
remainingMs = serverEndsAtMs - serverNowApproxMs
remainingMs = max(0, remainingMs)
```

Ví dụ:
```text
clientSentAtMs = 1000
serverUnixTimeMs = 5050
clientReceivedAtMs = 1200

rttMs = 200
oneWayDelayMs = 100
estimatedServerNowMs = 5150
serverOffsetMs = 5150 - 1200 = 3950

localNowMs = 2000
serverNowApproxMs = 2000 + 3950 = 5950
```

Nếu client không gửi `clientSentAtMs`, server vẫn trả `ServerTime` nhưng client không tính RTT được. Khi đó chỉ nên dùng `serverUnixTimeMs - clientReceivedAtMs` làm offset tạm, độ chính xác thấp hơn vì chưa bù network delay.

Client nên gọi `RequestServerTime` khi vừa connect Game WS, sau reconnect, và có thể ping lại định kỳ nếu thấy drift lớn. Không cần gọi liên tục mỗi frame. Nếu muốn ổn định hơn, client có thể gọi 3-5 lần, bỏ mẫu có `rttMs` cao bất thường, rồi lấy offset trung bình hoặc median.

---

### 4.11. `AckStateVersion`

Xác nhận với server rằng client đã nhận và xử lý thành công phiên bản trạng thái (State Version) cụ thể.

**Client request**
```json
{
  "event": "AckStateVersion",
  "data": {
    "version": 123
  }
}
```

---

## 5. Server -> Client Events

Hành động đẩy từ Server về Client qua hàm `ReceiveMessage`. Các tên sự kiện đều dùng **PascalCase**.

### 5.1. `Connected`

Gửi về ngay khi bạn nối WebSocket thành công vào trận đấu.

**Success push**
```json
{
  "event": "Connected",
  "data": {
    "roomCode": "ABC123", // Mã phòng chơi
    "matchId": "guid" // ID duy nhất của trận đấu
  }
}
```

---

### 5.2. `Ack`

Server xác nhận một lệnh của bạn gửi lên là hợp lệ và đã được thực thi.

**Success push**
```json
{
  "event": "Ack",
  "data": {
    "stateVersion": 124 // Phiên bản trạng thái mới nhất sau khi áp dụng lệnh của bạn
  }
}
```

---

### 5.3. `StateSnapshot`

Sự kiện quan trọng nhất, cung cấp toàn bộ dữ liệu bàn chơi để client vẽ giao diện.

**Success push**
```json
{
  "event": "StateSnapshot",
  "data": {
    "phase": "Playing", // Giai đoạn: "Initializing", "Playing", "Ended"
    "players": [
      {
        "userId": "guid", // ID người chơi
        "nickname": "string", // Tên hiển thị
        "avatarUrl": "string", // Ảnh đại diện
        "role": "player", // Vai trò: "host" hoặc "player"
        "lifeState": "Alive", // Tình trạng: "Alive" (Còn sống), "Dead" (Đã nổ), "Disconnected" (Mất mạng)
        "handCount": 5 // Số bài trên tay
      }
    ],
    "selfHand": ["Skip", "Defuse"], // Danh sách lá bài cụ thể TRÊN TAY BẠN
    "drawPileCount": 42, // Số lượng thẻ còn lại ở chồng bài rút
    "discardPile": ["Attack", "Favor"], // Danh sách mã thẻ bài đã đánh ra ở xấp bài bỏ
    "turnIndex": 0, // Vị trí (Index) người chơi đang giữ lượt trong danh sách players
    "turnEndsAt": "ISO_TIMESTAMP", // Thời điểm hết lượt bốc bài (chuẩn ISO 8601)
    "stateVersion": 123 // Số phiên bản trạng thái hiện tại (dùng để đồng bộ)
  }
}
```

---

### 5.4. `GameplayEvent`

Sự kiện thông báo các diễn biến cụ thể từng bước trong game (thường dùng để chạy animation).

**Cấu trúc chung**:
```json
{
  "event": "GameplayEvent",
  "data": {
    "type": "EventName", // Tên sự kiện (PascalCase)
    "payload": "string", // Chuỗi JSON chi tiết (cần JSON.parse)
    "stateVersion": 125
  }
}
```

**Danh sách sự kiện và Payload (Bộ Gốc - Original Set)**:

| Loại (`type`)           | Payload (JSON sau khi parse)                                    | Mô tả                                                                |
| :---------------------- | :-------------------------------------------------------------- | :------------------------------------------------------------------- |
| **Diễn biến chính**     |                                                                 |                                                                      |
| `MatchStarted`          | `{}`                                                            | Trận đấu chính thức bắt đầu.                                         |
| `TurnStarted`           | `{"turnIndex": 0}`                                              | Bắt đầu lượt của người chơi mới.                                     |
| `TurnChanged`           | `{"turnIndex": 1}`                                              | Chuyển lượt sang người tiếp theo.                                    |
| `TurnContinues`         | `{"userId": "guid", "pendingDrawCount": 1}`                     | Vẫn là lượt người đó (vd: sau khi bốc 1 lá trong 2 lượt của Attack). |
| `CardDrawn`             | `{"userId": "guid", "cardCode": "Skip"}`                        | Có người bốc bài (cardCode là `"Hidden"` nếu không phải bạn).        |
| `CardPlayed`            | `{"userId": "guid", "cardCode": "Skip"}`                        | Có người đánh 1 lá bài.                                              |
| `ComboPlayed`           | `{"userId": "guid", "comboSize": 2, "cardCode": "Cat1"}`        | Có người đánh combo nhiều lá.                                        |
| `ExplosionTriggered`    | `{"userId": "guid"}`                                            | Bom nổ (người chơi bốc phải bom).                                    |
| `DefuseUsed`            | `{"userId": "guid"}`                                            | Người chơi đã sử dụng lá Defuse thành công.                          |
| `BombReinserted`        | `{"userId": "guid", "position": 5}`                             | Bom đã được giấu lại vào bộ bài.                                     |
| `BombReinsertAuto`      | `{}`                                                            | Server tự nhét lại bom do người chơi hết giờ.                        |
| `PlayerEliminated`      | `{"userId": "guid", "reason": "BOMB_EXPLODED"}`                 | Người chơi bị loại (nổ bom hoặc AFK).                                |
| `MatchFinished`         | `{"winnerUserId": "guid"}`                                      | Trận đấu kết thúc, tìm ra người thắng.                               |
| **Hiệu ứng bài**        |                                                                 |                                                                      |
| `AttackApplied`         | `{"from": "guid", "to": "guid", "added": 2}`                    | Áp dụng hiệu ứng Tấn công.                                           |
| `SkipApplied`           | `{"userId": "guid"}`                                            | Áp dụng hiệu ứng Bỏ lượt.                                            |
| `ShuffleApplied`        | `{}`                                                            | Bộ bài đã được xáo trộn.                                             |
| `FuturePeeked`          | `["Skip", "Defuse", "Attack"]`                                  | Kết quả của See The Future (chỉ gửi cho người đánh).                 |
| `FavorWindowOpened`     | `{"requesterId": "guid", "targetId": "guid"}`                   | Mở cửa sổ chờ nạn nhân tặng bài.                                     |
| `FavorResolved`         | `{"from": "guid", "to": "guid", "cardCode": "Skip"}`            | Hoàn tất việc tặng bài (cardCode ẩn với người khác).                 |
| `FavorTargetEmpty`      | `{"from": "guid", "target": "guid"}`                            | Nạn nhân không có bài để tặng.                                       |
| `ReactionWindowOpened`  | `{"userId": "guid", "cardCode": "Attack"}`                      | Mở 5s chờ người khác Nope.                                           |
| `ReactionWindowClosed`  | `{"cardCode": "Attack", "nopeCount": 2}`                        | Kết thúc 5s, tính toán số Nope để thực thi/hủy lệnh.                 |
| **Combo Resolution**    |                                                                 |                                                                      |
| `CatComboTwoResolved`   | `{"from": "guid", "to": "guid", "cardCode": "Skip"}`            | Combo 2: Trộm bài thành công.                                        |
| `CatComboThreeResolved` | `{"from": "guid", "to": "guid", "requestedCardCode": "Defuse"}` | Combo 3: Đòi bài và lấy được.                                        |
| `CatComboThreeMiss`     | `{"from": "guid", "to": "guid", "requestedCardCode": "Defuse"}` | Combo 3: Đòi bài nhưng nạn nhân không có.                            |
| `CatComboFiveResolved`  | `{"userId": "guid", "discardCardCode": "Defuse"}`               | Combo 5: Lấy lại bài từ xấp bài bỏ thành công.                       |

**Giải thích ý nghĩa các trường (fields) thường gặp trong Payload:**
- `userId`: ID của người chơi vừa thực hiện hành động hoặc chịu tác động chính của sự kiện.
- `cardCode`: Mã định danh của thẻ bài (vd: `Skip`, `Attack`, `Cat1`...). Nếu lá bài được giấu hoặc không được phép nhìn thấy, giá trị này có thể là `"Hidden"`.
- `comboSize`: Số lượng lá bài được dùng để tạo thành một combo (vd: `2`, `3`, `5`).
- `turnIndex`: Số thứ tự của người đang đến lượt (so khớp với index trong mảng `players` của `StateSnapshot`).
- `from`: ID của người chơi chủ động gây ra hành động (vd: người tấn công, người xin bài).
- `to` / `target`: ID của người chơi là mục tiêu hoặc nạn nhân của hành động.
- `added`: Số lượng (vd: số lượt bị cộng thêm do lá Attack).
- `position`: Vị trí cụ thể (vd: thứ tự chèn lại quả bom vào chồng bài rút, tính từ trên xuống).
- `reason`: Mã lỗi hoặc lý do dẫn đến một hành động bắt buộc (vd: `BOMB_EXPLODED`).
- `winnerUserId`: ID của người chiến thắng cuối cùng của trận đấu.
- `requestedCardCode`: Mã thẻ bài cụ thể mà người dùng Combo 3 lá yêu cầu từ đối thủ.
- `discardCardCode`: Mã thẻ bài mà người dùng Combo 5 lá vớt lại từ xấp bài bỏ (Discard Pile).
- `nopeCount`: Tổng số lá Nope đã được tung ra trong một cửa sổ phản ứng.

---

## 6. Danh sách Mã lỗi Rejected quy chuẩn (Game)

| Reason | Ý nghĩa |
| :--- | :--- |
| `NOT_TURN` | Đang không phải là lượt của bạn. |
| `INVALID_PHASE` | Game chưa bắt đầu hoặc đã kết thúc. |
| `CARD_NOT_OWNED` | Bạn không cầm lá bài này trên tay. |
| `INVALID_TARGET` | Mục tiêu của lá bài không hợp lệ. |
| `REACTION_IN_PROGRESS` | Đang chờ Nope, không được bốc/đánh bài mới. |
| `NO_REACTION_WINDOW` | Thời gian đánh Nope đã hết hoặc không có gì để Nope. |
| `BOMB_RESOLUTION_PENDING` | Bạn đang vướng Bom, phải xử lý Bom trước. |
| `NOT_BOMB_RESOLUTION` | Hành động gỡ/nhét bom khi không bốc phải bom. |
| `INVALID_POSITION` | Vị trí nhét bom không hợp lệ. |
| `NOT_FAVOR_RESOLUTION` | Gửi bài tặng khi không có ai dùng bài Favor lên bạn. |
| `PLAYER_ELIMINATED` | Bạn đã bị loại khỏi ván đấu. |
| `INSUFFICIENT_COMBO_CARDS` | Số lượng lá bài giống nhau không đủ để đánh combo. |
| `COMBO5_REQUIRES_5_DISTINCT_CARDS` | Combo 5 lá yêu cầu 5 lá bài hoàn toàn khác nhau. |
| `MISSING_REQUESTED_CARD` | Thiếu trường thông tin `requestedCardCode` khi đánh Combo 3 lá. |

---

## 7. Danh sách Thẻ bài (Bộ Gốc - Original Set)

Dưới đây là danh sách `cardCode` và hiệu ứng của các lá bài trong bộ cơ bản.

| cardCode          | Tên lá bài     | Hiệu ứng                                                                                                                      |
| :---------------- | :------------- | :---------------------------------------------------------------------------------------------------------------------------- |
| `ExplodingKitten` | Mèo Nổ         | Rút phải lá này mà không có Defuse -> Thua ngay lập tức.                                                                      |
| `Defuse`          | Vô Hiệu Hoá    | Ngăn nổ bom. Cho phép nhét lại Mèo Nổ vào vị trí bất kỳ trong bộ bài.                                                         |
| `Skip`            | Bỏ Lượt        | Kết thúc lượt của bạn ngay lập tức mà không cần rút bài.                                                                      |
| `Attack`          | Tấn Công       | Kết thúc lượt của bạn. Người tiếp theo phải chơi 2 lượt liên tiếp.                                                            |
| `Favor`           | Xin Ơn         | Chọn một người chơi, họ phải tự chọn 1 lá bài đưa cho bạn.                                                                    |
| `Shuffle`         | Xáo Trộn       | Xáo trộn lại toàn bộ xấp bài rút.                                                                                             |
| `SeeTheFuture`    | Thấy Tương Lai | Xem 3 lá bài trên cùng của xấp bài rút.                                                                                       |
| `Nope`            | Không Đâu      | Chặn bất kỳ hành động nào (trừ Mèo Nổ và Defuse).                                                                             |
| `Cat1` -> `Cat5`  | Các lá Mèo     | Không có hiệu ứng riêng. Dùng theo cặp (Combo 2) để lấy bài ngẫu nhiên, hoặc bộ 3 (Combo 3) để chỉ định lá bài từ người khác. |

---

## 8. Cơ chế Tự động (Timeouts)

- **Turn Timer** (15s): Tự động bốc bài.
- **Defuse Window** (6s): Tự động nổ bom và bị loại.
- **Nope Window** (5s): Tự động kết thúc chờ phản ứng.
- **Bomb Reinsert Window** (10s): Tự nhét Bom vào vị trí ngẫu nhiên.
- **Favor Window** (10s): Tự bốc bài đưa cho đối thủ.
