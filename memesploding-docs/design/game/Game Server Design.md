# Game Server Design (Phase 1)

## 0) Flow hoạt động tổng quan (đọc nhanh)

1. **API Server phát lệnh bắt đầu trận**
   - Host bấm start match ở room.
   - API validate điều kiện room, tạo `matchId`, chuyển room sang `starting`.
   - API publish **integration event** (qua contract/channels trong Shared) để yêu cầu game server start runtime.
   - API push `RoomMatchStarting` qua WS room, kèm `connection.wsUrl` + `connection.wsAccessToken` cho từng player.

2. **Client kết nối Game WS bằng game ticket**
   - Client dùng `wsUrl` + `wsAccessToken` để vào game server.
   - Game server verify ticket (`scope = game_ws`, `userId`, `roomCode/matchId`, hạn ngắn).
   - Client **không** gửi request để chuyển phase room (`waiting -> playing`).

3. **Game server khởi tạo runtime authoritative**
   - Tạo runtime theo `matchId` (in-memory).
   - Nạp danh sách player, setup deck, chia bài, random người đi đầu.
   - Persist snapshot ban đầu để phục vụ reconnect.

4. **Vòng lặp gameplay realtime**
   - Client chỉ gửi command/intent (`PlayCard`, `DrawCard`, ...).
   - Game server validate theo phase/turn/rule rồi mới apply.
   - State chạy theo tick/queue tuần tự để tránh race.

5. **Broadcast state cho client**
   - Game server gửi event gameplay + state patch/snapshot theo phiên bản (`stateVersion`).
   - Client render theo dữ liệu server, không tự quyết định kết quả.

6. **Disconnect/reconnect trong trận**
   - Khi rớt mạng, player được giữ slot trong grace window (phase 1: 120s).
   - Reconnect hợp lệ thì nhận snapshot mới nhất để tiếp tục.
   - Quá hạn grace thì xử lý theo rule AFK/eliminated của trận.

7. **Kết thúc trận và lưu kết quả**
   - Khi đủ điều kiện kết thúc, game server chốt winner + stats.
   - Publish kết quả về API server để persist DB (`matches`, `match_participants`, leaderboard nếu có).
   - Room được trả về pre-game state để người chơi quay lại lobby.

---

## 1) Mục tiêu và phạm vi

### Mục tiêu phase 1
- Xây dựng Game Server cho **core gameplay loop end-to-end** từ `StartMatch` đến `EndMatch`.
- Kiến trúc vận hành ban đầu: **single instance** (dễ triển khai, ổn định trước, scale sau).
- Hỗ trợ **reconnect cơ bản trong 120 giây** bằng snapshot state.
- Scope luật chơi: **chỉ bộ Original**.
- Target vận hành: **internal alpha ~50 phòng đồng thời**, ưu tiên ổn định.

### Ngoài phạm vi phase 1
- Chưa triển khai full social in-game (chat/sticker/spectator).
- Chưa làm multi-instance routing/sharding production-ready.
- Chưa triển khai expansion sets (Imploding/Barking/Streaking).

### Ranh giới trách nhiệm
- **API Server**: auth, profile, friends, lobby và **toàn bộ room lifecycle ngoài trận** (`create`, `join`, `leave`, `kick`, `ready/unready`, `dissolve`, `start match request`).
- **Game Server**: nhận điều khiển từ lúc `StartMatch` thành công, xử lý game state authoritative đến `EndMatch`, sau đó gửi kết quả về API để lưu DB và khôi phục room state.

---

## 2) Nguyên tắc kiến trúc

### 2.0 Ownership model (bắt buộc)
- Mô hình **single-writer theo state**:
  - Pre-game room state (`waiting/starting`) do API Server ghi.
  - In-match runtime state (`playing`) do Game Server ghi.
- Client có thể giữ kết nối tới cả API WS và Game WS, nhưng action phải đi đúng server owner.
- Không cho phép API và Game cùng ghi cùng một field trạng thái trong cùng thời điểm.

### 2.1 Server authoritative
- Toàn bộ game state nằm ở server, client chỉ gửi intent/action.
- Mọi action phải được validate theo turn/state/rule trước khi apply.
- Client không nhận dữ liệu không được phép (bài tay người khác, deck order đầy đủ).

### 2.2 Room Runtime = Actor đơn luồng logic
- Mỗi match room được quản lý bởi một runtime (actor) độc lập.
- Mọi command vào room đi qua queue tuần tự để loại race condition.
- Tick timer và command action đều được serialize trong cùng event loop của room runtime.

### 2.3 Tách lớp theo vai trò
- **Transport Layer**: SignalR Hub + contract mapping.
- **Application Layer**: command handlers, validation, orchestration.
- **Domain/Game Engine Layer**: state machine, rules, effect resolver.
- **Infra Layer**: Redis snapshot, pub/sub, persistence bridge sang API/DB.

### 2.4 Event/Channel boundary
- **Internal events/channels**: đặt trong từng service (`Memesploding.Api`, `Memesploding.Game`) cho flow nội bộ service đó.
- **Integration events/channels (API <-> Game)**: đặt trong Shared để hai server dùng cùng schema, tránh drift contract.
- Game runtime chỉ được start bởi integration event từ API, không bởi command từ client.

---


## 3) Runtime model và state model

### 3.1 Trạng thái vòng đời room trong game server
1. `WaitingStart` (đã có room pre-game, chờ StartMatch)
2. `Dealing` (setup deck, chia bài, random first player)
3. `Playing` (turn loop + reaction windows)
4. `ResolvingExplosion` (defuse window khi rút bomb)
5. `Finished` (xác định winner, finalize stats, publish match result)
6. `Archived` (dọn runtime, giữ snapshot ngắn hạn cho reconnect hậu kỳ)

### 3.2 Trạng thái người chơi
- `Alive`, `Eliminated`, `Disconnected`
- Cờ bổ sung:
  - `PendingReconnectUntil` (UTC deadline 120s)
  - `MustDrawCount` (tích lũy do Attack)
  - `HasDefuse` (phục vụ validate nhanh)

### 3.3 Snapshot strategy (reconnect 120s)
- Snapshot tối thiểu gồm:
  - Match metadata (roomCode, phase, turn index, timer remaining)
  - Player public states + private hand per player
  - Draw pile/discard pile + pending effects/reaction window
- Lưu Redis theo key match runtime, TTL phù hợp vòng đời match + reconnect grace.

---

## 4) Realtime protocol (SignalR envelope thống nhất)

### 4.1 Nguyên tắc contract
- Reuse envelope hiện tại:  
  `event` + `data` + `timestamp`
- Enum/event gửi dạng string, camelCase.
- Tách nhóm event gameplay rõ ràng để client render deterministic.

### 4.2 Nhóm client -> server (ý tưởng command)
- `PlayCard`
- `DrawCard`
- `UseDefuse`
- `ChooseBombInsertPosition`
- `Nope`
- `ReconnectMatch`
- `AckStateVersion` (optional nếu cần sync/catch-up)

> `StartMatch` không thuộc nhóm client -> game. Đây là API room action và API sẽ phát integration event sang game server.

### 4.3 Nhóm server -> client (ý tưởng event)
- `MatchStarted`
- `TurnChanged`
- `ActionAccepted` / `ActionRejected`
- `ReactionWindowOpened` / `ReactionWindowClosed`
- `CardDrawn` (public/private payload theo quyền nhìn)
- `ExplosionTriggered` / `DefuseWindowOpened` / `PlayerEliminated`
- `StatePatched` (delta) hoặc `StateSnapshot` (khi reconnect)
- `MatchEnded`

### 4.4 Versioning contract
- Thêm `protocolVersion` ở handshake hoặc first server push.
- Mỗi state update có `stateVersion` tăng dần để client detect missing event.

### 4.5 Timing policy (đã chốt)
- Turn timer: **15s**.
- Nope reaction window: **3s**.
- Defuse decision window: **5s**.
- Bomb reinsert window sau Defuse: **10s**.
- Effect resolution delay sau khi đóng cửa sổ phản ứng: **400ms**.
- Reconnect grace window: **120s**.

---

## 5) Luồng nghiệp vụ cốt lõi phase 1

### 5.1 StartMatch
1. Host gọi `StartMatch` vào API Server.
2. API validate host + trạng thái ready + khóa room sang `starting`.
3. API publish integration event `StartMatchRequested` sang game server (channel/contract trong Shared).
4. Game server nhận event, tạo runtime, load card sets (Original), build deck, chia bài, random người đi đầu.
5. Game persist initial snapshot + broadcast `MatchStarted` cho client đã kết nối game WS.
6. Game publish integration event `MatchStarted`/`RoomUpdated` về API để API cập nhật presence + phản ánh trạng thái `playing`.

### 5.2 Turn loop
1. Mở turn + countdown timer.
2. Nhận command `PlayCard` hoặc `DrawCard`.
3. Nếu có action cần phản ứng -> mở reaction window Nope.
4. Resolve effect cuối cùng sau khi đóng window.
5. Chuyển turn hoặc chuyển state đặc biệt (explosion).

### 5.3 Explosion/Defuse
1. Khi player rút Exploding Kitten -> vào `ResolvingExplosion`.
2. Mở cửa sổ Defuse (timeout xác định).
3. Nếu có Defuse hợp lệ: player chọn vị trí trả bomb vào deck.
4. Nếu không: eliminate player, remove bomb theo luật.

### 5.4 EndMatch
1. Khi còn 1 người sống -> winner.
2. Tổng hợp stats + participant results.
3. Gửi dữ liệu kết quả cho API layer để lưu `matches`/`match_participants`.
4. API khôi phục room về trạng thái pre-game (`waiting`) để người chơi quay lại phòng.
5. Broadcast `MatchEnded`, cleanup runtime có kiểm soát.

---

## 6) Data model mức thiết kế

### 6.1 In-memory runtime model
- `MatchRuntimeState`
  - `MatchId`, `RoomCode`, `Phase`, `StateVersion`
  - `Players[]`, `TurnState`, `DeckState`, `DiscardState`
  - `PendingReaction`, `PendingDefuse`, `Metrics`

### 6.2 Redis keys (dự kiến mở rộng)
- `game:match:{roomCode}:state` (serialized snapshot)
- `game:match:{roomCode}:events` (optional ring buffer cho catch-up)
- `game:match:{roomCode}:lock` (optional guard khi scale multi-instance)
- Reuse `room:*` hiện có cho bridge presence/status.

### 6.3 Bridge với API Server
- API publish integration event StartMatch (kèm snapshot room pre-game tối thiểu) theo shared contract; game server consume event này để start runtime.
- Game server publish integration events room/match updates theo shared contract (bao gồm match started/match ended callback).
- API là nơi cuối cùng persist lịch sử trận và reset room state cho phiên tiếp theo.

---

## 7) Roadmap triển khai

### Milestone 0 - Foundation
- Tạo skeleton game server modules (transport/app/domain/infra).
- Định nghĩa contract event/command cho gameplay phase 1.
- Thống nhất mapper DTO/public-private payload.

### Milestone 1 - Room runtime + lifecycle
- Runtime manager theo room code.
- Queue command tuần tự + timer engine cơ bản.
- State machine lifecycle (`WaitingStart` -> `Finished`).

### Milestone 2 - Original core rules
- Deck setup, deal, draw, skip/attack/shuffle/see-the-future/favor/nope.
- Reaction window Nope.
- Explosion + Defuse flow hoàn chỉnh.

### Milestone 3 - Reconnect 120s + snapshot
- Persist snapshot định kỳ + tại critical transitions.
- Reconnect handshake và rehydrate state theo player.
- Timeout reconnect xử lý AFK theo rule phase 1.

### Milestone 4 - Match finalize + integration
- Persist match result sang API DB.
- Đồng bộ presence/activity trạng thái `in_match` -> `idle/in_room`.
- Hardening error paths + audit logs gameplay.

### Milestone 5 - Internal alpha hardening
- Soak test ~50 rooms đồng thời.
- Tuning timer jitter, memory profile, cleanup leak check.
- Freeze protocol v1 và checklist release internal alpha.

---

## 8) Test strategy

### 8.1 Unit tests (ưu tiên cao)
- Rule resolver cho từng card effect Original.
- State transition tests (happy path + invalid transition).
- Reaction/Nope resolution order tests.

### 8.2 Deterministic simulation tests
- Seeded RNG để replay một match với cùng kết quả.
- Golden scenarios cho các case phức tạp (attack chain, explosion timing).

### 8.3 Integration tests
- Hub command -> runtime -> event push.
- Redis snapshot save/load + reconnect restore.
- API bridge test cho match finalize.

### 8.4 Load/soak tests
- Kịch bản alpha: 50 rooms đồng thời, latency và error budget theo NFR.
- Đo memory per room runtime, cleanup sau match.

---

## 9) Risk và phương án giảm thiểu

1. **Race/timer drift gây sai luật**  
   - Mitigation: single-thread room queue + unified clock source per runtime + deterministic tests.

2. **Payload lộ thông tin riêng tư lá bài**  
   - Mitigation: phân tách DTO public/private ở mapper layer, test snapshot/payload visibility.

3. **Reconnect restore sai state**  
   - Mitigation: snapshot tại critical points + stateVersion + integration replay tests.

4. **Mismatch contract giữa API docs và runtime thật**  
   - Mitigation: contract-first, lock schema, add compatibility tests client stub.

5. **Khó scale từ single-instance sang multi-instance**  
   - Mitigation: giữ abstraction runtime manager + routing boundary rõ ngay từ phase 1.

---

## 10) Definition of Done cho phase 1

- Có thể chạy full match Original từ start đến end qua SignalR realtime.
- Server authoritative, validate đầy đủ action cơ bản.
- Reconnect trong 120s hoạt động cho player hợp lệ.
- Kết quả match được lưu và truy vấn qua API hiện có.
- Vượt tiêu chí internal alpha (~50 phòng đồng thời) với độ ổn định chấp nhận được.
