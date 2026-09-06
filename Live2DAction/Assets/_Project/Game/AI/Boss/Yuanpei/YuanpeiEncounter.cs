using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Input;
using Live2DAction.Combat;
using Live2DAction.World;
using Live2DAction.Vehicles;

namespace Live2DAction.AI.Boss.Yuanpei
{
    // Encounter shell (spec §14 BossEncounterController, §20 victory). A trigger volume starts
    // the fight: shows the HUD, applies the "no defence" combat rule note, calls
    // YuanpeiBoss.BeginEncounter, and on defeat runs the victory flow (HUD fade, lock-on release,
    // notify). Victory is HP-only (spec §20 / §24).
    [RequireComponent(typeof(Collider))]
    public class YuanpeiEncounter : MonoBehaviour
    {
        [SerializeField] private YuanpeiBoss boss;
        [SerializeField] private YuanpeiBossHUD hud;
        [Tooltip("Optional - the screen-space Boss domain edge effect (BossDomainScreenVFXSetup wires it). " +
                 "BeginDomain on start, EndDomain on victory/defeat. Auto-found if left null.")]
        [SerializeField] private BossDomainScreenVFX domainVfx;
        [Tooltip("Optional - the 6-beat intro cutscene (續180). When set, it plays before the fight; " +
                 "leave null to keep the boss's own 2.6s descend.")]
        [SerializeField] private YuanpeiIntroCinematic introCinematic;
        [SerializeField] private Vector3 combatCenter = new Vector3(0f, 0f, -114f);
        [SerializeField] private bool startOnTrigger = true;
        [Tooltip("續 131 (user): a LINE across the plaza, not a point. The fight arms the moment the " +
                 "player (anywhere in the trigger volume, ANY X) crosses this world Z toward the boss. " +
                 "'crossSouth' = arm when position.z <= this; uncheck if the boss is on +Z instead.")]
        [SerializeField] private float activationLineZ = -109f;
        [SerializeField] private bool activationCrossSouth = true;

        [Header("Victory sequence (spec §20)")]
        [SerializeField] private string victoryMessage = "戰鬥勝利";
        [SerializeField] private float dissolveSeconds = 1.6f;
        [SerializeField] private float victoryHoldSeconds = 5f;
        [Tooltip("Scene unloaded when the victory sequence returns the player. Empty = don't unload.")]
        [SerializeField] private string returnUnloadScene = "Map_School";
        [Tooltip("Where the player is placed on return - default matches SchoolGate_Exit (在路口前).")]
        [SerializeField] private Vector3 returnArrivalPosition = new Vector3(0f, 1.1f, -78f);
        [SerializeField] private float returnArrivalYaw = 0f;

        [Header("Arena lockdown (續 134→135, user: \"boss有機會因為直線衝刺衝出圍牆之外\" + " +
                 "\"觸發時把整個學校領地60*60往上框起來\")")]
        [Tooltip("Invisible collider-only box the instant the fight starts, covering the WHOLE 學校 " +
                 "60x60 footprint (not just the small boss combat ring) so neither the player nor the " +
                 "boss can wander/fly/get-flung off the map mid-fight. Torn down again on Victory/Defeat " +
                 "- the return teleport and the ChargeCrush void-punt both move players by a direct " +
                 "position set, so they pass through it untouched either way.")]
        [SerializeField] private Vector2 lockdownCenterXZ = new Vector2(0f, -115f);
        [SerializeField] private float lockdownHalfX = 31.5f;
        [SerializeField] private float lockdownHalfZ = 31.5f;
        [Tooltip("The school's own north gate opening (SchoolGate_Exit sits at X=0,Z=-86) stays open at " +
                 "every height, matching the permanent SchoolWall_NorthLeft/Right gap - so the portal-" +
                 "dialogue exception still works while the lockdown is up. Half-width of that gap.")]
        [SerializeField] private float lockdownGateGapHalfWidth = 4.31f;
        [SerializeField] private float lockdownWallThickness = 1f;
        [SerializeField] private float lockdownFloorY = -8f;
        [SerializeField] private float lockdownCeilingY = 45f;

        private GameObject _lockdownRoot;

        public bool Started { get; private set; }
        public bool Won { get; private set; }

        // spec §8.1 - this fight forbids defence / block / parry. While the encounter is live the
        // player's PlayerGuard is disabled so a guard input can't "look like it worked" and still
        // eat unexplained damage. Restored on victory / when the encounter object goes away.
        private Behaviour _playerGuard;
        private bool _guardWasEnabled;

        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void Awake()
        {
            if (boss == null) boss = FindFirstObjectByType<YuanpeiBoss>();
            if (hud == null && boss != null) hud = boss.GetComponent<YuanpeiBossHUD>();
            if (domainVfx == null) domainVfx = FindFirstObjectByType<BossDomainScreenVFX>();
            if (introCinematic == null) introCinematic = FindFirstObjectByType<YuanpeiIntroCinematic>();
        }

        private Transform _zonePlayer;   // the player character while it's inside the trigger volume

        private void OnTriggerEnter(Collider other)
        {
            if (!startOnTrigger || Started) return;
            var p = ResolvePlayerFrom(other);
            if (p != null) _zonePlayer = p;
        }

        private void OnTriggerExit(Collider other)
        {
            if (Started) return;
            if (ResolvePlayerFrom(other) != null) _zonePlayer = null;
        }

        // The player character, whether they walked in OR drove in (VehicleEntrySystem re-parents
        // the seated character under the vehicle, so the "Player" GameObject rides in as a child
        // of whatever collider enters the trigger). Cat-only vehicles resolve to null.
        private Transform ResolvePlayerFrom(Collider other)
        {
            if (other == null) return null;
            var root = other.transform.root;
            foreach (var pip in root.GetComponentsInChildren<PlayerInputProvider>(true))
            {
                for (var t = pip.transform; t != null; t = t.parent)
                    if (t.name == "Player") return t;
            }
            return null;
        }

        public void StartEncounter() => StartEncounter(null);

        public void StartEncounter(Transform triggeringPlayer)
        {
            if (Started || _teardown || boss == null) return;
            Started = true;
            // 續 124 (user "boss 不該把車輛當成目標物件"): if the player drove in, get everyone out of
            // the vehicle first - the fight is on foot, and while seated the Player GameObject is
            // re-parented under the car so `.root` (what the boss targets) would be the car.
            foreach (var ves in FindObjectsByType<VehicleEntrySystem>(FindObjectsSortMode.None))
                if (ves.PlayerSeat != VehicleEntrySystem.Seat.None)   // only if the PLAYER drove in - don't yank a cat out of a car it's driving elsewhere
                    ves.ForceDismountAll();

            ApplyNoDefenceRule(triggeringPlayer);
            SpawnArenaLockdown();

            Transform p = triggeringPlayer != null ? triggeringPlayer : ResolvePlayerTransform();
            if (introCinematic != null && p != null)
            {
                // 續 180 - play the 6-beat intro演出 first, THEN begin the fight (the cinematic
                // already did the boss descend + started the domain, so skip both here).
                StartCoroutine(IntroThenFight(p));
            }
            else
            {
                hud?.SetVisible(true);
                boss.BeginEncounter(combatCenter, triggeringPlayer, playIntro: true);
                domainVfx?.BeginDomain();
            }
        }

        private IEnumerator IntroThenFight(Transform player)
        {
            yield return introCinematic.Play(player, combatCenter);
            hud?.SetVisible(true);
            boss.BeginEncounter(combatCenter, player, playIntro: false);   // cinematic did the descend
            // domainVfx.BeginDomain() already ran inside the cinematic's first beat.
        }

        private Transform ResolvePlayerTransform()
        {
            foreach (var pip in FindObjectsByType<PlayerInputProvider>(FindObjectsSortMode.None))
                for (var t = pip.transform; t != null; t = t.parent)
                    if (t.name == "Player") return t;
            return null;
        }

        // 續 134 - six invisible collider-only panels (4 walls + ceiling + floor) sealing the fight
        // area the moment the encounter starts. Reuses the existing BoundaryBlockEffect/BoundaryBlockHud
        // pair (already a persistent singleton from GreyboxTest.unity) for the same screen-vignette
        // touch feedback the map's other boundary walls give - no new assets needed.
        private void SpawnArenaLockdown()
        {
            if (_lockdownRoot != null) return;   // idempotent - a rematch after Defeat() rebuilds it fresh
            _lockdownRoot = new GameObject("YuanpeiArenaLockdown");
            _lockdownRoot.transform.SetParent(transform, false);

            float t = lockdownWallThickness;
            float cx = lockdownCenterXZ.x, cz = lockdownCenterXZ.y;
            float wallHeight = lockdownCeilingY - lockdownFloorY;
            float wallCenterY = (lockdownCeilingY + lockdownFloorY) * 0.5f;
            // overlaps past the corners so there's no seam to slip through, matching the school's own
            // permanent perimeter wall idiom (SchoolAreaSetup).
            float spanX = lockdownHalfX * 2f + t * 2f;
            float spanZ = lockdownHalfZ * 2f + t * 2f;

            // south / east / west are solid across their whole span - only the school's own north gate
            // (where SchoolGate_Exit lives) stays open, so nothing new is exploitable there.
            MakeLockdownPanel("South", new Vector3(cx, wallCenterY, cz - lockdownHalfZ - t * 0.5f), new Vector3(spanX, wallHeight, t));
            MakeLockdownPanel("East",  new Vector3(cx + lockdownHalfX + t * 0.5f, wallCenterY, cz), new Vector3(t, wallHeight, spanZ));
            MakeLockdownPanel("West",  new Vector3(cx - lockdownHalfX - t * 0.5f, wallCenterY, cz), new Vector3(t, wallHeight, spanZ));

            // north wall: two segments flanking the existing gate gap (mirrors SchoolWall_NorthLeft/
            // Right exactly) - walkable/interactable at every height inside the gap, solid everywhere
            // else including straight up, so the boss can't fly out over just this one side either.
            float gateHalf = lockdownGateGapHalfWidth;
            float northZ = cz + lockdownHalfZ + t * 0.5f;
            float sideWidth = (lockdownHalfX + t) - gateHalf;
            MakeLockdownPanel("NorthLeft",  new Vector3(cx - gateHalf - sideWidth * 0.5f, wallCenterY, northZ), new Vector3(sideWidth, wallHeight, t));
            MakeLockdownPanel("NorthRight", new Vector3(cx + gateHalf + sideWidth * 0.5f, wallCenterY, northZ), new Vector3(sideWidth, wallHeight, t));

            MakeLockdownPanel("Ceiling", new Vector3(cx, lockdownCeilingY + t * 0.5f, cz), new Vector3(spanX, t, spanZ));
            MakeLockdownPanel("Floor",   new Vector3(cx, lockdownFloorY - t * 0.5f, cz), new Vector3(spanX, t, spanZ));
        }

        private void MakeLockdownPanel(string suffix, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("ArenaWall_" + suffix);
            go.transform.SetParent(_lockdownRoot.transform, false);
            go.transform.position = pos;

            var solid = go.AddComponent<BoxCollider>();
            solid.size = size;   // blocks CharacterController.Move / vehicle physics like any ordinary wall

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = size + Vector3.one * (0.6f * 2f);   // same padding convention as the map's other boundary walls

            go.AddComponent<BoundaryBlockEffect>();   // ripple field stays unset (null-safe) - still pulses BoundaryBlockHud
        }

        // Direct position sets (SceneTransitionRunner's return teleport, ChargeCrush's VoidPunt) both
        // bypass CharacterController collision entirely, so neither exception needs special-casing here
        // - they simply aren't stopped by these colliders regardless of when the lockdown is torn down.
        private void DestroyArenaLockdown()
        {
            if (_lockdownRoot == null) return;
            Destroy(_lockdownRoot);
            _lockdownRoot = null;
        }

        private void ApplyNoDefenceRule(Transform player)
        {
            Transform root = player != null ? player.root : null;
            if (root == null)
            {
                foreach (var pip in FindObjectsByType<PlayerInputProvider>(FindObjectsSortMode.None))
                    if (pip.transform.root.name == "Player") { root = pip.transform.root; break; }
            }
            if (root == null) return;
            _playerGuard = root.GetComponentInChildren<PlayerGuard>();
            if (_playerGuard != null)
            {
                _guardWasEnabled = _playerGuard.enabled;
                _playerGuard.enabled = false; // PlayerGuard.OnDisable releases its speed knob cleanly
            }
        }

        private void RestoreDefenceRule()
        {
            if (_playerGuard != null && _guardWasEnabled) _playerGuard.enabled = true;
            _playerGuard = null;
        }

        private void OnDisable() => RestoreDefenceRule();
        private void OnDestroy() => RestoreDefenceRule();

        private bool _ended;
        // 續184d - set the instant Victory()/Defeat() begins and never cleared. The teardown does
        // Started=false (for a would-be in-place rematch) while the player's dead body is still
        // sitting past activationLineZ inside the trigger and Update() still runs - without this
        // guard, Update() immediately re-fires StartEncounter() -> IntroThenFight -> the intro's
        // LockActors() re-disables every player-control + camera-director script, then Map_School
        // unloads and kills the coroutine before UnlockActors() can run -> player permanently
        // frozen after the return teleport (user: "死亡後出來依舊無法移動玩家"). A real rematch is a
        // fresh Map_School load = a fresh YuanpeiEncounter, so this flag never needs clearing.
        private bool _teardown;
        private Live2DAction.Core.Health _playerHealth;

        // YuanpeiBoss.EnterDeath() sends "OnYuanpeiBossDefeated" to its own GameObject; this
        // component listens on the same object if it's placed there, otherwise poll.
        private void Update()
        {
            // arm the fight only once the player (on foot OR in a vehicle) is at the plaza's
            // innermost centre - not merely inside the trigger volume (續 123, user).
            if (startOnTrigger && !Started && !_teardown && _zonePlayer != null)
            {
                float pz = _zonePlayer.position.z;
                if (activationCrossSouth ? pz <= activationLineZ : pz >= activationLineZ)
                    StartEncounter(_zonePlayer);
            }

            if (!Started || _ended || boss == null) return;

            if (boss.BattleOver && boss.Vitals != null && boss.Vitals.IsDead)
            {
                _ended = true;
                StartCoroutine(Victory());
                return;
            }

            // player died in this fight -> show "你菜完了" (RespawnController owns that) then kick
            // them out of the boss map, and reset the fight so re-entering starts fresh.
            if (_playerHealth == null && boss.Player != null)
                _playerHealth = boss.Player.GetComponentInChildren<Live2DAction.Core.Health>();
            if (_playerHealth != null && _playerHealth.IsDead)
            {
                _ended = true;
                StartCoroutine(Defeat());
            }
        }

        private IEnumerator Defeat()
        {
            _teardown = true;   // block Update() from re-firing StartEncounter while we tear down (see _teardown)
            domainVfx?.EndDomain();   // dissolve the domain edge effect over its exit duration
            Transform player = boss != null ? boss.Player : null;

            // 續184c - was a flat WaitForSecondsRealtime(5.6): RespawnController revives the player on
            // a SCALED WaitForSeconds(5), so if a cinematic leaked a low Time.timeScale that 5s
            // stretches well past 5.6s real and we ran the restore + teleport while the player was
            // still dead / SetActive(false) - it came out unmovable. Wait for the ACTUAL revive
            // (Health.IsDead cleared + GameObject active again), timeboxed so a genuinely stuck
            // respawn can't hang the return forever.
            // a cinematic slow-mo left running would also stretch RespawnController's own scaled
            // WaitForSeconds - clear it now so the revive lands on schedule.
            if (!Mathf.Approximately(Time.timeScale, 1f))
            {
                Debug.LogWarning($"[YuanpeiEncounter] Defeat: Time.timeScale was {Time.timeScale:0.00} - forcing to 1");
                Time.timeScale = 1f;
            }
            var reviveHealth = player != null ? player.GetComponentInChildren<Live2DAction.Core.Health>(true) : null;
            float waitDeadline = Time.unscaledTime + 15f;
            while (Time.unscaledTime < waitDeadline)
            {
                bool alive = reviveHealth == null || !reviveHealth.IsDead;
                bool active = player != null && player.gameObject.activeInHierarchy;
                if (alive && active) break;
                yield return null;
            }
            Live2DAction.UI.PlayerDeathScreen.Hide();

            RestoreDefenceRule();
            hud?.SetVisible(false);
            if (boss != null) boss.ResetForRematch();
            DestroyArenaLockdown();
            Started = false;
            _ended = false;
            _playerHealth = null;

            // A ChargeCrush death runs a wide-shot cinematic that deliberately leaves the camera
            // controller OFF (the player is dead in the void). Re-assert camera + full player control
            // (+ drop any lock-on onto the dead boss) now so the returned player can actually move.
            HandControlBackToPlayer(player);

            if (SceneTransitionRunner.Instance != null)
                SceneTransitionRunner.Instance.Begin("", returnUnloadScene, player,
                    returnArrivalPosition, returnArrivalYaw, "", 0.4f, 3);
            else if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.SetPositionAndRotation(returnArrivalPosition, Quaternion.Euler(0f, returnArrivalYaw, 0f));
                if (cc != null) cc.enabled = true;
            }
        }

        private IEnumerator Victory()
        {
            Won = true;
            _teardown = true;                     // block Update() from re-firing StartEncounter while we tear down (see _teardown)
            domainVfx?.EndDomain();               // domain breaks apart as the boss dies
            RestoreDefenceRule();                 // spec §20.7 - lift the combat rule set on defeat
            Transform player = boss != null ? boss.Player : null;

            // 1. death演出 (user: "boss 回到廣場空中做一段震動動畫後碎裂消失" + a camera move).
            //    Runs here on the encounter - YuanpeiBoss.EnterDeath already stopped the boss's own
            //    coroutines + disabled its colliders / lock-on. Driven guarded so a fault inside it
            //    can't skip the camera hand-back + the scene return below (user: "boss戰結束後 攝影機
            //    視角沒有正確回到玩家身上 無論勝利或失敗").
            yield return RunGuarded(DeathDissolve(), "DeathDissolve");

            // DeathDissolve re-enables the controller on its own last line; re-assert camera + full
            // player control here so a mid-dissolve fault still hands everything back before the hold.
            HandControlBackToPlayer(player);

            // 2. centre-screen "戰鬥勝利"
            YuanpeiVictoryBanner.Show(victoryMessage);
            hud?.SetVisible(false);
            SendMessage("OnYuanpeiEncounterWon", SendMessageOptions.DontRequireReceiver);
            Debug.Log("[YuanpeiEncounter] yuanpei_LogoSky defeated - player victory.");

            // 3. hold, then auto-return the player to the road entrance
            yield return new WaitForSecondsRealtime(victoryHoldSeconds);
            YuanpeiVictoryBanner.Hide();
            DestroyArenaLockdown();

            if (SceneTransitionRunner.Instance != null)
            {
                SceneTransitionRunner.Instance.Begin(
                    "", returnUnloadScene, player,
                    returnArrivalPosition, returnArrivalYaw, "", 0.4f, 3);
            }
            else if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.SetPositionAndRotation(returnArrivalPosition, Quaternion.Euler(0f, returnArrivalYaw, 0f));
                if (cc != null) cc.enabled = true;
            }
        }

        // 2026-09-06 - guaranteed camera + player-control hand-back on fight end (user: "boss戰結束後
        // 攝影機視角沒有正確回到玩家身上 無論勝利或失敗" then, once the camera was fixed, "有成功傳送回
        // 入口 但是無法移動角色了"). Every fight-end cinematic (YuanpeiIntroCinematic, DeathDissolve,
        // YuanpeiExecution.Finisher, ChargeCrush's CrushEjectCam) parks some subset of {camera
        // controller, CharacterMovement, PlayerCombat, PlayerInputProvider, CharacterController,
        // Time.timeScale, the lock-on, a StancePoise stagger} and each is supposed to un-park its own
        // set on its own last line. If any faults partway, or two overlap, the player comes back
        // frozen. Rather than trust every path, re-assert the whole "player is in full control" state
        // here, once, at the single encounter-end choke point. Mirrors YuanpeiIntroCinematic.
        // UnlockActors' own end-of-cutscene restore.
        private void HandControlBackToPlayer(Transform player)
        {
            var tpc = Camera.main != null
                ? Camera.main.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour
                : null;
            if (tpc != null)
            {
                tpc.enabled = true;
                (tpc as Live2DAction.CameraSystem.ThirdPersonCameraController)?.SnapYawToTarget();
            }

            Transform root = player != null ? player.root : null;
            if (root == null || root.name != "Player")
                foreach (var pip in FindObjectsByType<PlayerInputProvider>(FindObjectsSortMode.None))
                    if (pip.transform.root.name == "Player") { root = pip.transform.root; break; }
            if (root == null) { Debug.LogWarning("[YuanpeiEncounter] HandControlBackToPlayer - no Player root"); return; }

            var stuck = new System.Collections.Generic.List<string>();

            // slow-mo left running by a cinematic (intro clash, hitstop) would freeze the player.
            if (!Mathf.Approximately(Time.timeScale, 1f)) { stuck.Add($"Time.timeScale({Time.timeScale:0.00})"); Time.timeScale = 1f; }

            void ReEnable<T>() where T : Behaviour
            {
                var c = root.GetComponentInChildren<T>(true);
                if (c != null && !c.enabled) { c.enabled = true; stuck.Add(typeof(T).Name); }
            }
            ReEnable<Live2DAction.Input.PlayerInputProvider>();
            ReEnable<Live2DAction.Characters.CharacterMovement>();
            ReEnable<Live2DAction.Characters.CharacterAnimatorLink>();
            ReEnable<Live2DAction.Combat.PlayerCombat>();

            var cc = root.GetComponent<CharacterController>();
            if (cc != null && !cc.enabled) { cc.enabled = true; stuck.Add("CharacterController"); }

            var stance = root.GetComponentInChildren<Live2DAction.Combat.StancePoise>(true);
            if (stance != null && stance.IsStaggered) { stance.EndStagger(); stuck.Add("StancePoise(staggered)"); }

            var lockCtrl = root.GetComponentInChildren<Live2DAction.Targeting.TargetLockController>(true);
            if (lockCtrl != null) lockCtrl.ForceRelease();

            // Only noisy when something actually had to be un-parked - a healthy fight end logs nothing.
            if (stuck.Count > 0)
                Debug.LogWarning("[YuanpeiEncounter] fight end left player state parked - force-restored: " + string.Join(", ", stuck));
        }

        // Drive a cinematic sub-coroutine so an exception inside it can't abort the caller (which
        // would skip the camera hand-back + the scene return). Mirrors YuanpeiIntroCinematic.Play's
        // own fault guard.
        private IEnumerator RunGuarded(IEnumerator inner, string label)
        {
            while (true)
            {
                object cur = null;
                bool done = false;
                try
                {
                    if (!inner.MoveNext()) done = true;
                    else cur = inner.Current;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[YuanpeiEncounter] {label} faulted - handing control back anyway: {e}");
                    done = true;
                }
                if (done) yield break;
                yield return cur;
            }
        }

        private IEnumerator DeathDissolve()
        {
            Transform vis = boss != null ? boss.VisualRoot : null;
            Transform bt = boss != null ? boss.transform : null;
            if (bt == null) { yield return new WaitForSeconds(0.5f); yield break; }

            var renderers = vis != null ? vis.GetComponentsInChildren<Renderer>() : new Renderer[0];
            Vector3 baseScale = vis != null ? vis.localScale : Vector3.one;
            var mpb = new MaterialPropertyBlock();

            // --- take the camera (always handed back to the player at the end) ---
            Camera cam = Camera.main;
            Behaviour camCtrl = cam != null
                ? cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour
                : null;
            if (camCtrl != null) camCtrl.enabled = false;

            // --- 1. rise back to the plaza centre, up in the air ---
            Vector3 center = boss.Config != null ? boss.Config.arenaCenter : bt.position;
            float groundY = SampleGroundY(new Vector3(center.x, bt.position.y, center.z));
            Vector3 skyPos = new Vector3(center.x, groundY + 13f, center.z);
            Vector3 from = bt.position;

            float t = 0f, dur = 1.2f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                bt.position = Vector3.Lerp(from, skyPos, k);
                if (vis != null) vis.Rotate(0f, 200f * Time.deltaTime, 0f, Space.Self);
                DriveDeathCam(cam, skyPos, k, 0);
                yield return null;
            }
            bt.position = skyPos;

            // --- 2. vibrate / 震動 in place (spec §3.2 "縮放脈衝、傾斜、Emission 變化") ---
            float vibDur = 1.5f;
            t = 0f;
            while (t < vibDur)
            {
                t += Time.deltaTime;
                float k = t / vibDur;
                float amp = 0.05f + 0.55f * k;
                Vector3 jitter = new Vector3(
                    (Mathf.PerlinNoise(Time.time * 47f, 0.3f) - 0.5f),
                    (Mathf.PerlinNoise(0.7f, Time.time * 53f) - 0.5f),
                    (Mathf.PerlinNoise(Time.time * 41f, 0.9f) - 0.5f)) * 2f * amp;
                bt.position = skyPos + jitter;
                if (vis != null)
                {
                    vis.Rotate(0f, (140f + 500f * k) * Time.deltaTime, 0f, Space.Self);
                    float pulse = 1f + Mathf.Sin(Time.time * (18f + 30f * k)) * 0.06f * (0.3f + k);
                    vis.localScale = baseScale * pulse;
                    float emi = 2f + 6f * k + Mathf.Sin(Time.time * 40f) * k;
                    foreach (var r in renderers)
                    {
                        if (r == null) continue;
                        r.GetPropertyBlock(mpb);
                        mpb.SetColor("_EmissionColor", new Color(0.45f, 0.6f, 1f) * emi);
                        r.SetPropertyBlock(mpb);
                    }
                }
                DriveDeathCam(cam, skyPos, k, 1);
                yield return null;
            }

            // --- 3. shatter / 碎裂 ---
            HitStopController.Request(0.09f, 0.15f);
            var shards = SpawnShards(skyPos, 26);
            t = 0f;
            while (t < dissolveSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dissolveSeconds);

                if (vis != null)
                {
                    vis.localScale = baseScale * Mathf.Max(0f, 1f - k * 1.4f);
                    vis.Rotate(0f, 600f * Time.deltaTime, 0f, Space.Self);
                    foreach (var r in renderers)
                    {
                        if (r == null) continue;
                        r.GetPropertyBlock(mpb);
                        mpb.SetColor("_EmissionColor", new Color(0.5f, 0.65f, 1f) * (10f * (1f - k)));
                        r.SetPropertyBlock(mpb);
                    }
                }
                for (int i = 0; i < shards.Count; i++)
                {
                    var s = shards[i];
                    if (s.tr == null) continue;
                    s.vel += Vector3.down * 9f * Time.deltaTime;
                    s.tr.position += s.vel * Time.deltaTime;
                    s.tr.Rotate(s.spin * Time.deltaTime, Space.Self);
                    s.tr.localScale = s.size * Mathf.Max(0f, 1f - k);
                }
                DriveDeathCam(cam, skyPos, k, 2);
                yield return null;
            }

            if (vis != null) vis.gameObject.SetActive(false);
            foreach (var s in shards) if (s.tr != null) Destroy(s.tr.gameObject);

            // Hand the camera back to the player unconditionally (續 123, user: "戰鬥勝利後 攝影機
            // 視角沒有回到玩家身上"). `camCtrlWas` could be false if an F-execution left the controller
            // disabled - restoring to that would freeze the camera on the death angle forever.
            if (camCtrl != null)
            {
                camCtrl.enabled = true;
                (camCtrl as Live2DAction.CameraSystem.ThirdPersonCameraController)?.SnapYawToTarget();
            }
        }

        // Frames the boss during the death演出. phase 0 = rise (ease from behind the player up to a
        // low hero angle), 1 = vibrate (slow push-in + orbit), 2 = shatter (small kick + pull back).
        private void DriveDeathCam(Camera cam, Vector3 bossPos, float k, int phase)
        {
            if (cam == null) return;

            // slow continuous orbit so the shot always feels alive
            float orbitDeg = (phase == 0 ? 15f : phase == 1 ? 40f : 70f) + k * (phase == 1 ? 30f : 10f);
            float dist = phase == 0 ? Mathf.Lerp(15f, 9f, k)
                       : phase == 1 ? Mathf.Lerp(9f, 7.5f, k)
                                    : Mathf.Lerp(7.5f, 16f, k);           // pull back on the shatter
            float height = phase == 0 ? Mathf.Lerp(-2f, 2.5f, k) : phase == 1 ? 2.5f : 2f;

            Vector3 offset = Quaternion.Euler(0f, orbitDeg, 0f) * new Vector3(0f, height, -dist);
            Vector3 want = bossPos + offset;

            float shake = phase == 2 ? Mathf.Max(0f, 0.5f - k) : 0f;
            want += new Vector3(Mathf.PerlinNoise(Time.time * 60f, 0f) - 0.5f,
                                Mathf.PerlinNoise(0f, Time.time * 60f) - 0.5f, 0f) * shake * 2f;

            cam.transform.position = Vector3.Lerp(cam.transform.position, want, 6f * Time.deltaTime);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation,
                Quaternion.LookRotation((bossPos - cam.transform.position).normalized, Vector3.up),
                6f * Time.deltaTime);
        }

        private struct Shard { public Transform tr; public Vector3 vel; public Vector3 spin; public Vector3 size; }

        private List<Shard> SpawnShards(Vector3 center, int count)
        {
            var list = new List<Shard>(count);
            var rng = new System.Random();
            for (int i = 0; i < count; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(g.GetComponent<Collider>());
                var r = g.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mpb = new MaterialPropertyBlock();
                r.GetPropertyBlock(mpb);
                var c = new Color(0.35f + (float)rng.NextDouble() * 0.2f, 0.4f, 0.9f);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_EmissionColor", c * 2f);
                r.SetPropertyBlock(mpb);

                float sz = 0.25f + (float)rng.NextDouble() * 0.55f;
                g.transform.position = center + new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() * 1.5f, (float)rng.NextDouble() - 0.5f) * 2f;
                g.transform.localScale = Vector3.one * sz;
                g.transform.rotation = Random.rotation;

                float a = (float)(rng.NextDouble() * System.Math.PI * 2.0);
                var outward = new Vector3(Mathf.Cos(a), 0.6f + (float)rng.NextDouble() * 0.9f, Mathf.Sin(a));
                list.Add(new Shard
                {
                    tr = g.transform,
                    vel = outward * (2.5f + (float)rng.NextDouble() * 4f),
                    spin = new Vector3((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 720f,
                    size = Vector3.one * sz,
                });
            }
            return list;
        }

        private float SampleGroundY(Vector3 at)
        {
            var hits = Physics.RaycastAll(new Vector3(at.x, at.y + 40f, at.z), Vector3.down, 320f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.GetComponentInParent<PlayerInputProvider>() != null) continue;
                if (h.collider.GetComponentInParent<CharacterController>() != null) continue;
                if (boss != null && h.collider.transform.root == boss.transform.root) continue;
                if (h.collider.gameObject.layer == 9) continue; // building AABBs
                return h.point.y;
            }
            return combatCenter.y + 0.5f;
        }
    }
}
