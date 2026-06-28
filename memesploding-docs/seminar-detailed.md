# Seminar chi tiết: SignalR và ứng dụng trong Game Server Memesploding

## Slide 1: Tiêu đề

**SignalR và ứng dụng trong game server realtime**

- Bài thuyết trình gồm 2 phần rõ ràng:
  - **Phần 1:** Giới thiệu SignalR, cơ chế hoạt động và cú pháp cơ bản.
  - **Phần 2:** Cách Memesploding Game Server sử dụng SignalR trong thực tế.
- Case study là game bài realtime nhiều người chơi, nơi server cần nhận hành động từ client và đẩy trạng thái mới về nhiều người trong cùng phòng.

**Ý chính cần hiểu:** SignalR không phải là game engine. SignalR là lớp giao tiếp realtime giúp game server và client trao đổi message hai chiều.

---

## Slide 2: Mục tiêu bài trình bày

- Sau phần giới thiệu, người nghe cần hiểu SignalR giải quyết vấn đề gì so với HTTP request-response truyền thống.
- Sau phần cú pháp, người nghe cần biết các khái niệm chính:
  - **Hub:** điểm vào realtime ở phía server.
  - **Endpoint:** URL client dùng để kết nối vào Hub.
  - **Invoke:** client gọi method trên server.
  - **SendAsync:** server gọi method trên client.
  - **Group:** gom nhiều connection vào một kênh để broadcast đúng phạm vi.
- Sau phần project, người nghe cần hình dung rõ cách Memesploding dùng SignalR qua `/ws`, `GameHub`, `SendCommand`, `ReceiveMessage`, group theo room và `IHubContext`.

**Ý chính cần hiểu:** Bài này đi từ khái niệm chung đến implementation cụ thể, không chỉ nói SignalR là gì mà còn chỉ ra nó nằm ở đâu trong code game server.

---

# Phần 1: Giới thiệu SignalR

---

## Slide 3: Bài toán realtime trong ứng dụng hiện đại

- Với HTTP API thông thường, client gửi request rồi server trả response. Cách này phù hợp với các thao tác như đăng nhập, lấy danh sách dữ liệu, cập nhật form.
- Nhưng trong ứng dụng realtime, nhiều dữ liệu phát sinh từ server và cần được gửi ngay về client mà không đợi client hỏi.
- Ví dụ:
  - Chat: khi người A gửi tin nhắn, người B phải nhận ngay.
  - Notification: khi có thông báo mới, server cần đẩy về client.
  - Dashboard realtime: số liệu thay đổi liên tục.
  - Game online: khi một người chơi hành động, những người khác phải thấy diễn biến đó ngay.
- Nếu dùng polling, client phải gọi API liên tục. Điều này gây tốn request, tăng độ trễ và khó mở rộng khi số client lớn.

**Ý chính cần hiểu:** Realtime cần một kết nối lâu dài để server chủ động gửi dữ liệu về client.

---

## Slide 4: SignalR là gì?

- SignalR là thư viện realtime của ASP.NET Core, được thiết kế để đơn giản hoá giao tiếp hai chiều giữa client và server.
- Thay vì chỉ có client gọi server như REST API, SignalR cho phép:
  - client gọi method trên server Hub;
  - server gọi method đã đăng ký ở phía client.
- SignalR che bớt các chi tiết phức tạp như WebSocket connection, message framing, danh sách connection, group broadcast và fallback transport.
- Lập trình viên làm việc với các abstraction dễ hiểu hơn: `Hub`, `Clients`, `Groups`, `Context`.

**Ý chính cần hiểu:** SignalR biến realtime communication thành mô hình gọi method hai chiều giữa client và server.

---

## Slide 5: SignalR giải quyết vấn đề gì?

- Nếu tự viết WebSocket raw, ta phải tự xử lý nhiều việc:
  - mở và đóng connection;
  - định dạng message;
  - xác định client nào đang kết nối;
  - broadcast cho một nhóm client;
  - xử lý reconnect hoặc lỗi transport;
  - tích hợp authentication.
- SignalR cung cấp sẵn các API cho những nhu cầu này.
- SignalR cho phép gửi message theo nhiều phạm vi:
  - `Clients.Caller`: gửi về chính client đang gọi Hub method.
  - `Clients.All`: gửi cho tất cả client đang kết nối Hub.
  - `Clients.Group(...)`: gửi cho các connection trong một group.
  - gửi theo user nếu hệ thống có user identifier.
- SignalR cũng tích hợp với dependency injection, logging, authorization và cấu hình của ASP.NET Core.

**Ý chính cần hiểu:** SignalR giúp tập trung vào luồng nghiệp vụ realtime thay vì tự xử lý hạ tầng socket.

---

## Slide 6: Cơ chế hoạt động tổng quan

1. Client tạo một SignalR connection và trỏ tới Hub endpoint, ví dụ `/chat` hoặc `/ws`.
2. Client và server thực hiện handshake để thống nhất protocol, thường là JSON.
3. Sau handshake, connection được giữ mở bằng WebSockets nếu có thể.
4. Client có thể gọi method public trên Hub bằng `invoke`.
5. Hub xử lý request realtime và có thể gọi ngược method trên client bằng `SendAsync`.
6. Nếu cần broadcast, Hub hoặc service khác có thể gửi message tới nhiều client qua `Clients.All`, `Clients.Caller` hoặc `Clients.Group(...)`.

**Ví dụ dễ hiểu:** Client gọi `SendMessage`, server nhận trong Hub, sau đó server gọi `ReceiveMessage` trên các client cần nhận tin.

**Ý chính cần hiểu:** SignalR không hoạt động theo kiểu mỗi lần một HTTP response riêng lẻ, mà giữ connection mở để trao đổi message liên tục.

---

## Slide 7: Transport và fallback

- Transport là cơ chế mạng bên dưới dùng để truyền message realtime.
- SignalR ưu tiên **WebSockets** vì WebSockets hỗ trợ giao tiếp hai chiều trên một connection lâu dài, phù hợp nhất với realtime.
- Nếu WebSockets không khả dụng, SignalR có thể fallback sang:
  - **Server-Sent Events:** server đẩy dữ liệu một chiều về client.
  - **Long Polling:** client gửi request giữ lâu, server trả khi có dữ liệu hoặc timeout.
- Điểm quan trọng là code Hub vẫn gần như không đổi dù transport bên dưới khác nhau.

**Ý chính cần hiểu:** SignalR giúp lập trình viên viết theo API thống nhất, còn việc chọn transport phù hợp do SignalR xử lý.

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

- `ChatHub : Hub` nghĩa là class này là một SignalR Hub.
- `SendMessage` là public method, nên client có thể gọi method này qua SignalR.
- `Clients.All.SendAsync(...)` nghĩa là server gọi method `ReceiveMessage` trên tất cả client đang kết nối.
- Tên `"ReceiveMessage"` phải khớp với method mà client đã đăng ký bằng `connection.on("ReceiveMessage", ...)`.

**Ý chính cần hiểu:** Hub là nơi client gọi vào server, còn `Clients` là cổng để server gọi ngược về client.

---

## Slide 9: Cú pháp server - map endpoint và cấu hình

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();

var app = builder.Build();

app.MapHub<ChatHub>("/chat");

app.Run();
```

- `AddSignalR()` đăng ký các service cần thiết để ASP.NET Core chạy SignalR.
- `MapHub<ChatHub>("/chat")` tạo endpoint `/chat` cho Hub.
- Từ phía client, URL `/chat` là địa chỉ dùng để mở SignalR connection.
- Trong project thực tế, endpoint có thể là `/ws`, `/hub`, `/notification`, hoặc bất kỳ route nào phù hợp.

**Ý chính cần hiểu:** Muốn client kết nối được SignalR, server phải đăng ký SignalR service và map Hub vào một endpoint cụ thể.

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

- `HubConnectionBuilder()` tạo connection ở phía client.
- `.withUrl("/chat")` chỉ ra Hub endpoint mà client sẽ kết nối.
- `connection.on("ReceiveMessage", handler)` đăng ký method client để server có thể gọi.
- `connection.start()` mở connection SignalR.
- `connection.invoke("SendMessage", ...)` gọi method `SendMessage` trên server Hub.

**Ý chính cần hiểu:** Ở phía client luôn có hai việc chính: đăng ký method để nhận message và invoke method để gửi message lên server.

---

## Slide 11: Group trong SignalR

```csharp
await Groups.AddToGroupAsync(Context.ConnectionId, "room:ABC123");

await Clients.Group("room:ABC123")
    .SendAsync("ReceiveMessage", payload);
```

- Group là một tập hợp connection do server quản lý.
- Một connection có thể được add vào một hoặc nhiều group.
- Khi server gửi tới `Clients.Group("room:ABC123")`, chỉ các connection trong group đó nhận được message.
- Group rất phù hợp với các mô hình:
  - chat room;
  - game room;
  - lobby;
  - channel notification;
  - match/session riêng.

**Ý chính cần hiểu:** Group là cách SignalR giới hạn phạm vi broadcast, đặc biệt quan trọng khi server có nhiều phòng hoặc nhiều trận cùng lúc.

---

## Slide 12: Authentication và connection lifecycle

- SignalR connection vẫn bắt đầu từ HTTP request, nên Hub có thể đọc thông tin từ HTTP context khi client kết nối.
- Token có thể truyền qua:
  - header Authorization;
  - cookie;
  - query string, nhất là khi client hoặc transport có giới hạn về header.
- Hai lifecycle method quan trọng:
  - `OnConnectedAsync`: chạy khi connection được mở.
  - `OnDisconnectedAsync`: chạy khi connection đóng.
- Trong các method này, server thường:
  - validate quyền truy cập;
  - lưu mapping giữa `ConnectionId` và user/session;
  - add connection vào group;
  - remove connection khỏi group khi disconnect.

**Ý chính cần hiểu:** Lifecycle của Hub là nơi phù hợp để kiểm soát ai được vào realtime channel và connection đó thuộc nhóm nào.

---

## Slide 13: Khi nào nên dùng SignalR?

- SignalR phù hợp khi ứng dụng cần realtime hai chiều và backend dùng ASP.NET Core.
- Các use case phù hợp:
  - chat và notification;
  - dashboard realtime;
  - multiplayer lobby;
  - game theo lượt hoặc game realtime nhẹ;
  - cập nhật trạng thái đơn hàng, giao dịch, tiến trình xử lý.
- Tuy nhiên, Hub không nên chứa toàn bộ business logic. Nếu nhồi quá nhiều logic vào Hub, code sẽ khó test, khó maintain và bị phụ thuộc vào connection.
- Pattern tốt hơn là Hub chỉ nhận/gửi message, còn nghiệp vụ nằm trong service hoặc domain layer riêng.

**Ý chính cần hiểu:** SignalR nên được xem là transport layer realtime, không phải nơi chứa toàn bộ logic ứng dụng.

---

# Phần 2: Triển khai SignalR trong Memesploding Game Server

---

## Slide 14: Bối cảnh project Memesploding

- Memesploding là game bài kiểu Mèo Nổ, có nhiều người chơi trong một phòng.
- Trong trận đấu, client cần giao tiếp realtime với Game Server:
  - khi vừa vào trận cần nhận trạng thái hiện tại;
  - khi người chơi thao tác cần gửi command lên server;
  - khi server có diễn biến mới cần push event về các client trong cùng phòng;
  - khi reconnect cần gửi lại snapshot cho client.
- Phần này chỉ tập trung vào SignalR: endpoint, Hub, token, group, method gửi/nhận message và broadcast.
- Không đi sâu vào luật bài, deck, rule validation hay cách runtime xử lý state.

**Ý chính cần hiểu:** Trong Memesploding, SignalR là đường realtime giữa client và Game Server trong lúc trận đấu diễn ra.

---

## Slide 15: SignalR nằm ở đâu trong Game Server?

- `Program.cs` là nơi đăng ký SignalR service và map `GameHub` vào endpoint `/ws`.
- `GameHub` là Hub chính cho realtime gameplay. Nó nhận connection, nhận command từ client và gửi message trực tiếp cho client khi cần.
- `WsClientCommand` là DTO mô tả format command client gửi lên qua Hub.
- `WsServerEvent<T>` là DTO mô tả format message server gửi về client.
- `IHubContext<GameHub>` cho phép các background worker gửi SignalR message dù không nằm trong class Hub.
- Nhìn tổng thể, SignalR trong project nằm ở lớp giao tiếp. Nó không tự quyết định luật game, mà chỉ truyền command và event giữa client với server.

**Ý chính cần hiểu:** Các thành phần liên quan trực tiếp đến SignalR là `Program.cs`, `GameHub`, DTO message và `IHubContext<GameHub>`.

---

## Slide 16: Endpoint `/ws` trong Game Server

```csharp
builder.Services.AddSignalR().AddJsonProtocol(...);

app.MapHub<GameHub>("/ws");
```

- Game Server đăng ký SignalR bằng `AddSignalR()`.
- Project dùng JSON protocol để payload dễ đọc, dễ debug và phù hợp với client/test tool.
- `app.MapHub<GameHub>("/ws")` nghĩa là toàn bộ realtime gameplay đi qua endpoint `/ws`.
- Client kết nối vào `/ws` kèm game ticket:

```text
/ws?access_token=<gameTicket>
```

- Project cũng cấu hình `HubOptions.MaximumReceiveMessageSize = 64 * 1024` để giới hạn kích thước message client gửi lên.
- CORS được cấu hình cho phép credential vì SignalR cần duy trì connection realtime từ client tới server.

**Ý chính cần hiểu:** `/ws` là cửa vào SignalR của Game Server, và mọi realtime connection trong trận đều đi qua Hub này.

---

## Slide 17: Cách truyền token qua SignalR connection

- Khi client muốn vào Game Server, client không chỉ mở connection tới `/ws`, mà phải kèm `access_token`.
- Token được truyền qua query string:

```text
/ws?access_token=<gameTicket>
```

- Trong `GameHub.OnConnectedAsync`, server đọc token bằng HTTP context của SignalR connection.
- `GameTicketValidator` validate token trước khi cho connection tham gia realtime game.
- Các claim quan trọng trong token:
  - `scope = game_ws`: token này dùng cho WebSocket/game realtime, không phải token bất kỳ.
  - `sub = userId`: xác định người chơi.
  - `room_code`: xác định phòng mà connection được phép tham gia.
  - `match_id`: xác định trận nếu token có thông tin match.
- Nếu token sai hoặc thiếu thông tin quan trọng, server gọi `Context.Abort()` để ngắt connection.
- Nếu token hợp lệ, server tạo `GameConnectionContext` để gắn connection với user, room và match.

**Ý chính cần hiểu:** Project không cho client vào `/ws` tự do; SignalR connection được kiểm soát bằng game ticket.

---

## Slide 18: Lifecycle SignalR connection trong `GameHub`

- Khi một client connect thành công, `OnConnectedAsync` làm các bước chính:
  - validate token;
  - lưu `GameConnectionContext` theo `Context.ConnectionId`;
  - add connection vào SignalR group của room;
  - gửi `Connected` cho chính connection đó bằng `Clients.Caller`;
  - gửi `state_snapshot` để client có trạng thái hiện tại.
- Khi client disconnect, `OnDisconnectedAsync`:
  - xoá context của connection;
  - remove connection khỏi group;
  - cập nhật trạng thái disconnected cho player ở phía server.
- `Context.ConnectionId` rất quan trọng vì nó là định danh duy nhất của connection hiện tại trong SignalR.

**Ý chính cần hiểu:** `OnConnectedAsync` và `OnDisconnectedAsync` là nơi project quản lý vòng đời realtime connection.

---

## Slide 19: SignalR group theo room

```csharp
private static string GetRoomGroup(string roomCode)
    => $"room:{roomCode.ToUpperInvariant()}";
```

- Mỗi phòng game trong project được ánh xạ thành một SignalR group.
- Ví dụ room code `ABC123` sẽ thành group `room:ABC123`.
- Khi player connect hợp lệ, connection của player được add vào group của room.
- Khi server có event của một trận, server gửi message vào group room tương ứng thay vì gửi toàn bộ client.
- Lợi ích:
  - người chơi chỉ nhận event của phòng mình;
  - server không cần tự quản lý danh sách connection trong từng room;
  - tránh gửi nhầm dữ liệu giữa các trận;
  - code broadcast ngắn và rõ ràng hơn.

**Ý chính cần hiểu:** Group là cách project biến room game thành channel realtime trong SignalR.

---

## Slide 20: Client gọi server qua `SendCommand`

```csharp
public record WsClientCommand(string Event, JsonElement Data);
```

- Project dùng một Hub method chung tên là `SendCommand` cho hướng client -> server.
- Thay vì tạo nhiều Hub method như `DrawCard()`, `PlayCard()`, `Nope()`, project gom các hành động vào một format command chung.
- `WsClientCommand` có hai phần:
  - `Event`: tên command, ví dụ `DrawCard`, `PlayCard`, `Nope`, `ReconnectMatch`.
  - `Data`: payload JSON chứa dữ liệu chi tiết của command.
- `GameHub` dùng `Context.ConnectionId` để lấy connection context, từ đó biết command này đến từ user và room nào.
- Một số command hệ thống như `RequestServerTime` được Hub xử lý trực tiếp để trả thời gian server.

**Ý chính cần hiểu:** Trong project, client gửi mọi command realtime lên Game Server bằng cách invoke `SendCommand`.

---

## Slide 21: Server push về client qua `ReceiveMessage`

```csharp
public record WsServerEvent<T>(
    string Event,
    T Data,
    DateTime Timestamp
);
```

- Ở hướng server -> client, project dùng một method thống nhất là `ReceiveMessage`.
- Tất cả payload server gửi về đều được bọc trong `WsServerEvent<T>`.
- Envelope này gồm:
  - `Event`: tên event để client biết loại message.
  - `Data`: dữ liệu thực tế của event.
  - `Timestamp`: thời điểm server tạo message.
- Các event tiêu biểu:
  - `Connected`: connection vào game thành công.
  - `Ack`: server xác nhận command đã được nhận/xử lý.
  - `state_snapshot`: snapshot trạng thái hiện tại.
  - `gameplay_event`: diễn biến trong trận.
  - `match_ended`: trận kết thúc.
- Cách làm này giúp client chỉ cần listen `ReceiveMessage`, sau đó switch theo trường `Event`.

**Ý chính cần hiểu:** `ReceiveMessage` là cổng nhận message thống nhất ở phía client.

---

## Slide 22: Broadcast event bằng `IHubContext`

```csharp
hubContext.Clients.Group(GetRoomGroup(roomCode))
    .SendAsync("ReceiveMessage", payload);
```

- Không phải message SignalR nào cũng được gửi trực tiếp từ method trong `GameHub`.
- Trong project, `RuntimeTickWorker` là background worker, không kế thừa `Hub`, nhưng vẫn cần broadcast event realtime.
- ASP.NET Core cung cấp `IHubContext<GameHub>` để code bên ngoài Hub vẫn gửi được SignalR message.
- Worker dùng `Clients.Group(...)` để gửi event vào đúng room group.
- Pattern này hữu ích vì:
  - Hub không cần chứa toàn bộ logic phát event;
  - background worker vẫn có thể push message realtime;
  - broadcast vẫn dùng cùng protocol `ReceiveMessage`;
  - phạm vi gửi vẫn được kiểm soát bằng group room.

**Ý chính cần hiểu:** `IHubContext` là cầu nối để các service/worker ngoài Hub vẫn gửi được message qua SignalR.

---

## Slide 23: Snapshot, reconnect và server time

- `state_snapshot` là một message SignalR được gửi qua `ReceiveMessage`.
- Server gửi snapshot trong các trường hợp:
  - client vừa connect thành công;
  - client gửi command `ReconnectMatch`;
  - client gửi command `RequestStateSnapshot`.
- Snapshot giúp client đồng bộ lại giao diện sau khi mới vào trận, mất kết nối, hoặc nghi ngờ thiếu event.
- `RequestServerTime` cũng đi qua `SendCommand`. Server trả `ServerTime` cho đúng client gọi bằng `Clients.Caller`.
- `ServerTime` giúp client ước lượng lệch giờ giữa máy client và server khi hiển thị countdown.
- Điểm quan trọng là các chức năng này đều tận dụng cùng một connection SignalR, không cần mở REST endpoint riêng cho từng thao tác nhỏ.

**Ý chính cần hiểu:** Reconnect, snapshot và server time đều là ví dụ project dùng SignalR để đồng bộ client trong thời gian thực.

---

## Slide 24: Tổng kết

- Phần 1 đã giới thiệu SignalR như một realtime abstraction của ASP.NET Core.
- Các khái niệm quan trọng gồm:
  - Hub là điểm vào realtime ở server.
  - Client invoke method để gửi dữ liệu lên server.
  - Server dùng `SendAsync` để gọi method ở client.
  - Group giúp broadcast đúng phạm vi.
  - Lifecycle method giúp kiểm soát connection khi connect/disconnect.
- Trong Memesploding Game Server:
  - `/ws` là endpoint SignalR.
  - `GameHub` quản lý connection lifecycle.
  - `SendCommand` là hướng client -> server.
  - `ReceiveMessage` là hướng server -> client.
  - `room:{ROOM_CODE}` là group cho từng phòng game.
  - `IHubContext<GameHub>` giúp worker broadcast event realtime.
- Kết luận: SignalR không thay thế game engine. Nó là lớp realtime transport giúp Game Server giao tiếp với client rõ ràng, có kiểm soát và phù hợp với kiến trúc ASP.NET Core.

**Ý chính cần hiểu:** Nếu phải tóm tắt trong một câu, Memesploding dùng SignalR để biến mỗi room game thành một kênh realtime hai chiều giữa Game Server và các client trong phòng.

