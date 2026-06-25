import { useState, useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";
import "./App.css";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || "http://localhost:5217/api/v1";
const DEFAULT_WS_URL = import.meta.env.VITE_DEFAULT_WS_URL || "ws://localhost:5204/ws";

function toSignalRUrl(url) {
  if (url.startsWith("ws://")) return "http://" + url.slice("ws://".length);
  if (url.startsWith("wss://")) return "https://" + url.slice("wss://".length);
  return url;
}

const CARD_COLORS = {
  ExplodingKitten: "#7f1d1d",
  Defuse: "#065f46",
  Attack: "#92400e",
  TargetedAttack: "#92400e",
  PersonalAttack: "#78350f",
  Nope: "#831843",
  Skip: "#1e3a8a",
  SuperSkip: "#1e40af",
  Favor: "#4c1d95",
  Shuffle: "#374151",
  SeeTheFuture: "#0c4a6e",
  AlterTheFuture: "#0c4a6e",
  DrawFromBottom: "#14532d",
  Reverse: "#1a202c",
  Cat1: "#713f12",
  Cat2: "#713f12",
  Cat3: "#713f12",
  Cat4: "#713f12",
  Cat5: "#713f12",
  FeralCat: "#78350f",
  Bury: "#1c1917",
  BarkingKitten: "#164e63",
  StreakingKitten: "#7c2d12",
  GarbageCollection: "#1c1917",
};

const ALL_CARD_CODES = [
  "Nope",
  "Attack",
  "Skip",
  "Favor",
  "Shuffle",
  "SeeTheFuture",
  "AlterTheFuture",
  "DrawFromBottom",
  "Reverse",
  "Cat1",
  "Cat2",
  "Cat3",
  "Cat4",
  "Cat5",
  "Defuse",
];

function decodeToken(t) {
  try {
    const b = t.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(
      decodeURIComponent(
        atob(b)
          .split("")
          .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
          .join(""),
      ),
    ).sub;
  } catch {
    return "";
  }
}

export default function App() {
  const [token, setToken] = useState("");
  const [apiToken, setApiToken] = useState("");
  const [wsUrl, setWsUrl] = useState(DEFAULT_WS_URL);
  const [status, setStatus] = useState("Disconnected");
  const [gameState, setGameState] = useState(null);
  const [logs, setLogs] = useState([]);
  const [myUserId, setMyUserId] = useState("");
  const [selectedCards, setSelectedCards] = useState([]);
  const [showComboModal, setShowComboModal] = useState(false);
  const [comboPayload, setComboPayload] = useState({
    targetUserId: "",
    requestedCardCode: "",
    discardCardCode: "",
  });
  const [futurePeek, setFuturePeek] = useState(null); // { cards: [] }
  const [favorChoice, setFavorChoice] = useState("");

  const connRef = useRef(null);
  const logsEndRef = useRef(null);

  const addLog = (msg) =>
    setLogs((prev) =>
      [...prev, `[${new Date().toLocaleTimeString()}] ${msg}`].slice(-100),
    );

  useEffect(() => {
    logsEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [logs]);

  const normalizeEvent = (eventName) =>
    (eventName || "").replace(/_/g, "").toLowerCase();

  const connect = async (overrideToken = token, overrideWsUrl = wsUrl) => {
    if (!overrideToken) return alert("Nhập token đi!");
    if (connRef.current) {
      try {
        await connRef.current.stop();
      } catch (err) {
        addLog("Lỗi khi ngắt kết nối cũ: " + err);
      }
    }

    const uid = decodeToken(overrideToken);
    setMyUserId(uid);

    const signalRUrl = toSignalRUrl(overrideWsUrl);

    const c = new signalR.HubConnectionBuilder()
      .withUrl(`${signalRUrl}?access_token=${overrideToken}`, {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    c.on("ReceiveMessage", (msg) => {
      const eventName = normalizeEvent(msg.event);
      if (eventName === "statesnapshot") {
        setGameState(msg.data);
        addLog(`Snapshot v${msg.data.stateVersion}`);
      } else if (eventName === "gameplayevent") {
        const type = normalizeEvent(msg.data.type);
        if (type === "unknowncommand") return;
        const p =
          typeof msg.data.payload === "string"
            ? JSON.parse(msg.data.payload || "{}")
            : msg.data.payload;
        const pStr = JSON.stringify(p);
        addLog(`[${msg.data.type}] ${pStr}`);
        if (type === "actionrejected") {
          const reason = p?.reason;
          if (reason === "NOT_TURN" || reason === "not_turn")
            alert("Chưa tới lượt mày!");
          else if (reason === "CAT_CARD_MUST_BE_COMBO" || reason === "cat_card_must_be_combo")
            alert("Lá Cat phải đánh Combo 2/3/5 lá!");
          else alert("Lỗi: " + reason);
        }

        if (type === "futurepeeked") {
          if (p.userId === myUsId) {
            setFuturePeek(p);
          }
        }

        if (type === "catcombotworesolved" || type === "favorresolved") {
          // Notify requester what they got
          if (p.to === myUsId) {
            const card = p.cardCode || "một lá bài";
            const fromName =
              gs?.players?.find((pl) => pl.userId === p.from)?.nickname ||
              "đối thủ";
            alert(`🎁 Mày cướp được lá [${card}] từ ${fromName}!`);
          }
          // Notify victim what was taken
          if (p.from === myUsId) {
            const card = p.cardCode || "một lá bài";
            const toName =
              gs?.players?.find((pl) => pl.userId === p.to)?.nickname ||
              "đối thủ";
            alert(`💸 Mày bị ${toName} cướp mất lá [${card}]!`);
          }
        }
        // After gameplay event, refresh state ONCE
        c.invoke("SendCommand", {
          Event: "requeststatesnapshot",
          Data: {},
        }).catch(() => {});
      } else if (eventName === "error") {
        addLog(`Error: ${msg.data.message}`);
      } else if (eventName === "connected") {
        setStatus("Connected");
        addLog("Kết nối thành công!");
      } else if (eventName === "ack") {
        addLog(`Ack v${msg.data.stateVersion}`);
      }
    });

    c.onclose(() => setStatus("Disconnected"));

    try {
      await c.start();
      connRef.current = c;
    } catch (err) {
      addLog("Lỗi kết nối: " + err);
    }
  };

  const startBotTest = async () => {
    if (!apiToken) return alert("Dán API accessToken trước!");

    try {
      addLog("Đang tạo bot test match...");
      const res = await fetch(`${API_BASE_URL}/test-matches/bot`, {
        method: "POST",
        headers: {
          Authorization: `Bearer ${apiToken}`,
        },
      });

      const rawBody = await res.text();
      const body = rawBody ? JSON.parse(rawBody) : null;
      if (!res.ok) {
        throw new Error(body?.message || rawBody || `HTTP ${res.status}`);
      }

      if (!body?.data?.connection?.wsAccessToken) {
        throw new Error("Response thiếu data.connection.wsAccessToken");
      }

      const wsAccessToken = body.data.connection.wsAccessToken;
      const gameWsUrl = body.data.connection.wsUrl || DEFAULT_WS_URL;
      setToken(wsAccessToken);
      setWsUrl(gameWsUrl);
      addLog(`Bot test match ${body.data.roomCode} đã tạo.`);
      await connect(wsAccessToken, gameWsUrl);
    } catch (err) {
      addLog("Lỗi tạo bot test match: " + err.message);
      alert("Không tạo được bot test match: " + err.message);
    }
  };

  const sendCmd = (event, data) => {
    const c = connRef.current;
    if (!c || c.state !== signalR.HubConnectionState.Connected)
      return alert("Chưa kết nối!");
    c.invoke("SendCommand", { Event: event, Data: data }).catch((err) =>
      addLog("Lỗi: " + err),
    );
  };

  const toggleCard = (code, idx) => {
    const id = `${code}-${idx}`;
    setSelectedCards((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id],
    );
  };

  const playSelected = () => {
    if (selectedCards.length === 0) return alert("Chọn bài trước!");
    const codes = selectedCards.map((c) => c.split("-")[0]);
    const uniq = [...new Set(codes)];

    if (codes.length === 1) {
      const card = codes[0];
      if (card === "Favor") {
        setShowComboModal(true);
        return;
      }
      sendCmd("playcard", { cardCode: card });
      setSelectedCards([]);
      return;
    }

    // Universal combos
    const isSameType =
      uniq.length === 1 && (codes.length === 2 || codes.length === 3);
    const is5diff = codes.length === 5 && uniq.length === 5;

    if (isSameType || is5diff) {
      setShowComboModal(true);
      return;
    }
    alert(
      "Combo không hợp lệ! Combo 2/3 lá phải cùng loại, hoặc combo 5 lá khác nhau.",
    );
  };

  const commitCombo = () => {
    const codes = selectedCards.map((c) => c.split("-")[0]);
    const uniq = [...new Set(codes)];
    const isFavor = codes.length === 1 && codes[0] === "Favor";
    const is5diff = codes.length === 5 && uniq.length === 5;

    let payload;
    if (isFavor) {
      payload = { cardCode: "Favor", targetUserId: comboPayload.targetUserId };
    } else if (is5diff) {
      payload = {
        cardCode: codes[0], // doesn't matter much for combo 5
        comboSize: 5,
        cardCodes: codes,
        discardCardCode: comboPayload.discardCardCode,
      };
    } else {
      payload = {
        cardCode: uniq[0],
        comboSize: codes.length,
        ...(comboPayload.targetUserId && {
          targetUserId: comboPayload.targetUserId,
        }),
        ...(comboPayload.requestedCardCode && {
          requestedCardCode: comboPayload.requestedCardCode,
        }),
      };
    }
    sendCmd("playcard", payload);
    setShowComboModal(false);
    setSelectedCards([]);
    setComboPayload({
      targetUserId: "",
      requestedCardCode: "",
      discardCardCode: "",
    });
  };

  const gs = gameState;
  const myUsId = myUserId;
  const myPlayerInfo = gs?.players?.find((p) => p.userId === myUsId);
  const isMyTurn = gs?.players?.[gs?.turnIndex]?.userId === myUsId;
  const pendingDraws = myPlayerInfo?.pendingDrawCount ?? 1;
  const alivePlayers =
    gs?.players?.filter(
      (p) => p.lifeState !== "Eliminated" && p.userId !== myUsId,
    ) || [];
  const showNope =
    gs?.reactionWindowEndsAt && new Date(gs.reactionWindowEndsAt) > new Date();
  const lastDiscard = gs?.discardPile?.length
    ? gs.discardPile[gs.discardPile.length - 1]
    : null;

  return (
    <div className="app-wrapper">
      <header>
        <div className="header-title">💥 Memesploding Arena</div>
        <div className="connect-bar">
          <input
            value={apiToken}
            onChange={(e) => setApiToken(e.target.value)}
            placeholder="API accessToken để Start Bot Test..."
          />
          <button onClick={startBotTest}>Start Bot Test</button>
          <input
            value={wsUrl}
            onChange={(e) => setWsUrl(e.target.value)}
            placeholder="Game WS URL..."
          />
          <input
            value={token}
            onChange={(e) => setToken(e.target.value)}
            placeholder="Game wsAccessToken..."
          />
          <button onClick={connect}>
            {status === "Connected" ? "Reconnect" : "Connect"}
          </button>
          <span
            className={`status-badge ${status === "Connected" ? "green" : "red"}`}
          >
            {status}
          </span>
        </div>
      </header>

      <div className="main-content">
        {/* ===== GAME AREA ===== */}
        <div className="game-area">
          {/* NOPE ALERT BANNER */}
          {showNope && (
            <div className="nope-banner">
              ⚡{" "}
              <b>
                {gs.pendingReactionUserId === myUsId
                  ? "Đang chờ phản ứng của đối thủ..."
                  : `${gs.players?.find((p) => p.userId === gs.pendingReactionUserId)?.nickname || "??"} vừa đánh [${gs.pendingReactionAction}]!`}
              </b>
              &nbsp;— Mày có muốn bắn NOPE không?
              <button
                className="btn-nope"
                onClick={() => sendCmd("playcard", { cardCode: "Nope" })}
              >
                NOPE!
              </button>
            </div>
          )}

          {/* BOARD INFO */}
          <section className="board-panel">
            <div className="game-stats">
              <div className="stat-box">
                <span className="stat-label">Phase</span>
                <span className="stat-value">{gs?.phase || "Setup"}</span>
              </div>
              <div className="stat-box">
                <span className="stat-label">Lượt Của</span>
                <span
                  className="stat-value"
                  style={{ color: isMyTurn ? "#10b981" : "#f59e0b" }}
                >
                  {gs?.players?.[gs?.turnIndex]?.nickname || "..."}
                  {isMyTurn ? " 👈 MÀY!" : ""}
                </span>
              </div>
              <div className="stat-box">
                <span className="stat-label">Lượt Cần Rút</span>
                <span
                  className="stat-value"
                  style={{ color: pendingDraws > 1 ? "#ef4444" : "#10b981" }}
                >
                  {pendingDraws}
                </span>
              </div>
            </div>

            {/* Players */}
            <div className="players-row">
              {gs?.players?.map((p) => (
                <div
                  key={p.userId}
                  className={`player-card ${p.lifeState === "Eliminated" ? "dead" : ""} ${p.userId === myUsId ? "me" : ""}`}
                >
                  <div className="player-name">
                    {p.lifeState === "Eliminated" ? "💀" : "💖"} {p.nickname}
                  </div>
                  <div className="player-cards-count">🃏 {p.handCount} lá</div>
                  <div className="player-sub">
                    {p.pendingDrawCount > 1
                      ? `🎯 Phải rút ${p.pendingDrawCount}`
                      : ""}
                  </div>
                  <div className="player-id" title={p.userId}>
                    {p.userId.slice(0, 8)}...
                  </div>
                </div>
              ))}
            </div>

            {/* Play Mat */}
            <div className="play-mat">
              <div
                className="deck draw"
                onClick={() => sendCmd("drawcard", {})}
              >
                <div className="deck-icon">🃏</div>
                <div className="deck-label">DRAW</div>
                <div className="deck-count">
                  {gs?.drawPileCount ?? "?"} lá còn
                </div>
              </div>
              <div className="deck discard">
                <div className="deck-icon">{lastDiscard || "🗑️"}</div>
                <div className="deck-label">DISCARD</div>
                <div className="deck-count">
                  {gs?.discardPile?.length ?? 0} lá
                </div>
              </div>
            </div>
          </section>

          {/* HAND */}
          <section className="hand-panel">
            <div className="hand-header">
              <span>
                🖐️ Tay bài của <b>{myPlayerInfo?.nickname || "..."}</b>
              </span>
              <div className="hand-actions">
                <button
                  onClick={playSelected}
                  disabled={selectedCards.length === 0}
                  style={{
                    background:
                      selectedCards.length > 0 ? "#10b981" : undefined,
                  }}
                >
                  Đánh{" "}
                  {selectedCards.length > 0
                    ? `(${selectedCards.length} lá)`
                    : ""}
                </button>
                {selectedCards.length > 0 && (
                  <button
                    onClick={() => setSelectedCards([])}
                    style={{ background: "#475569", marginLeft: 8 }}
                  >
                    Bỏ chọn
                  </button>
                )}
              </div>
            </div>

            <div className="my-cards">
              {gs?.selfHand
                ?.slice()
                .sort((a, b) => a.localeCompare(b))
                .map((code, idx) => {
                  const id = `${code}-${idx}`;
                  const isSelected = selectedCards.includes(id);
                  const bg = CARD_COLORS[code] || "#334155";
                  return (
                    <div
                      key={idx}
                      className={`card-item ${isSelected ? "selected" : ""}`}
                      style={{ background: bg }}
                      onClick={() => toggleCard(code, idx)}
                    >
                      <div className="card-name">{code}</div>
                    </div>
                  );
                })}
            </div>
          </section>
        </div>

        {/* ===== LOGS SIDEBAR ===== */}
        <div className="logs-sidebar">
          <div className="logs-title">📋 Server Logs</div>
          <div className="logs-content">
            {logs.map((L, i) => (
              <div key={i} className="log-line">
                {L}
              </div>
            ))}
            <div ref={logsEndRef} />
          </div>
        </div>
      </div>

      {/* ===== DEFUSE MODAL ===== */}
      {gs?.pendingDefuseUserId === myUsId && gs?.pendingBombCardCode && (
        <div className="modal-overlay">
          <div className="modal-content danger">
            <div style={{ fontSize: 80 }}>💣</div>
            <h1 style={{ color: "#ef4444" }}>BOMB!!!</h1>
            <p>
              Mày vừa bốc trúng <b>{gs.pendingBombCardCode}</b>! Dùng thẻ Defuse
              đi!
            </p>
            <button
              className="btn-danger"
              onClick={() => sendCmd("usedefuse", {})}
            >
              🛡️ DÙNG DEFUSE!
            </button>
          </div>
        </div>
      )}

      {/* ===== BOMB REINSERT MODAL ===== */}
      {gs?.pendingBombOwnerUserId === myUsId &&
        !gs?.pendingDefuseUserId &&
        gs?.bombReinsertWindowEndsAt && (
          <div className="modal-overlay">
            <div className="modal-content" style={{ borderColor: "#f59e0b" }}>
              <div style={{ fontSize: 60 }}>🥷</div>
              <h2 style={{ color: "#f59e0b" }}>Giấu Bom Đi!</h2>
              <p>
                Chọn vị trí nhét bom. <b>1</b> = đầu bộ (dính liền),{" "}
                <b>{(gs.drawPileCount ?? 0) + 1}</b> = cuối bộ.
              </p>
              <input
                id="bomb-pos"
                type="number"
                min={1}
                max={(gs.drawPileCount ?? 0) + 1}
                defaultValue={1}
                style={{
                  width: "100%",
                  padding: "10px",
                  background: "#0f172a",
                  color: "white",
                  border: "1px solid #334155",
                  borderRadius: 6,
                  fontSize: 20,
                }}
              />
              <button
                style={{ marginTop: 16, width: "100%" }}
                onClick={() => {
                  const raw =
                    parseInt(document.getElementById("bomb-pos").value) - 1;
                  sendCmd("choosebombinsertposition", {
                    position: Math.max(0, raw),
                  });
                }}
              >
                Nhét Bom Vào Vị Trí Này!
              </button>
            </div>
          </div>
        )}

      {/* ===== FUTURE PEEK MODAL ===== */}
      {futurePeek && (
        <div className="modal-overlay" onClick={() => setFuturePeek(null)}>
          <div className="modal-content" style={{ borderColor: "#06b6d4" }}>
            <h2>🔮 Tương Lai Có Gì?</h2>
            <p>3 lá bài trên cùng là:</p>
            <div style={{ display: "flex", gap: 10, justifyContent: "center" }}>
              {futurePeek.cards.map((c, i) => (
                <div
                  key={i}
                  className="card-item"
                  style={{
                    background: CARD_COLORS[c] || "#334155",
                    cursor: "default",
                  }}
                >
                  {c}
                </div>
              ))}
            </div>
            <button
              style={{ marginTop: 20 }}
              onClick={() => setFuturePeek(null)}
            >
              Đã Hiểu
            </button>
          </div>
        </div>
      )}

      {/* ===== FAVOR RESPONSE MODAL (BOB'S VIEW) ===== */}
      {gs?.pendingFavorTargetId === myUsId && (
        <div className="modal-overlay">
          <div className="modal-content" style={{ borderColor: "#8b5cf6" }}>
            <h2>🙏 Cho Đi Một Ân Huệ</h2>
            <p>
              <b>
                {
                  gs.players.find(
                    (p) => p.userId === gs.pendingFavorRequesterId,
                  )?.nickname
                }
              </b>{" "}
              đang xin mày 1 lá bài.
            </p>
            <p>Chọn lá mày muốn vứt đi:</p>
            <select
              value={favorChoice}
              onChange={(e) => setFavorChoice(e.target.value)}
            >
              <option value="">-- Chọn 1 lá --</option>
              {[...new Set(gs.selfHand)].map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
            <button
              style={{ background: "#8b5cf6", width: "100%" }}
              onClick={() => {
                if (!favorChoice) return alert("Chọn lá đã chứ!");
                sendCmd("choosefavorcard", { cardCode: favorChoice });
                setFavorChoice("");
              }}
            >
              Xác Nhận Cho
            </button>
          </div>
        </div>
      )}

      {/* ===== COMBO/FAVOR MODAL ===== */}
      {showComboModal &&
        (() => {
          const codes = selectedCards.map((c) => c.split("-")[0]);
          const uniq = [...new Set(codes)];
          const isFavor = codes.length === 1 && codes[0] === "Favor";
          const is5diff = codes.length === 5 && uniq.length === 5;
          const isCombo3 = codes.length === 3;
          const needTarget = isFavor || codes.length >= 2;

          return (
            <div className="modal-overlay">
              <div className="modal-content">
                <h2>
                  {isFavor
                    ? "🙏 Yêu Cầu Favor"
                    : is5diff
                      ? "🃏 Combo 5 (Nhặt Discard)"
                      : `🐱 Combo ${codes.length} Lá `}
                </h2>

                {needTarget && !is5diff && (
                  <>
                    <p>Chọn Mục Tiêu:</p>
                    <select
                      value={comboPayload.targetUserId}
                      onChange={(e) =>
                        setComboPayload((p) => ({
                          ...p,
                          targetUserId: e.target.value,
                        }))
                      }
                    >
                      <option value="">-- Chọn người chơi --</option>
                      {alivePlayers.map((p) => (
                        <option key={p.userId} value={p.userId}>
                          {p.nickname}
                        </option>
                      ))}
                    </select>
                  </>
                )}

                {isCombo3 && (
                  <>
                    <p>Mày muốn xin thẻ gì?</p>
                    <select
                      value={comboPayload.requestedCardCode}
                      onChange={(e) =>
                        setComboPayload((p) => ({
                          ...p,
                          requestedCardCode: e.target.value,
                        }))
                      }
                    >
                      <option value="">-- Chọn loại thẻ --</option>
                      {ALL_CARD_CODES.map((c) => (
                        <option key={c} value={c}>
                          {c}
                        </option>
                      ))}
                    </select>
                  </>
                )}

                {is5diff && (
                  <>
                    <p>Nhặt lại thẻ nào từ Discard Pile?</p>
                    <select
                      value={comboPayload.discardCardCode}
                      onChange={(e) =>
                        setComboPayload((p) => ({
                          ...p,
                          discardCardCode: e.target.value,
                        }))
                      }
                    >
                      <option value="">-- Chọn thẻ --</option>
                      {[...new Set(gs?.discardPile || [])].map((c) => (
                        <option key={c} value={c}>
                          {c}
                        </option>
                      ))}
                    </select>
                  </>
                )}

                <div
                  style={{
                    display: "flex",
                    gap: 10,
                    justifyContent: "flex-end",
                    marginTop: 20,
                  }}
                >
                  <button
                    onClick={() => {
                      setShowComboModal(false);
                      setSelectedCards([]);
                    }}
                    style={{ background: "#475569" }}
                  >
                    Hủy
                  </button>
                  <button
                    onClick={commitCombo}
                    style={{ background: "#10b981" }}
                  >
                    ✅ Xác Nhận
                  </button>
                </div>
              </div>
            </div>
          );
        })()}
    </div>
  );
}
