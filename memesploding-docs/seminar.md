# Seminar: SignalR và ứng dụng trong Game Server Memesploding

## Slide 1: Tiêu đề

**SignalR và ứng dụng trong game server realtime**

- Phần 1: Giới thiệu SignalR
- Phần 2: Triển khai SignalR trong Memesploding Game Server
- Case study: game bài realtime nhiều người chơi

---

## Slide 2: Mục tiêu bài trình bày

- Hiểu SignalR là gì và giải quyết bài toán nào
- Nắm cơ chế hoạt động cơ bản của SignalR
- Biết cú pháp chính khi dùng SignalR:
  - khai báo Hub
  - map endpoint
  - client gọi server
  - server gọi client
  - group broadcast
- Phân tích cách Memesploding Game Server áp dụng SignalR

---

# Phần 1: Giới thiệu SignalR

---

## Slide 3: Bài toán realtime trong ứng dụng hiện đại

- Nhiều ứng dụng cần server chủ động gửi dữ liệu về client
- Ví dụ:
  - chat
  - notification
  - dashboard realtime
  - game online
  - cộng tác nhiều người dùng
- Request-response truyền thống không đủ tối ưu vì client phải polling liên tục

---

## Slide 4: SignalR là gì?

- SignalR là thư viện realtime của ASP.NET Core
- Cho phép giao tiếp hai chiều giữa client và server
- Server có thể gọi method trên client
- Client có thể gọi method trên server
- SignalR trừu tượng hoá phần quản lý connection và transport

---

## Slide 5: SignalR giải quyết vấn đề gì?

- Giảm nhu cầu tự viết WebSocket raw
- Quản lý connection theo mô hình Hub
- Hỗ trợ gửi message đến:
  - một connection cụ thể
  - một user
  - một group
  - tất cả client
- Tự động chọn transport phù hợp
- Tích hợp tốt với dependency injection, authentication và logging của ASP.NET Core

---

## Slide 6: Cơ chế hoạt động tổng quan

1. Client kết nối tới một SignalR Hub endpoint
2. Server thực hiện handshake để thống nhất protocol
3. Connection được giữ mở bằng WebSockets hoặc transport fallback
4. Client invoke method trên Hub
5. Hub xử lý và có thể gọi method ngược lại trên client
6. Server có thể broadcast qua `Clients.All`, `Clients.Caller`, `Clients.Group(...)`

---

## Slide 7: Transport và fallback

- SignalR ưu tiên WebSockets nếu môi trường hỗ trợ
- Nếu không dùng được WebSockets, SignalR có thể fallback:
  - Server-Sent Events
  - Long Polling
- Lập trình viên vẫn làm việc với cùng một API Hub
- Điểm mạnh: giảm chi phí xử lý khác biệt giữa các transport

---

## Slide 8: Cú pháp server - khai báo Hub

```csharp
using Microsoft.AspNetCore.SignalR;

public class ChatHub : Hub
{
    public async Task SendMessage(string user, string message)
    {
        await Clients.All.SendAsync("ReceiveMessage", user, message);
    }
}
```

- Hub là class trung tâm của SignalR
- Method public trong Hub có thể được client gọi
- `Clients` dùng để gửi message từ server về client

---

## Slide 9: Cú pháp server - map endpoint và cấu hình

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

var app = builder.Build();

app.MapHub<ChatHub>("/chat");

app.Run();
```

- `AddSignalR()` đăng ký dịch vụ SignalR
- `MapHub<ChatHub>("/chat")` tạo endpoint realtime
- Client kết nối vào URL tương ứng, ví dụ `/chat`

---

## Slide 10: Cú pháp client - kết nối và gọi Hub

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/chat")
  .build();

connection.on("ReceiveMessage", (user, message) => {
  console.log(user, message);
});

await connection.start();
await connection.invoke("SendMessage", "Alice", "Hello");
```

- `connection.on(...)`: đăng ký method client để server gọi
- `connection.start()`: mở kết nối SignalR
- `connection.invoke(...)`: gọi method trên Hub

---

## Slide 11: Group trong SignalR

```csharp
await Groups.AddToGroupAsync(Context.ConnectionId, "room:ABC123");

await Clients.Group("room:ABC123")
    .SendAsync("ReceiveMessage", payload);
```

- Group là tập hợp nhiều connection
- Phù hợp với chat room, lobby, match, channel
- Server có thể broadcast chỉ trong một group
- Group giúp tránh gửi dữ liệu không liên quan tới client khác

---

## Slide 12: Authentication và connection lifecycle

- Hub có thể đọc thông tin HTTP context khi client kết nối
- Có thể dùng cookie, bearer token hoặc token query string
- Các lifecycle method thường dùng:
  - `OnConnectedAsync`
  - `OnDisconnectedAsync`
- Dùng để:
  - validate quyền truy cập
  - lưu connection context
  - add/remove group
  - đánh dấu online/offline

---

## Slide 13: Khi nào nên dùng SignalR?

- Nên dùng khi cần realtime hai chiều và backend là ASP.NET Core
- Phù hợp với:
  - chat
  - notification
  - dashboard realtime
  - multiplayer lobby
  - game server theo lượt hoặc realtime nhẹ
- Không nên nhồi toàn bộ business logic vào Hub
- Hub nên là lớp transport, còn domain logic nên nằm ở service/runtime riêng

---

# Phần 2: Triển khai SignalR trong Memesploding Game Server

---

## Slide 14: Bối cảnh project Memesploding

- Memesploding là game bài kiểu Mèo Nổ
- Phần này chỉ tập trung vào cách Game Server dùng SignalR
- Các câu hỏi chính:
  - Client kết nối vào đâu?
  - Server xác thực connection như thế nào?
  - Client gửi command qua method nào?
  - Server push message về client qua method nào?
  - Room được ánh xạ vào SignalR group ra sao?

---

## Slide 15: SignalR nằm ở đâu trong Game Server?

- `Program.cs`: đăng ký SignalR và map endpoint `/ws`
- `GameHub`: Hub chính cho toàn bộ realtime gameplay
- `WsClientCommand`: format command client gửi lên Hub
- `WsServerEvent<T>`: format message server push về client
- `IHubContext<GameHub>`: cho phép worker gửi SignalR message ngoài Hub
- SignalR trong project giữ vai trò transport layer

---

## Slide 16: Endpoint `/ws` trong Game Server

```csharp
builder.Services.AddSignalR().AddJsonProtocol(...);

app.MapHub<GameHub>("/ws");
```

- Client kết nối tới Game Server qua `/ws`
- Token truyền bằng query string: `?access_token=<gameTicket>`
- SignalR dùng JSON protocol
- `HubOptions.MaximumReceiveMessageSize = 64 * 1024`
- CORS bật credential để hỗ trợ connection SignalR từ client

---

## Slide 17: Cách truyền token qua SignalR connection

- Client truyền game ticket qua query string:

```text
/ws?access_token=<gameTicket>
```

- `GameHub.OnConnectedAsync` đọc token từ HTTP context
- `GameTicketValidator` validate token trước khi cho connection vào game
- Các claim được dùng để định danh realtime connection:
  - `scope = game_ws`
  - `sub = userId`
  - `room_code`
  - `match_id`
- Nếu token sai: `Context.Abort()`
- Nếu token đúng: lưu thành `GameConnectionContext`

---

## Slide 18: Lifecycle SignalR connection trong `GameHub`

- Khi connected:
  - validate token
  - lưu context theo `Context.ConnectionId`
  - add connection vào SignalR group của room
  - gửi `Connected` cho `Clients.Caller`
  - gửi `state_snapshot` cho `Clients.Caller`
- Khi disconnected:
  - xoá connection context
  - remove connection khỏi group
  - cập nhật trạng thái disconnected cho player

---

## Slide 19: SignalR group theo room

```csharp
private static string GetRoomGroup(string roomCode)
    => $"room:{roomCode.ToUpperInvariant()}";
```

- Mỗi phòng game được ánh xạ thành một SignalR group
- Connection hợp lệ được add vào `room:{ROOM_CODE}`
- Khi có event của trận, server gửi vào group tương ứng
- Lợi ích:
  - không gửi nhầm event sang phòng khác
  - không cần quản lý thủ công danh sách connection trong room

---

## Slide 20: Client gọi server qua `SendCommand`

```csharp
public record WsClientCommand(string Event, JsonElement Data);
```

- Client invoke Hub method `SendCommand`
- Payload gồm:
  - `Event`: tên command, ví dụ `DrawCard`, `PlayCard`, `Nope`
  - `Data`: JSON payload của command
- `GameHub` dùng `Context.ConnectionId` để biết ai gửi command
- Đây là cách project dùng SignalR cho hướng client -> server

---

## Slide 21: Server push về client qua `ReceiveMessage`

```csharp
public record WsServerEvent<T>(
    string Event,
    T Data,
    DateTime Timestamp
);
```

- Tất cả message server gửi về đều đi qua client method `ReceiveMessage`
- Envelope thống nhất gồm:
  - `Event`: tên event
  - `Data`: payload
  - `Timestamp`: thời điểm server tạo message
- Các event tiêu biểu:
  - `Connected`
  - `Ack`
  - `state_snapshot`
  - `gameplay_event`
  - `match_ended`

---

## Slide 22: Broadcast event bằng `IHubContext`

- `RuntimeTickWorker` không nằm trong Hub nhưng vẫn gửi được SignalR message
- Worker inject `IHubContext<GameHub>`
- Dùng:

```csharp
hubContext.Clients.Group(GetRoomGroup(roomCode))
    .SendAsync("ReceiveMessage", payload);
```

- Ý nghĩa:
  - Hub nhận connection và command
  - worker vẫn có thể push event realtime
  - event được gửi đúng group room
- Đây là pattern quan trọng khi broadcast không xuất phát trực tiếp từ Hub method

---

## Slide 23: Snapshot, reconnect và server time

- `state_snapshot` là message SignalR gửi qua `ReceiveMessage`
- Gửi trong các trường hợp:
  - client vừa connect
  - client gọi `ReconnectMatch`
  - client gọi `RequestStateSnapshot`
- `RequestServerTime` cũng đi qua `SendCommand`
- Server trả `ServerTime` cho `Clients.Caller`
- Các phần này đều là cách dùng SignalR để đồng bộ lại client

---

## Slide 24: Tổng kết

- SignalR cung cấp realtime abstraction trên ASP.NET Core
- Cú pháp cốt lõi gồm Hub, endpoint, client invoke, server send và group
- Trong Memesploding:
  - `/ws` là endpoint SignalR của Game Server
  - `GameHub` quản lý connection lifecycle
  - `SendCommand` là hướng client -> server
  - `ReceiveMessage` là hướng server -> client
  - group `room:{ROOM_CODE}` giới hạn phạm vi broadcast
  - `IHubContext<GameHub>` giúp broadcast từ background worker
- Bài học chính: trong project này, SignalR là lớp realtime transport cho game server
