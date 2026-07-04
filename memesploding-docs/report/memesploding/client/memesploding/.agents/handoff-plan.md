# Memesploding Handoff Plan

## Summary

Da lam va da push:
- `27e125e Add SO event channels and Nope event handling`: tao ScriptableObject event channel/listener/bridge va xu ly event Nope backend co ban.
- `2d9c7d9 Add client reaction window flow`: them reaction/nope UI vao `Canvas/UI` scene `Gameplay`, noi target selection, command `nope`, `usedefuse`, combo 2, va cac handler event client.
- `2dc56db Complete backend reaction event flow`: backend emit `ReactionWindowOpened` co deadline, `NopePlayed` reset window, `SkipApplied`, `ShuffleApplied`, va doi Nope window mac dinh 5s.

Dang co thay doi chua commit:
- Fix lop phu den sau Nope window: `Canvas/UI` tu tat khi reaction panel an va khong con overlay khac active.
- `ActionNoped` auto-hide sau 1.5s.
- Nut Nope chi enable neu local hand co la `Nope`, tranh `CARD_NOT_OWNED`.

## Immediate Next Steps

1. Khi quay lai, kiem tra nhanh working tree:
   - `git status`
   - Cac file modified du kien:
     - `client/memesploding/Assets/Scripts/Managers/GameManager.cs`
     - `client/memesploding/Assets/Scripts/Managers/UIManager/GameplayUIManager.cs`
     - `client/memesploding/Assets/Scripts/UI/Gameplay/ReactionWindowView.cs`

2. Validate roi commit/push fix overlay:
   - Unity validate 3 script tren.
   - Doc Unity console error-only.
   - `git diff --check`
   - Commit de xuat: `Fix reaction overlay cleanup`
   - Push `origin/dev`.

3. Test lai flow thuc te:
   - Start server bang `docker compose up --build postgres redis game api`.
   - Trong Unity, vao match bot.
   - Danh action card de mo Nope window.
   - Cho window het hoac bam `PASS`.
   - Khi den luot local, phai thao tac duoc voi hand/deck, khong con overlay den.

## Remaining Work

- Chan hoac xu ly Cat don:
  - Hien log con co `Unhandled card: Cat1` khi danh Cat don.
  - Quyet dinh mac dinh: Cat chi duoc danh qua combo hop le; single Cat phai shake/return, khong animate ra ban.

- Fix draw animation errors:
  - `PlayerDrawCard.Animator.Play` dang goi state/layer sai.
  - `PlayerDrawCard.Reset()` khong duoc goi `Animator.Update` khi object inactive.
  - Acceptance: draw card khong con `Animator.GotoState`, `Invalid Layer Index -1`, hoac `Animator.Update on inactive object`.

- Fix scene cleanup `[EventBus]`:
  - Console bao object `[EventBus]` con sot khi closing scene.
  - Tim noi tao runtime GameObject `EventBus`/dispatcher va dam bao destroy dung lifecycle, khong spawn trong `OnDestroy`.

- Fix opponent profile prefab:
  - `OpponentProfile` thieu `NicknameText`.
  - Acceptance: khong con warning khi instantiate 3 bot.

- Implement UI con thieu cho card logic:
  - Combo 3: chon `requestedCardCode`.
  - Combo 5: chon `discardCardCode`.
  - Favor target: nguoi bi Favor chon la tra qua `ChooseFavorCard`.
  - Defuse: slider/list chon vi tri dat bomb, khong hard-code `0`.

- Polish reaction/effect animations:
  - Animation/sound cho `AttackApplied`, `SkipApplied`, `ShuffleApplied`, `FuturePeeked`, `FavorResolved`, `ActionNoped`, `DefuseUsed`, `ExplosionTriggered`.
  - `SeeTheFuture` nen co popup rieng 3 la thay vi dung display card generic.

## Test Plan

- Unity static:
  - Validate scripts touched.
  - Console error-only sau compile phai la 0.

- Backend:
  - `dotnet build server/Memesploding.sln`.
  - Neu sua runtime rule, test script: `bash server/Memesploding.Game/Tests/Gameplay/run-gameplay-tests.sh`.

- Manual gameplay:
  - Login guest, start bot match.
  - Play action card -> `ReactionWindowOpened` appears.
  - If no `Nope` in hand, Nope button disabled.
  - If window closes, overlay disappears and local input works.
  - Draw card animation runs without Animator errors.
  - Close/reload scene without `[EventBus]` cleanup error.

## Assumptions

- Branch target remains `dev`.
- Normal deck is still the only gameplay scope for now.
- Backend remains authoritative: client may show intent/animation, but final state follows server events.
- No further backend schema break needed; added `reactionWindowEndsAt`/`nopeCount` fields are backward compatible for client payload parsing.
