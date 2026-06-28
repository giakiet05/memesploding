# ADR 0001: Sử dụng cấu trúc Monorepo cho toàn bộ dự án Memesploding

## Trạng thái
Đã chấp thuận (Accepted)

## Bối cảnh
Dự án Memesploding bao gồm nhiều thành phần khác nhau: Máy chủ API (REST), Máy chủ Game (WebSocket/Real-time), và các tài liệu đi kèm. Việc quản lý các thành phần này trong các kho lưu trữ (Repository) riêng biệt gây khó khăn trong việc đồng bộ hóa mã nguồn, chia sẻ các định nghĩa dữ liệu (Data Models/Interfaces) và quản lý quy trình triển khai (CI/CD).

## Quyết định
Chúng tôi quyết định sử dụng cấu trúc **Monorepo** (một kho lưu trữ duy nhất) cho toàn bộ mã nguồn của dự án Memesploding.

### Cấu trúc thư mục dự kiến:
```text
/memesploding
├── memesploding-docs/       # Tài liệu dự án (ADRs, Requirements, Diagrams)
├── memesploding-server/     # Mã nguồn Backend (Node.js/Go...)
│   ├── apps/
│   │   ├── api/             # API Server (Auth, Profile, Social)
│   │   └── game/            # Game Server (Real-time logic, WebSockets)
│   └── packages/            # Thư viện dùng chung (Shared types, Utils, Core logic)
└── memesploding-client/     # Mã nguồn Frontend (nếu có hoặc bản mẫu Web)
```

## Hệ quả
* **Ưu điểm:**
    * Dễ dàng chia sẻ các `Types` và `Interfaces` giữa API Server và Game Server, đảm bảo tính nhất quán của dữ liệu.
    * Quy trình phát triển (Development Workflow) tập trung, chỉ cần một lần `git clone` để có toàn bộ dự án.
    * Thuận tiện trong việc thực hiện các thay đổi có ảnh hưởng đến nhiều thành phần cùng lúc (Atomic commits).
* **Nhược điểm:**
    * Dung lượng kho lưu trữ sẽ tăng nhanh theo thời gian.
    * Cần cấu hình CI/CD thông minh để chỉ build những phần có thay đổi nhằm tối ưu thời gian.

## Ghi chú
Sẽ sử dụng các công cụ quản lý Monorepo phù hợp (như TurboRepo/Nx cho Node.js hoặc Go Workspaces) tùy thuộc vào ngôn ngữ lập trình được chọn cuối cùng.
