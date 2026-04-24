# 🎮 Memesploding Game Flow Test - Hướng Dẫn Nhanh

## 📋 Những gì mình vừa fix

✅ **GameplaySetup.http** - Cập nhật dùng env variables thay vì hardcode URL  
✅ **GameplayCommands.md** - Fix WS URL (5217 thay vì 5204) + thêm guide tự động hóa  
✅ **http-client.env.json** - Tạo mới, match với env của API server  
✅ **run-gameplay-tests.sh** - Bash script tự động hóa (curl + websocat)  
✅ **README.md** - Tài liệu hoàn chỉnh

---

## 🚀 Start Here - 3 Cách Test

### **Cách 1: Manual với Rider (Dễ nhất - để debug)**

1. Mở file `GameplaySetup.http` trong Rider
2. Chọn environment `dev` ở dropdown (top right)
3. Chạy requests **theo thứ tự:**
   ```
   Phase 1: aliceLogin → bobLogin → charlieLogin
   Phase 2: loadCardSets → createRoom → bobJoin → charlieJoin
   Phase 3: aliceReady → bobReady → charlieReady → startMatch
   Phase 4: getAliceTicket → getBobTicket → getCharlieTicket
   ```
4. **Kiếm WebSocket URLs từ Phase 4:**
   - Click vào response của `getAliceTicket`
   - Copy `aliceWsUrl` variable (dạng: `ws://localhost:5217/ws?access_token=...`)
   - Dùng nó trong WebSocketKing hoặc wscat

5. **Game Actions - Copy từ GameplayCommands.md:**
   - Mở WebSocketKing
   - Paste WS URL vào
   - Send handshake: `{"protocol":"json","version":1}`
   - Copy-paste commands từ file (draw card, play card, nope, etc.)

---

### **Cách 2: Tự động hóa với Bash (Recommended)**

```bash
# 1. Install websocat nếu chưa có
sudo snap install websocat

# 2. Chạy test script (tự động từ login tới game)
cd /path/to/Memesploding.Game/Tests/Gameplay
chmod +x run-gameplay-tests.sh
./run-gameplay-tests.sh

# 3. Xem kết quả
cat output/gameplay-test-report.txt
cat output/game-urls.txt  # WS URLs để test manual nếu cần
```

**Output sẽ show:**
- ✅/❌ Mỗi bước (login, create room, join, ready, start, tickets)
- Game URLs để manual test
- Pass rate

---

### **Cách 3: Manual WebSocket với wscat**

```bash
# 1. Install wscat
npm install -g wscat

# 2. Run GameplaySetup.http tới Phase 4 (trong Rider) để lấy token
# 3. Copy aliceWsUrl từ Phase 4 response

# 4. Connect
wscat -c "ws://localhost:5217/ws?access_token=YOUR_TOKEN_HERE"

# 5. Handshake (bắt buộc)
> {"protocol":"json","version":1}

# 6. Draw card
> {"type":1,"target":"SendCommand","arguments":[{"Event":"drawcard","Data":{}}]}

# 7. Xem more commands trong GameplayCommands.md
```

---

## 📁 File Structure

```
Memesploding.Game/Tests/Gameplay/
├── GameplaySetup.http          ← Setup flow: login → room → join → ready → start → tickets
├── GameplayCommands.md         ← WS commands & payloads
├── http-client.env.json        ← Env config (match API server)
├── run-gameplay-tests.sh       ← Auto script (curl + websocat)
└── README.md                   ← Full docs
```

---

## ⚙️ Environment Config

File `http-client.env.json` đã setup sẵn:
- **Dev:** `http://localhost:5217/api/v1` + `ws://localhost:5217/ws`
- **Prod:** `https://api.memesploding.com/api/v1` + `wss://api.memesploding.com/ws`

Switch trong Rider: Dropdown top right trước khi run requests

---

## 🧪 Test Scenarios

### Happy Path (All pass)
```
Login 3 users → Create room → All join → All ready → Start match → Get tickets → WS connect
```

### Error Cases (Edge cases)
- Duplicate device ID
- Invalid room code
- Player joins after start
- Invalid token
- WebSocket handshake timeout

---

## 🔗 Related Files

- **API Server Tests:** `../../../Memesploding.Api/HttpTests/`
- **API Room Docs:** `../../../Memesploding.Api/HttpTests/Room.HappyPath.http`
- **Game Server Config:** `../../../Memesploding.Game/Program.cs`

---

## ✅ Checklist

- [ ] Server running? (check with: `curl http://localhost:5217/health`)
- [ ] PostgreSQL running? (check: `sudo service postgresql status`)
- [ ] Redis running? (check: `redis-cli ping`)
- [ ] First time? Try **Cách 1** (Rider manual)
- [ ] Then automate with **Cách 2** (bash script)

---

## 💡 Tips

**For Rider users:**
- Use Ctrl+Enter to run individual request
- Use Ctrl+Alt+Enter to run all in file
- Hover over variables to see extracted values
- Right-click response → Copy formatted JSON

**For bash script users:**
- Edit `HTTP_BASE_URL` + `WS_BASE_URL` for different servers
- Check `output/` folder for reports
- Script includes auto-cleanup

---

## 🆘 Quick Troubleshooting

| Error | Fix |
|-------|-----|
| "Connection refused" | Start server: `cd Memesploding.Api && dotnet run` |
| "Database error" | Check PostgreSQL: `sudo service postgresql status` |
| "Redis error" | Start Redis: `sudo service redis-server start` |
| "WebSocket timeout" | Check port 5217 is open, verify token not expired |
| "jq: command not found" | Install: `sudo apt install jq` |

---

**Ready to test! Bắt đầu từ Cách 1 hoặc Cách 2 nhé! 🚀**
