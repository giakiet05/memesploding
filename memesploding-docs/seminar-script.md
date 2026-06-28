# Lời dẫn thuyết trình: SignalR và Game Server Memesploding

## Slide 1: Tiêu đề

Hôm nay em xin trình bày về SignalR và cách công nghệ này được ứng dụng trong game server realtime, lấy project Memesploding làm ví dụ thực tế.

Bài trình bày được chia thành hai phần. Phần đầu giới thiệu SignalR, bao gồm khái niệm, cơ chế hoạt động và cú pháp cơ bản. Phần thứ hai đi vào cách SignalR được triển khai trong Memesploding Game Server.

## Slide 2: Mục tiêu bài trình bày

Mục tiêu đầu tiên là hiểu SignalR giải quyết bài toán gì trong các ứng dụng realtime.

Mục tiêu thứ hai là nắm được cách SignalR hoạt động ở mức tổng quan: client kết nối tới Hub, gọi method trên server, và server đẩy message ngược lại client.

Mục tiêu cuối cùng là phân tích implementation cụ thể trong project ở góc nhìn SignalR, gồm `GameHub`, endpoint `/ws`, group theo room, `SendCommand`, `ReceiveMessage`, `IHubContext`, snapshot và reconnect.

## Slide 3: Bài toán realtime trong ứng dụng hiện đại

Trong nhiều ứng dụng hiện đại, dữ liệu không chỉ đi theo chiều client request rồi server response. Có nhiều trường hợp server cần chủ động gửi dữ liệu về client.

Ví dụ như chat, notification, dashboard realtime, ứng dụng cộng tác nhiều người dùng, và đặc biệt là game online.

Nếu chỉ dùng HTTP request-response thông thường, client thường phải polling liên tục. Cách này tốn tài nguyên, có độ trễ cao hơn, và không phù hợp với những tình huống cần phản hồi gần như ngay lập tức.

## Slide 4: SignalR là gì?

SignalR là thư viện realtime của ASP.NET Core. Công nghệ này cho phép giao tiếp hai chiều giữa client và server.

Điểm khác biệt so với HTTP API thông thường là server có thể chủ động gọi method trên client, thay vì chỉ trả lời khi client gửi request.

SignalR cũng trừu tượng hoá phần connection và transport, giúp lập trình viên làm việc với Hub và method thay vì phải tự xử lý WebSocket raw ở mức thấp.

## Slide 5: SignalR giải quyết vấn đề gì?

SignalR giúp giảm đáng kể phần code phải tự viết khi xây dựng realtime communication. Thay vì tự quản lý socket, message framing, danh sách connection và broadcast, ta dùng mô hình Hub có sẵn.

SignalR hỗ trợ gửi message đến nhiều phạm vi khác nhau: một connection cụ thể, một user, một group hoặc tất cả client.

Ngoài ra, SignalR tích hợp tốt với hệ sinh thái ASP.NET Core như dependency injection, authentication, authorization và logging.

## Slide 6: Cơ chế hoạt động tổng quan

Cơ chế hoạt động của SignalR có thể hiểu theo một luồng cơ bản.

Đầu tiên, client kết nối tới một Hub endpoint. Sau đó client và server thực hiện handshake để thống nhất protocol, ví dụ JSON protocol.

Khi connection đã được mở, client có thể invoke method trên Hub. Ngược lại, Hub có thể dùng `Clients` để gửi message về client. Tuỳ phạm vi cần gửi, server có thể dùng `Clients.All`, `Clients.Caller` hoặc `Clients.Group(...)`.

## Slide 7: Transport và fallback

Bên dưới SignalR, transport được ưu tiên là WebSockets vì WebSockets hỗ trợ kết nối hai chiều hiệu quả.

Tuy nhiên, SignalR không bắt buộc lập trình viên phải tự viết logic fallback. Nếu môi trường không hỗ trợ WebSockets, SignalR có thể fallback sang Server-Sent Events hoặc Long Polling.

Điểm quan trọng là dù transport bên dưới thay đổi, code ở tầng Hub vẫn gần như giữ nguyên.

## Slide 8: Cú pháp server - khai báo Hub

Slide này minh hoạ cú pháp khai báo một Hub đơn giản.

Một Hub là một class kế thừa từ `Hub`. Các public method trong Hub có thể được client gọi thông qua SignalR connection.

Trong ví dụ, client có thể gọi method `SendMessage`. Bên trong method đó, server dùng `Clients.All.SendAsync("ReceiveMessage", ...)` để gọi method `ReceiveMessage` trên tất cả client đang kết nối.

## Slide 9: Cú pháp server - map endpoint và cấu hình

Để dùng SignalR trong ASP.NET Core, ta cần đăng ký service bằng `builder.Services.AddSignalR()`.

Sau đó, trong pipeline của ứng dụng, ta map Hub vào một endpoint cụ thể bằng `app.MapHub<ChatHub>("/chat")`.

Kể từ thời điểm này, client có thể kết nối tới endpoint `/chat` để bắt đầu realtime communication với Hub.

## Slide 10: Cú pháp client - kết nối và gọi Hub

Ở phía client JavaScript, ta tạo connection bằng `HubConnectionBuilder`, truyền URL của Hub vào `withUrl`.

Client dùng `connection.on("ReceiveMessage", handler)` để đăng ký method mà server có thể gọi.

Sau khi gọi `connection.start()`, client có thể gọi method trên server bằng `connection.invoke("SendMessage", ...)`. Đây là cách client gửi dữ liệu lên Hub.

## Slide 11: Group trong SignalR

Group là một cơ chế rất quan trọng trong SignalR. Group cho phép gom nhiều connection vào cùng một kênh logic.

Ví dụ, một chat room hoặc một game room có thể tương ứng với một group.

Khi server gửi message bằng `Clients.Group("room:ABC123")`, chỉ những connection trong group đó nhận được message. Điều này giúp giới hạn phạm vi broadcast và tránh gửi dữ liệu không liên quan tới client khác.

## Slide 12: Authentication và connection lifecycle

Trong thực tế, realtime connection thường cần xác thực. Hub có thể đọc thông tin từ HTTP context khi client connect, ví dụ token trong header hoặc query string.

SignalR Hub cũng có hai lifecycle method quan trọng là `OnConnectedAsync` và `OnDisconnectedAsync`.

Các method này thường được dùng để validate quyền truy cập, lưu connection context, add hoặc remove group, và cập nhật trạng thái online/offline.

## Slide 13: Khi nào nên dùng SignalR?

SignalR phù hợp khi ứng dụng cần realtime hai chiều và backend dùng ASP.NET Core.

Các use case phổ biến gồm chat, notification, dashboard realtime, lobby nhiều người chơi, hoặc game server theo lượt.

Tuy nhiên, một điểm cần lưu ý là không nên đưa toàn bộ business logic vào Hub. Hub nên giữ vai trò transport layer, còn domain logic nên nằm trong service, runtime hoặc application layer riêng.

## Slide 14: Bối cảnh project Memesploding

Sang phần thứ hai, em sẽ trình bày cách project Memesploding áp dụng SignalR trong Game Server.

Ở phần này, em không đi sâu vào luật chơi hay cách game runtime xử lý state. Trọng tâm chỉ là SignalR đang được dùng ở đâu, client kết nối như thế nào, server gửi message ra sao, và room được map thành group như thế nào.

Các câu hỏi chính là: client kết nối vào endpoint nào, token được truyền qua đâu, command đi qua Hub method nào, và server push event về client bằng method nào.

## Slide 15: SignalR nằm ở đâu trong Game Server?

Trong Game Server, SignalR xuất hiện đầu tiên ở `Program.cs`, nơi server đăng ký SignalR và map `GameHub` vào endpoint `/ws`.

`GameHub` là Hub chính cho realtime gameplay. Đây là nơi nhận connection, nhận command từ client, đưa connection vào group, và gửi một số message trực tiếp cho client vừa kết nối.

Ngoài Hub, project còn dùng `WsClientCommand` để định dạng command client gửi lên, `WsServerEvent<T>` để định dạng message server gửi về, và `IHubContext<GameHub>` để background worker vẫn có thể gửi SignalR message.

## Slide 16: Endpoint `/ws` trong Game Server

Trong implementation hiện tại, Game Server đăng ký SignalR bằng `AddSignalR()` và map `GameHub` vào `/ws`.

Client kết nối vào endpoint này bằng URL có kèm `access_token`. Token này là game ticket do API Server cấp khi trận đấu bắt đầu.

Game Server dùng JSON protocol cho payload SignalR. Ngoài ra, server cũng cấu hình giới hạn kích thước message và CORS credential để connection realtime hoạt động được từ client.

## Slide 17: Cách truyền token qua SignalR connection

Khi client connect SignalR, token được truyền qua query string theo dạng `/ws?access_token=<gameTicket>`.

Trong `GameHub.OnConnectedAsync`, server đọc `access_token` từ HTTP context của SignalR connection.

Token này được đưa vào `GameTicketValidator` để validate. Các claim quan trọng gồm `scope = game_ws`, `sub` là userId, `room_code` là mã phòng, và `match_id` nếu có.

Nếu token không hợp lệ, server gọi `Context.Abort()` để ngắt connection. Nếu hợp lệ, server tạo `GameConnectionContext` để biết SignalR connection này thuộc user, room và match nào.

## Slide 18: Lifecycle SignalR connection trong `GameHub`

Khi connection hợp lệ, Hub lưu context theo `Context.ConnectionId` và add connection vào SignalR group của room.

Sau đó server gửi event `Connected` cho chính client vừa kết nối bằng `Clients.Caller`, rồi gửi tiếp `state_snapshot` cũng cho `Clients.Caller`.

Khi client disconnect, Hub xoá connection context và remove connection khỏi group. Đây là hai lifecycle chính mà project dùng để quản lý realtime connection.

## Slide 19: SignalR group theo room

Trong project, mỗi room game được ánh xạ thành một SignalR group theo format `room:{ROOM_CODE}`.

Khi player connect thành công, connection của player được add vào group của room tương ứng. Khi server cần broadcast event của trận, server chỉ gửi vào group đó.

Điểm này rất phù hợp với game server, vì mỗi trận chỉ nên nhận event của chính trận đó. SignalR group giúp project không phải tự quản lý thủ công danh sách connection trong từng phòng.

## Slide 20: Client gọi server qua `SendCommand`

Ở hướng client gửi lên server, project dùng Hub method tên là `SendCommand`.

Payload gửi lên là `WsClientCommand`, gồm `Event` và `Data`. `Event` là tên command, ví dụ `DrawCard`, `PlayCard`, `Nope`, còn `Data` là JSON payload đi kèm.

Từ góc nhìn SignalR, đây chính là cú pháp client invoke server method. Client không gọi từng method riêng cho từng hành động, mà gom vào một method chung là `SendCommand`.

## Slide 21: Server push về client qua `ReceiveMessage`

Ở hướng server gửi về client, project dùng một method thống nhất là `ReceiveMessage`.

Mọi message server push đều được bọc trong `WsServerEvent<T>`, gồm `Event`, `Data` và `Timestamp`.

Ví dụ, khi vừa connect, client nhận `Connected`. Khi cần đồng bộ lại trạng thái, client nhận `state_snapshot`. Khi có diễn biến trong trận, client nhận `gameplay_event`. Cách này giúp client chỉ cần listen một method SignalR, rồi phân loại message bằng trường `Event`.

## Slide 22: Broadcast event bằng `IHubContext`

Một điểm đáng chú ý trong implementation là không phải message SignalR nào cũng được gửi trực tiếp từ bên trong `GameHub`.

`RuntimeTickWorker` không nằm trong Hub, nhưng vẫn gửi được SignalR message bằng cách inject `IHubContext<GameHub>`.

Từ `IHubContext`, worker gọi `Clients.Group(room).SendAsync("ReceiveMessage", payload)`. Như vậy, broadcast realtime vẫn dùng đúng SignalR group, kể cả khi message không xuất phát từ một Hub method do client gọi.

## Slide 23: Snapshot, reconnect và server time

Trong project, snapshot và reconnect cũng được thể hiện qua SignalR message.

Khi client vừa connect, gọi `ReconnectMatch`, hoặc gọi `RequestStateSnapshot`, server gửi lại `state_snapshot` qua `ReceiveMessage`.

Ngoài ra, client có thể gửi `RequestServerTime` thông qua `SendCommand`. Server trả `ServerTime` cho chính connection đó bằng `Clients.Caller`. Đây là ví dụ rõ ràng về việc dùng SignalR cho request realtime nhỏ nhưng không cần REST API riêng.

## Slide 24: Tổng kết

Tóm lại, SignalR cung cấp một abstraction rất thuận tiện cho realtime communication trong ASP.NET Core.

Ở phần lý thuyết, các thành phần cốt lõi gồm Hub, endpoint, client invoke, server send, group và lifecycle connection.

Trong Memesploding, SignalR được dùng đúng vai trò realtime transport: `/ws` là endpoint, `GameHub` quản lý connection, `SendCommand` là hướng client gọi server, `ReceiveMessage` là hướng server gọi client, và group theo room giúp broadcast đúng phạm vi.

Bài học chính là SignalR không thay thế game engine. SignalR là lớp giao tiếp realtime giúp Game Server nói chuyện với client một cách rõ ràng, có kiểm soát và phù hợp với kiến trúc ASP.NET Core.
