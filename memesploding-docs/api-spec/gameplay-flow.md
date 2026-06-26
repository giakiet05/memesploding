# Memesploding Gameplay Flow Specification

Tài liệu này mô tả luồng vận hành chi tiết của một ván đấu Memesploding từ góc nhìn của Client. Nó hướng dẫn Frontend cách tổ chức State, xử lý tuần tự các Action, Window, Combo và cách chuyển giao mượt mà giữa API Server và Game Server.

---

## 1. Tổng quan các giai đoạn (Match Lifecycle)

Một ván đấu trải qua 4 giai đoạn chính, liên quan đến 2 kênh WebSocket khác nhau:

1. **Lobby (Phòng chờ)**: Quản lý bởi `AppHub` (API Server).
2. **Handover (Chuyển giao)**: Bước "bắt tay" lấy Ticket và chuyển sang `GameHub`.
3. **Gameplay (Trong trận)**: Quản lý bởi `GameHub` (Game Server).
4. **Conclusion (Kết thúc)**: Lưu kết quả từ Game Server sang Database và AppHub báo cho client.

---

## 2. Giai đoạn 1: Lobby (Chuẩn bị)

- **Hành động**: Người chơi tạo phòng (`POST /rooms`) hoặc vào phòng (`POST /rooms/{code}/join`).
- **WebSocket**: Client kết nối tới `wss://api.memesploding.com/api/v1/ws` (AppHub) bằng JWT Token (chuẩn bị trước khi vào phòng hoặc ngay khi vào).
- **Tương tác trong phòng**:
  - Gửi lệnh `SetReadyStatus` (true/false) để báo danh.
  - Lắng nghe các event `RoomMemberJoined`, `RoomMemberLeft`, `RoomReadyStatusChanged` để update giao diện phòng chờ theo thời gian thực.
- **Điều kiện bắt đầu**: Khi phòng có >= 2 người và tất cả (trừ host) đã `isReady: true`, Chủ phòng (Host) có quyền gọi lệnh `StartRoomMatch`.

### 2.1. Bot Test Mode

Client có thể bỏ qua lobby để vào nhanh một trận test với bot bằng REST API:

```http
POST /api/v1/test-matches/bot
Authorization: Bearer <access_token>
```

Server sẽ tạo ngay một trận gồm:
- Người dùng hiện tại.
- `Bot 1`, `Bot 2`, `Bot 3`.

Response trả về `matchId`, `roomCode`, `connection.wsUrl`, `connection.wsAccessToken`, và danh sách `participants`. Client dùng `connection.wsUrl` + `connection.wsAccessToken` để connect `GameHub` giống trận thường.

Lưu ý:
- Bot chạy hoàn toàn trong **Game Server**, không có client/WebSocket riêng.
- Bot tự gửi command backend vào match runtime.
- Trận bot test không đi qua room ready flow.
- Trận bot test không lưu match history, không cộng stats/leaderboard.
- Mode này phục vụ dev/test gameplay.

---

## 3. Giai đoạn 2: Handover (Chuyển giao Server)

Đây là bước nhảy quan trọng nhất. Client không gọi REST API để bắt đầu trận mà dựa vào Realtime Event.

1. Chủ phòng gọi method `StartRoomMatch(roomCode)` lên AppHub.
2. API Server kiểm tra đủ điều kiện, tạo bản ghi Match trong Cache, gen Game Ticket và bắn event `RoomMatchStarting` cho **từng thành viên đang online**.
3. **Payload nhận được**:
   ```json
   {
     "event": "RoomMatchStarting",
     "data": {
       "roomCode": "ABC123",
       "startedByUserId": "guid",
       "connection": {
         "wsUrl": "wss://game.memesploding.com/ws", // URL của Game Server
         "wsAccessToken": "jwt-game-ticket" // Ticket 120s chỉ dùng 1 lần để vào GameHub
       }
     }
   }
   ```
4. **Xử lý của Client**:
   - Vẫn **giữ nguyên kết nối AppHub** (để nhận notification hoặc chat lobby).
   - Dùng `wsUrl` và `wsAccessToken` mở **KẾT NỐI MỚI** sang Game Server (`GameHub`).
   - _Lưu ý SignalR thô_: Nếu dùng WebSocket thô (Unity/Custom), phải gửi handshake `{"protocol":"json","version":1}` kèm ký tự `\u001e` ngay sau khi mở liên kết GameHub. Đừng quên thêm `\u001e` vào cuối mỗi message gửi lên.

---

## 4. Giai đoạn 3: Gameplay (Vận hành trận đấu)

Đây là trái tim của game, mọi logic được điều phối qua `GameHub`.

### 4.1. Khởi tạo Bàn chơi
1. Sau khi kết nối GameHub thành công, Server tự động gửi event `Connected`.
2. Ngay lập tức, Server sẽ gửi tiếp event `StateSnapshot` chứa toàn bộ dữ liệu bàn chơi.
3. **Vẽ giao diện (Render UI)**: 
   - Dựa vào mảng `players` để vẽ danh sách đối thủ (số bài `handCount`, máu `lifeState`).
   - Dựa vào mảng `selfHand` để vẽ các lá bài bạn đang cầm.
   - Vẽ xấp bài rút (`drawPileCount`) và xấp bài bỏ (`discardPile`).
   - Dựa vào `turnIndex` và `players[turnIndex].userId` để làm nổi bật (highlight) người đang giữ lượt.

### 4.1.1. Deck setup hiện tại (Original Set)

Hiện tại Game Server chỉ dùng **bộ gốc (Original Set)** cho gameplay chính. Code có một số lá expansion nhưng chưa xem là scope gameplay chính.

**Luật chia bài**
- Mỗi người chơi bắt đầu với `7` lá random từ pool bộ gốc.
- Mỗi người chơi được thêm chắc chắn `1 Defuse` vào tay sau khi chia 7 lá.
- Số `ExplodingKitten` trong draw pile = `playerCount - 1`.
- Số `Defuse` thêm vào draw pile:
  - `1-4` người chơi: `2` lá.
  - `5-6` người chơi: `3` lá.

**Scale số lá bộ gốc theo số người chơi**

`extraCopies = clamp(playerCount - 2, 0, 4)`.

| Số người | Attack | Skip | Favor | Shuffle | SeeTheFuture | Nope | Mỗi loại Cat1-5 | Pool trước chia bài |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 2 | 4 | 4 | 4 | 4 | 5 | 5 | 4 | 46 |
| 3 | 5 | 5 | 5 | 5 | 6 | 6 | 5 | 57 |
| 4 | 6 | 6 | 6 | 6 | 7 | 7 | 6 | 68 |
| 5 | 7 | 7 | 7 | 7 | 8 | 8 | 7 | 79 |
| 6 | 8 | 8 | 8 | 8 | 9 | 9 | 8 | 90 |

**Draw pile sau setup**

Sau khi chia bài, thêm bomb và extra Defuse:

| Số người | ExplodingKitten | Extra Defuse trong draw pile | Draw pile count ban đầu |
| --- | ---: | ---: | ---: |
| 2 | 1 | 2 | 35 |
| 3 | 2 | 2 | 40 |
| 4 | 3 | 2 | 45 |
| 5 | 4 | 3 | 51 |
| 6 | 5 | 3 | 56 |

### 4.2. Vòng lặp Lượt đi (Turn Cycle)
1. Xác định đến lượt của ai bằng cách so sánh `userId` của bạn với `players[turnIndex].userId` trong `StateSnapshot`.
2. Nếu là lượt của bạn, bạn có thể **Đánh bài** (`PlayCard`) bao nhiêu lần tùy ý (miễn là bạn có bài và đáp ứng logic).
3. Khi không muốn đánh nữa hoặc hết bài, bạn **PHẢI bốc bài** (`DrawCard`). Lệnh bốc bài này sẽ **kết thúc lượt của bạn**.
4. Khung thời gian mỗi lượt được quy định ở `turnEndsAt` (thường là 15 giây). Nếu quá thời hạn này, Server tự động ép bạn bốc bài (`DrawCard` force) và chuyển lượt.

### 4.3. Đánh Bài và Các cửa sổ phản ứng (Windows)
Đây là phần Frontend cần đặc biệt chú ý. Rất nhiều cửa sổ (Window) bật lên yêu cầu tương tác. Mọi Window đều có mốc thời gian hết hạn (`...EndsAt`) trong bản tin `StateSnapshot`.

#### A. Action Cards & Nope Window (Cửa sổ Chặn - 5s)
- Khi một người đánh thẻ Hành Động (vd: `Skip`, `Attack`, `Favor`, `SeeTheFuture`...), hành động đó **CHƯA ĐƯỢC THỰC THI NGAY**.
- Server bật cờ `PendingReactionAction` và đặt `ReactionWindowEndsAt` (5 giây).
- **Client UI**: Phải hiện một thanh Progress Bar 5s và nút "Nope" to đùng cho **Tất cả những người chơi khác** (nếu họ có lá Nope trong tay). Những người không có Nope chỉ đứng nhìn.
- Mọi người có thể đánh lệnh `Nope`. Khi bị Nope, cửa sổ 5s sẽ reset lại để người ban đầu có thể "Nope lại cái Nope đó".
- Khi hết thời gian 5s, server đếm tổng số lượng lệnh Nope được đánh (`PendingNopeCount`):
  - Nếu **CHẴN** (0, 2, 4...): Action được thi hành.
  - Nếu **LẺ** (1, 3, 5...): Action bị hủy bỏ, bài vẫn vứt ra xấp bỏ nhưng không có hiệu ứng gì.

#### B. Bomb & Defuse Window (Cửa sổ Gỡ Bom - 6s)
- Bật lên khi một người rút bài trúng `ExplodingKitten` (Mèo Nổ).
- Trạng thái `PendingDefuseUserId` sẽ trỏ vào ID của nạn nhân, kèm theo `DefuseWindowEndsAt` (6 giây).
- **Client UI của nạn nhân**: Phải khóa mọi thao tác (không cho đánh bài khác, rút bài), hiện đỏ màn hình Yêu cầu Gỡ Bom.
  - Nếu nạn nhân có lá `Defuse` trong tay, nạn nhân phải bấm gửi lệnh `UseDefuse`.
  - Nếu không gửi `UseDefuse` trước khi đếm ngược kết thúc, nạn nhân sẽ NỔ TUNG (`lifeState: Dead`) và bị loại khỏi trận.

#### C. Bomb Reinsert Window (Cửa sổ Giấu Bom - 10s)
- Ngay sau khi gửi `UseDefuse` thành công.
- Trạng thái `PendingBombOwnerUserId` trỏ vào nạn nhân, kèm `BombReinsertWindowEndsAt` (10 giây).
- **Client UI của nạn nhân**: Phải hiện một thanh trượt (Slider) từ `0` đến `drawPileCount` để nạn nhân chọn vị trí giấu lại quả bom vào xấp rút.
- Nạn nhân gửi lệnh `ChooseBombInsertPosition` kèm `position` (0 là trên cùng, -1 là dưới đáy). 
- Nếu hết 10s không gửi, Server tự nhét bom vô một vị trí random.

#### D. Favor Window (Cửa sổ Trả Ơn - 10s)
- Bật lên khi người A dùng `Favor` thành công lên người B.
- Trạng thái `PendingFavorTargetId` trỏ vào người B, kèm `FavorWindowEndsAt` (10 giây).
- **Client UI của người B**: Bị khóa thao tác khác, UI bung lên bắt chọn 1 lá bài trên tay để tặng A.
- Người B gửi lệnh `ChooseFavorCard` kèm `cardCode` muốn tặng.
- Nếu hết 10s không chọn, Server tự bốc đại 1 lá ngẫu nhiên trên tay B gửi cho A.

### 4.4. Combo Thẻ Bài (Cat Cards & Mọi loại bài)
Memesploding cho phép ghép nhiều lá bài giống nhau để tạo hiệu ứng đặc biệt. Việc này thực hiện thông qua lệnh `PlayCard` kết hợp thêm trường thông tin combo.

**Combo 2 lá (Ăn trộm ngẫu nhiên):**
Cần 2 lá giống hệt nhau (vd: 2 lá `Cat1`). Bạn được lấy 1 lá bài ngẫu nhiên trên tay đối thủ.
```json
{
  "event": "PlayCard",
  "data": {
    "comboSize": 2,
    "cardCode": "Cat1", // Mã của cặp bài
    "targetUserId": "guid" // ID nạn nhân muốn trộm
  }
}
```

**Combo 3 lá (Xin đích danh 1 lá):**
Cần 3 lá giống hệt nhau. Buộc nạn nhân đưa lá bài bạn đọc tên (nếu họ có lá đó).
```json
{
  "event": "PlayCard",
  "data": {
    "comboSize": 3,
    "cardCode": "Cat2", 
    "targetUserId": "guid", // ID nạn nhân
    "requestedCardCode": "Defuse" // Mã lá bài bạn muốn trấn lột
  }
}
```

**Combo 5 lá (Nhặt đồ phế liệu):**
Cần 5 lá bài **KHÁC NHAU HOÀN TOÀN** trên tay. Cho phép bới xấp bài bỏ (`DiscardPile`) lấy lại 1 lá bất kỳ lên tay mình.
```json
{
  "event": "PlayCard",
  "data": {
    "comboSize": 5,
    "cardCodes": ["Skip", "Cat1", "Attack", "Favor", "Defuse"], // Mảng 5 lá khác nhau
    "discardCardCode": "Attack" // Lá bài muốn vớt lại từ DiscardPile
  }
}
```

### 4.5. Đứt kết nối (Disconnect & Reconnect)
- Nếu một người rớt mạng, trường `Connected` của họ trong Snapshot sẽ bằng `false`.
- Họ không bị xử thua ngay. Game Server giữ chỗ cho họ đến mốc `PendingReconnectUntil` (mặc định 120s).
- Trong lúc họ offline, game vẫn chạy bình thường. Nếu tới lượt họ, hệ thống Timeouts sẽ tự động Rút bài (`DrawCard`) hoặc tự Nổ bom nếu có.
- **Reconnect**: 
  - Client vô lại app -> Gọi REST API `/rooms/{code}` để lấy lại Token -> Nối WebSocket vào GameHub.
  - Gửi lệnh `ReconnectMatch` hoặc đợi nhận `StateSnapshot` mới nhất để vẽ lại bàn chơi và tiếp tục chiến đấu.

---

## 5. Giai đoạn 4: Kết thúc (Conclusion)

1. Khi số người có trạng thái `lifeState == 'Alive'` chỉ còn đúng 1 người.
2. Game Server đổi `phase` thành `Ended`. Client nhận Snapshot mới, chặn toàn bộ thao tác bàn chơi, hiện màn hình loading mờ để chờ kết quả.
3. Game Server đẩy dữ liệu Match (thời gian, lịch sử, ai thắng) qua Message Queue về cho API Server.
4. API Server lưu kết quả vào Database, tính toán XP, cộng/trừ Elo Score.
5. API Server bắn event **`MatchEnded`** qua kênh WebSocket của **AppHub** về cho tất cả người chơi.
6. **Xử lý của Client**:
   - Nhận `MatchEnded` từ AppHub.
   - Ngắt hoàn toàn kết nối với GameHub.
   - Hiện màn hình Kết Quả (Victory/Defeat, điểm XP kiếm được).
   - Khi người chơi bấm "Tiếp tục" (hoặc tự động sau 10s), UI chuyển về lại Màn hình Phòng Chờ (Lobby), vì API Server lúc này đã set trạng thái phòng từ `playing` về `waiting` và mọi người có thể bắt đầu trận mới.
