# 飛行系統設計文件（RPG 動作遊戲式自由飛行）

> **狀態：已實作**（2026-08-20）。第 3 節的規格已經照做進 `CharacterMovement.cs`／`ThirdPersonCameraController.cs`／`ICameraYawSource.cs`／`IInputCommand.cs`／`PlayerInputProvider.cs`／`EnemyAI.cs`，並用一套繞過「Editor 沒有 OS 焦點時 Play Mode 可能整個凍結」限制的做法驗證過（透過 reflection 直接反覆呼叫 `CharacterMovement.Update()`，同時暫時停用 `PlayerInputProvider`／飛行耐力元件的真實 `Update()`，改由腳本自己餵輸入，避免兩邊互相搶著寫同一組欄位）：進入飛行、垂直緩衝、飛行速度脫鉤、衝刺（含耐力額外消耗）、俯衝雙條件加成、側傾（角色＋鏡頭同步）都逐項量到數字確認正確；EditMode 134 個測試僅 1 個失敗、PlayMode 64 個測試 3 個失敗，三個失敗經檢查都跟這次改動的檔案完全無關（跳躍計時 flaky、`RespawnController`／連段步數既有場景資料飄移），是既有問題，不是這次引入的迴歸。第 4 節「實作後要驗證的項目」裡跟側傾方向、俯衝手感等「一定要人眼確認」的部分，還是需要你實際飛一輪才能定案。

2026-08-20，透過 `/grilling` 訪談定案的完整飛行系統設計。這份文件記錄**現況基準**、**每個決定背後的取捨理由**、以及**具體到欄位/公式層級的實作規格**，讓之後（不管是我自己還是別人）動手實作時有一份可以直接照做、也知道「為什麼這樣選」的依據，不用重新討論一輪。

範圍限定**玩家角色**（`Player` GameObject 上的 `CharacterMovement`）。敵人 AI（`EnemyAI.cs`）的「空戰追擊」是完全獨立、不共用這套機制的系統，此文件不影響它——這是先前已經明確定案的既有決定，見 `EnemyAI.cs` 自己的註解（"AI never triggers the player-only flight"）。

---

## 1. 現況基準（實作前，已經有什麼）

在動手改之前先把目前 `CharacterMovement.cs` 飛行相關的真實邏輯釘清楚，避免實作時對「現況」有錯誤假設：

- **水平移動**：飛行時跟地面共用同一套「攝影機相對 WASD」邏輯（`CameraRelativeDirection`），套用同一個 `moveSpeed`（目前 2，是照走路動畫調的，不是為飛行調的）。角色會自動轉向面對移動方向（`SmoothDampAngle` 緩動 yaw）。
- **垂直移動**：`FlyPressed`（Ctrl 按住）＝上升（`flightAscendSpeed=6`），`FlyDescendPressed`（Shift 按住）＝下降（`flightDescendSpeed=4`），兩者都是**瞬間**設定 `_verticalVelocity`，沒有任何緩衝；放開兩者都不按＝原地懸停（不是掉下來）。
- **鏡頭俯仰角完全不影響飛行方向或速度**——目前 `_pitch`（鏡頭俯仰）只用在鎖定空中目標時的視覺朝向，跟移動邏輯完全無關。
- **進出場**：按住 Ctrl 即可起飛（不管在地上還是空中），起飛後即使放開 Ctrl 也不會掉下來（只是懸停），只有真正落地或耐力耗盡才會結束飛行狀態，耐力耗盡會先掉進 Glide（`glideDescendSpeed=2` 固定緩降，水平仍可控，不耗能量）。
- **耐力**：獨立於大招能量的另一個 `UltimateEnergy` 實例，`maxEnergy=200`，`regenAmount=10/regenIntervalSeconds=1`（被動回復 10/秒，飛或不飛都在跑），飛行時額外呼叫 `Drain(flightEnergyDrainPerSecond * Time.deltaTime)`（目前 15/秒），淨消耗 5/秒，約 40 秒連續飛行。這次**不動**。
- **鏡頭**：`ThirdPersonCameraController` 的旋轉目前只有 `Quaternion.Euler(_pitch, _yaw, 0f)`——**完全沒有 roll（側傾）分量**，這是這次要新增的部分。飛行/滑翔時距離乘上 `flightDistanceMultiplier=1.4` 拉遠。
- **視覺回饋**：`WingFlap.cs` 已經有「飛行時振幅/頻率提高」的翅膀振翅（讀 `CharacterMovement.IsFlying`）。這次不動它，但飛行速度變快後這個既有效果會自然更明顯。
- **鍵位**：WASD 移動、左鍵攻擊、Shift（點按＝閃避／飛行時按住＝下降）、中鍵鎖定、Space 跳躍、R 大招、Ctrl 按住＝飛行上升。**Q 鍵目前完全沒用到**。

---

## 2. 定案的設計決定（含討論時的取捨理由）

### 2.1 操控模型：混合式（不是純看鏡頭飛，也不是原封不動）
維持現有「攝影機相對 WASD＋角色自動轉向面對移動方向」的地面移動慣例，飛行不脫鉤——理由：轉向面對移動方向已經跟攻擊朝向、鎖定邏輯等系統掛勾，飛行時額外開一條「朝向跟隨鏡頭、跟移動方向無關」的例外規則，會讓角色在地面/空中的轉向行為突然不一致，增加複雜度也可能讓操作感覺不連貫。垂直移動維持獨立按鍵（不靠鏡頭俯仰），但**額外疊加俯衝加速**這個手感層次（見 2.4）——整體最貼近原神/鳴潮式的滑翔手感，也是這個專案從頭到尾的鏡頭/移動哲學參照對象。

### 2.2 垂直移動加上緩衝
上升/下降從瞬間切換速度改成短時間 `SmoothDamp`（~0.15-0.2 秒），去掉瞬間翻轉的生硬感，同時保留操控的即時反應——緩衝時間刻意比水平移動的緩衝（`accelerationSmoothTime=0.08`／`decelerationSmoothTime=0.05`）長一點，但遠比「沉重」的手感短，純粹是去掉一格瞬間跳動，不是要做出笨重感。

### 2.3 飛行速度跟地面脫鉤＋獨立衝刺鍵
`moveSpeed=2` 是照走路動畫調的，飛行沿用同一個數字明顯太慢、沒有「自由飛翔」的感覺。飛行給一個獨立、明顯更快的水平巡航速度。另外加一個**獨立**的衝刺機制（不是俯衝加速的別名）：
- 綁 **Q 鍵**（按住觸發、放開結束——跟 `FlyPressed`/`FlyDescendPressed` 一樣讀 `isPressed`，不是 `wasPressedThisFrame`，因為衝刺需要整段按住持續生效）。
- 任何朝向都能用（乘在當下的飛行速度上，不管你往哪飛）。
- 額外消耗飛行耐力（沿用同一個 `flightEnergy` 池，不開新資源——耐力經濟這次不重新設計）。
- **只在真正 Flying 狀態可用，Glide 狀態不能衝刺**——Glide 本來就是耐力耗盡的後備狀態，讓一個會耗能量的機制在「已經沒能量」的狀態下可用不合理。

理由：衝刺服務的是「我現在就是想快，不管往哪飛」（追擊/閃避情境），俯衝加速服務的是「往下飛本來就該更快」的物理直覺——兩者情境不同，分開設計並允許疊加，讓進階玩家有操作深度，新手光靠俯衝也有感，不強制記額外的鍵才能感受到飛行的速度感。

### 2.4 俯衝加速：鏡頭角度＋實際下降，兩個條件都要
只看鏡頭俯仰角會讓玩家「維持水平飛行、單純低頭看地板」就白嫖加速，不合理；只看有沒有按下降鍵又會讓「往下看」這個原本想要的沉浸感提案名不符實。最終定案：**鏡頭俯仰角往下壓超過一個門檻，且同時按著下降鍵（Shift）在真的下降**，兩個條件都滿足才判定為「俯衝」，加速幅度隨鏡頭俯仰角漸進（壓得越低衝得越快，不是有/無的開關）。

### 2.5 側傾視覺（banking）：角色＋鏡頭都傾，跟著輸入強弱走
轉彎/橫移時角色模型跟攝影機都會側傾，增加「真的在飛」的重量感。側傾角度**直接跟著左右輸入（A/D）的強弱走**，不是跟著實際轉向角速度算——理由：這個混合模式的操控骨幹本來就是「按什麼鍵給什麼即時反應」（水平 WASD、垂直專鍵都是直接對應輸入的），側傾跟著輸入強弱走，手感才會跟其他操控一致；跟著轉向角速度算的話，因為朝向本身是靠 `SmoothDampAngle` 緩動出來的（見 2.1，朝向跟隨移動方向），側傾會感覺慢半拍、不夠精準。側傾在 Flying **跟** Glide 都套用（純視覺、不耗資源，兩個狀態下水平移動都可自由操控，一致比較合理）。

### 2.6 明確不動的部分
- 耐力經濟數值（200 上限、消耗 15/秒、回復 10/秒）——已經根據實測回饋調過，這次不重新開數值戰。
- 飛行進出場邏輯（按住才能操控、放開只懸停、只有落地/耐力耗盡才真正結束）——先前已明確拍板的決定。
- 視覺特效（速度拖尾、衝刺/俯衝時的 FOV 動態）——這次先只做操控機制本身，特效留到手感實際飛過、定案後再疊加，避免現在為了還沒定案的手感反覆調整特效。
- 敵人 AI 的空戰追擊系統——完全獨立，不受影響。

---

## 3. 具體實作規格

### 3.1 `Live2DAction/Assets/_Project/Game/Camera/ICameraYawSource.cs`
新增一個屬性，讓飛行邏輯讀得到鏡頭俯仰角（目前介面只有 `YawDegrees`）：

```csharp
public interface ICameraYawSource
{
    float YawDegrees { get; }
    float PitchDegrees { get; } // 新增：正值＝往下看（沿用 ThirdPersonCameraController 現有的 _pitch 正負號慣例）
}
```

目前整個專案唯一的實作者是 `ThirdPersonCameraController`（`grep` 確認過，只有它 + `CharacterMovement`/`ILockOnSource` 引用這個介面），改介面安全、不會動到其他地方。

### 3.2 `Live2DAction/Assets/_Project/Game/Camera/ThirdPersonCameraController.cs`
- 新增 `public float PitchDegrees => _pitch;` 實作上面新增的介面成員。
- `LateUpdate` 裡組相機旋轉的地方，從 `Quaternion.Euler(_pitch, _yaw, 0f)` 改成讀取目標角色的側傾角，加上 roll 分量：`Quaternion.Euler(_pitch, _yaw, targetMovement != null ? -targetMovement.CurrentBankRollDegrees : 0f)`（正負號、要不要取反等實際飛一輪再微調，這裡先用跟角色視覺側傾**相反號**去湊「鏡頭跟著同一側傾但視覺上自然」的直覺，不保證一次到位）。
- 側傾角**不**由攝影機自己算，直接讀 `CharacterMovement` 已經算好的同一個值（見 3.3）——單一資料來源，避免角色視覺側傾跟鏡頭側傾各自獨立運算導致兩者不同步。`ResolveTargetMovement()` 這個既有的 helper 已經在用了，直接複用。

### 3.3 `Live2DAction/Assets/_Project/Game/Characters/CharacterMovement.cs`
新增欄位（`[SerializeField]`，附初始建議值，實際照手感微調）：

```csharp
[SerializeField] private float flightVerticalSmoothTime = 0.18f;   // 2.2
[SerializeField] private float flightMoveSpeed = 9f;               // 2.3，跟地面 moveSpeed(2) 脫鉤
[SerializeField] private float boostSpeedMultiplier = 1.8f;        // 2.3
[SerializeField] private float boostEnergyDrainPerSecond = 25f;    // 2.3，疊加在既有 flightEnergyDrainPerSecond 之上
[SerializeField] private float diveMaxSpeedMultiplier = 1.4f;      // 2.4
[SerializeField] private float divePitchThresholdDegrees = 15f;    // 2.4
[SerializeField] private float maxBankRollDegrees = 20f;           // 2.5
[SerializeField] private float bankRollSmoothTime = 0.12f;         // 2.5
```

新增私有狀態：

```csharp
private float _verticalVelocitySmoothDampRef; // 給 3.3-a 的 SmoothDamp 用，跟水平移動那組 SmoothDamp 參照分開
private float _bankRollDegrees;
private float _bankRollAngularVelocity;
```

新增公開屬性（給攝影機/未來的視覺系統讀）：

```csharp
public float CurrentBankRollDegrees => _bankRollDegrees;
public bool IsBoosting { get; private set; } // 選配，之後想讓翅膀/特效對衝刺有反應時用得到
```

**3.3-a：垂直移動緩衝**（對應 2.2）——把現有的
```csharp
_verticalVelocity = flyDescendHeld ? -flightDescendSpeed : (flyHeld ? flightAscendSpeed : 0f);
```
改成：
```csharp
float targetVertical = flyDescendHeld ? -flightDescendSpeed : (flyHeld ? flightAscendSpeed : 0f);
_verticalVelocity = Mathf.SmoothDamp(_verticalVelocity, targetVertical, ref _verticalVelocitySmoothDampRef, flightVerticalSmoothTime);
```
只在 `_isFlying` 分支動這裡——Glide 的固定緩降跟一般重力墜落都維持原本的瞬間累加，緩衝只套用在主動飛行控制上。

**3.3-b：俯衝判定＋倍率**（對應 2.4）——需要先讀到鏡頭俯仰角，透過既有的 `CameraYawSource` 屬性（型別已經是 `ICameraYawSource`，改介面後自動拿得到 `PitchDegrees`）：
```csharp
float cameraPitch = CameraYawSource?.PitchDegrees ?? 0f;
bool diving = flyDescendHeld && cameraPitch > divePitchThresholdDegrees;
float diveT = diving ? Mathf.InverseLerp(divePitchThresholdDegrees, 70f, cameraPitch) : 0f; // 70 = ThirdPersonCameraController.maxPitch 現有上限
float diveMultiplier = Mathf.Lerp(1f, diveMaxSpeedMultiplier, diveT);
```
俯衝倍率同時套用在水平巡航速度**跟**下降速度上（見 3.3-d），讀起來才會是「真的在往前下方俯衝」而不是只有其中一個方向變快。

**3.3-c：衝刺判定**（對應 2.3）——新增輸入讀取（見 3.4），只在 Flying 生效：
```csharp
bool boostHeld = !staggered && inputCommand != null && inputCommand.BoostPressed;
IsBoosting = _isFlying && boostHeld;
float boostMultiplier = IsBoosting ? boostSpeedMultiplier : 1f;
if (IsBoosting && flightEnergy != null)
{
    flightEnergy.Drain(boostEnergyDrainPerSecond * Time.deltaTime); // 疊加在既有飛行耗能之上，不是取代
}
```

**3.3-d：套用到水平移動目標速度**——原本 `Vector3 desiredVelocity = desiredDirection * moveSpeed;` 這一行，改成依飛行狀態切換基準速度、再疊乘衝刺跟俯衝倍率：
```csharp
bool airborneControlled = _isFlying || _isGliding;
float baseSpeed = airborneControlled ? flightMoveSpeed : moveSpeed;
float totalMultiplier = boostMultiplier * diveMultiplier; // 疊乘，2.3/2.4 都同意可疊加
Vector3 desiredVelocity = desiredDirection * (baseSpeed * totalMultiplier);
```
（`boostMultiplier`/`diveMultiplier` 只在 Flying 分支算出有意義的值，Glide/地面狀態下兩者都自然是 1，不用另外特判。）

**3.3-e：俯衝倍率也套用在下降速度上**——3.3-a 算出的 `targetVertical`，下降那一支再乘上 `diveMultiplier`：
```csharp
float targetVertical = flyDescendHeld ? -flightDescendSpeed * diveMultiplier : (flyHeld ? flightAscendSpeed : 0f);
```

**3.3-f：側傾角計算**（對應 2.5）——放在 `Update()` 尾端、跟俯仰視覺套用同一段附近：
```csharp
float targetBankRoll = airborneControlled ? -moveInput.x * maxBankRollDegrees : 0f;
_bankRollDegrees = Mathf.SmoothDampAngle(_bankRollDegrees, targetBankRoll, ref _bankRollAngularVelocity, bankRollSmoothTime);
```
角色視覺套用（沿用現有 `_visual.localRotation` 那行，原本只寫俯仰，改成俯仰＋側傾一起寫）：
```csharp
_visual.localRotation = Quaternion.Euler(-_pitch, 0f, _bankRollDegrees);
```

### 3.4 輸入層
`Live2DAction/Assets/_Project/Game/Input/IInputCommand.cs` 新增一個成員：
```csharp
bool BoostPressed { get; }
```

`Live2DAction/Assets/_Project/Game/Input/PlayerInputProvider.cs` 比照 `FlyPressed` 的寫法（`isPressed`，不是 `wasPressedThisFrame`，因為要整段按住持續生效）：
```csharp
public bool BoostPressed { get; private set; }
...
BoostPressed = keyboard.qKey.isPressed;
```

`Live2DAction/Assets/_Project/Game/AI/EnemyAI.cs`（`IInputCommand` 的另一個實作者）比照它現有的 `public bool FlyPressed => false;` 補一行：
```csharp
public bool BoostPressed => false; // AI 不會觸發玩家專屬的飛行衝刺
```
（少了這行介面新增成員後 `EnemyAI` 會直接編譯失敗，記得一起補。）

---

## 4. 實作後要驗證的項目

- 垂直方向切換上升/下降/懸停時，確實感覺到短暫的加減速緩衝，但不會慢到操控遲鈍。
- 飛行水平速度明顯比走路快，衝刺（Q）再明顯快一截，放開 Q 立刻回到基礎飛行速度。
- 純低頭看地板（不按 Shift）不會加速；按著 Shift 下降但鏡頭沒往下壓超過門檻也不太會加速；兩個條件都滿足時，鏡頭壓得越低，俯衝越快。
- 按 A/D 橫移時角色跟鏡頭都會side-傾，放開立刻回正；側傾方向（左傾/右傾對應哪個輸入）要實際飛一次確認符合直覺，不符合就翻轉 3.2/3.3-f 裡的正負號。
- Glide 狀態下能側傾但不能衝刺；耐力耗盡時衝刺鍵應該自然沒有效果（`_isFlying` 已經是 false，`boostMultiplier` 自然是 1）。
- 耐力消耗速率沒有被這次改動意外動到（衝刺是疊加的新消耗，不是取代原本的 `flightEnergyDrainPerSecond`）。
- `EnemyAI`／任何其他 `IInputCommand` 實作者編譯過（新增介面成員後的必要檢查）。

---

## 5. 明確排除在這次範圍外

- 耐力經濟重新設計（維持現有 200/15/10 三個數字）。
- 速度拖尾、衝刺/俯衝的 FOV 動態、任何額外 VFX。
- 飛行進出場邏輯改動（按住控制、放開懸停、落地/耐力耗盡才結束——維持原樣）。
- 敵人 AI 的空戰追擊系統（完全獨立，這份文件不涉及）。
- 地面衝刺／地面移動速度（衝刺鍵只在 Flying 狀態生效）。
