using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Core;
using Live2DAction.Input;

namespace Live2DAction.World
{
    // 2026-08-17, explicit user request ("重送門(傳送門) 能夠前往天空島") - a simple hold-to-enter,
    // press-to-teleport gate. Deliberately NOT auto-teleport-on-touch for the PLAYER (explicit
    // user choice: "要按特定按鍵才傳送") - a CharacterController brushing past on its way somewhere
    // else shouldn't yank the player across the map, only actually standing on the pad and
    // choosing to.
    //
    // 2026-08-17 follow-up, explicit user request ("判定任何角色...進入後會變成紅色光柱...玩家以外
    // 之對象進入後直接傳送") - originally player-only; detectAnyCharacter/autoTeleportNonPlayers
    // (both default false, matching the original ground portal unchanged) let a specific instance
    // (the sky-island/"鳥居" portal) react to ANY character standing on it - idle green, red while
    // occupied - and skip the key-press gate for anyone who isn't the player (an AI-driven
    // character has no way to press E).
    //
    // Teleports by disabling/re-enabling the CharacterController around the position set, same
    // pattern this codebase already needs for any other direct-position-set on a
    // CharacterController-driven character (CharacterController fights a raw transform.position
    // write otherwise - it recomputes movement from its own last-known internal state on the next
    // Move() call, which can partially undo or fight the jump). Falls back to a plain transform
    // move for an occupant with no CharacterController at all.
    [RequireComponent(typeof(Collider))]
    public class Portal : MonoBehaviour
    {
        [SerializeField] private Transform destination;

        // World-space offset added to destination.position to get the arrival spot. The small
        // +0.1 Y component is a ground-clearance buffer (CharacterController.Move's own gravity/
        // grounding resolves a tiny gap instantly and harmlessly, but spawning slightly embedded
        // risks a one-frame stutter - see the half-height compensation in Move() for the other
        // half of that fix).
        //
        // 2026-08-18, explicit user request ("不要定位在傳送門判定範圍上，避免剛傳送過去不動又被
        // 傳回來") - the X/Z component is deliberately non-zero on both portal instances (set per-
        // instance in the Inspector, verified clear of both the destination's own trigger and any
        // solid geometry at both ends for every character size in the scene - see this session's
        // own note). Landing exactly at the destination's position put the arrival capsule dead
        // center in that portal's own trigger radius - technically fine for the PLAYER (nothing
        // fires until E is pressed), but for autoTeleportNonPlayers destinations this meant a
        // non-player that arrived and simply hadn't started moving yet was still standing in the
        // trigger the instant its own cooldown (see teleportCooldownSeconds) expired, sending it
        // straight back. Landing physically outside the trigger's footprint fixes both cases at
        // once.
        [SerializeField] private Vector3 arrivalOffset = new Vector3(0f, 0.1f, 0f);

        // See this class's own header comment - both false reproduces the original player-only
        // gate exactly (Portal_GroundToSky keeps this default), true switches on the "any
        // character, auto-teleport non-players" behaviour (Portal_SkyToGround).
        [SerializeField] private bool detectAnyCharacter;
        [SerializeField] private bool autoTeleportNonPlayers;

        // Optional - only used when detectAnyCharacter reacts visually (green/red/flashing). Left
        // unassigned on a portal that doesn't want this (e.g. the flat swirl disc ground portal).
        [SerializeField] private Renderer visualRenderer;
        [SerializeField] private Color idleColor = new Color(0.15f, 1f, 0.35f, 1f);
        [SerializeField] private Color activeColor = new Color(1f, 0.15f, 0.15f, 1f);

        // 2026-08-18, explicit user request ("讓綠色圓柱跟紅色圓柱一樣大") - supersedes the
        // previous "occupied doubles the radius" version. Idle and occupied are now the exact
        // same size; only the color (and, separately, the cooldown flash below) tells the two
        // states apart.
        [SerializeField] private float cooldownFlashSpeed = 6f;

        // 2026-08-17, real bug report ("敵人進到傳送門區域後會卡著且閃爍，沒有傳送到鳥居") - root
        // cause: the two portals are each other's destination, and a non-player auto-teleports
        // the INSTANT it enters a portal's trigger. Landing at the destination dropped it right
        // back inside THAT portal's own trigger, which immediately teleported it straight back -
        // every physics step, forever.
        //
        // 2026-08-18 rewrite, explicit user request ("不管玩家有沒有在圓柱裡面，冷卻時間內都要閃
        // 爍") - the original fix tracked cooldown PER CHARACTER (a shared static dictionary
        // keyed by root Transform), so the flash only showed while that specific character was
        // still standing in the trigger that had just fired. Moved to a cooldown owned by each
        // PORTAL INSTANCE instead: using a portal (as departure or as someone's arrival point)
        // puts THAT portal on cooldown regardless of who's there or whether they've since walked
        // off, which is both what was asked for and a strictly better fix for the original
        // ping-pong bug - the destination portal goes on cooldown the instant it's arrived at, so
        // it can't immediately auto-teleport anyone (including a completely different character
        // that happens to already be standing on it) back out.
        [SerializeField] private float teleportCooldownSeconds = 3f;

        private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

        private CharacterController _playerInside;
        private int _occupantCount;
        private MaterialPropertyBlock _propertyBlock;

        // 2026-08-18 fix (code review, no direct user report yet) - non-player colliders
        // CURRENTLY standing in the trigger, tracked separately from _occupantCount so Update
        // can retry auto-teleporting them once this portal's own cooldown clears. Without this,
        // a non-player that entered WHILE the portal was already on cooldown (e.g. a second
        // character arriving right behind the one that just triggered it) was only ever checked
        // once, in OnTriggerEnter itself - if that single check landed inside the cooldown
        // window, nothing ever re-checked it again unless it physically left and re-entered the
        // trigger, so it would just sit there forever once the cooldown expired.
        private readonly List<Collider> _pendingNonPlayers = new List<Collider>();

        // Time.time value below which this specific portal refuses to fire again - see
        // teleportCooldownSeconds' own comment. -1 so a portal that's never been used isn't
        // considered on cooldown at Time.time == 0.
        private float _cooldownUntil = -1f;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            ApplyVisualState(VisualState.Idle);
        }

        private void Reset()
        {
            Collider trigger = GetComponent<Collider>();
            if (trigger != null)
            {
                trigger.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            bool isPlayer = other.GetComponentInParent<PlayerInputProvider>() != null;
            if (!isPlayer && !ResolvesAsCharacter(other))
            {
                return;
            }

            _occupantCount++;

            if (isPlayer)
            {
                _playerInside = other.GetComponentInParent<CharacterController>();
                return;
            }

            if (!autoTeleportNonPlayers)
            {
                return;
            }

            if (IsOnCooldown())
            {
                // See _pendingNonPlayers' own field comment - can't teleport it right now, but
                // Update will retry once the cooldown clears instead of forgetting about it.
                _pendingNonPlayers.Add(other);
                return;
            }

            TeleportOccupant(other, isPlayer: false);
        }

        private void OnTriggerExit(Collider other)
        {
            bool isPlayer = other.GetComponentInParent<PlayerInputProvider>() != null;
            if (!isPlayer && !ResolvesAsCharacter(other))
            {
                return;
            }

            _occupantCount = Mathf.Max(0, _occupantCount - 1);
            if (isPlayer)
            {
                _playerInside = null;
            }
            else
            {
                // Walked off before its own cooldown-blocked entry ever got retried - it's gone,
                // stop tracking it (Move()'s own null-target guard would no-op anyway, but there's
                // no reason to keep a reference to a collider that isn't here to teleport).
                _pendingNonPlayers.Remove(other);
            }
        }

        private void Update()
        {
            ApplyVisualState(ResolveVisualState());

            if (destination == null)
            {
                return;
            }

            if (!IsOnCooldown())
            {
                RetryPendingNonPlayers();
            }

            if (_playerInside == null || IsOnCooldown())
            {
                return;
            }

            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                TeleportOccupant(null, isPlayer: true);
            }
        }

        // Only the FIRST still-valid pending occupant actually moves - TeleportOccupant's own
        // Move() call puts this portal back on cooldown immediately, so any others in the list
        // simply wait for the next window (same one-departure-per-cooldown-cycle behavior a
        // fresh OnTriggerEnter would produce). Iterates backwards so removing entries mid-loop
        // (both the teleported one and any that quietly went null/disabled since being queued)
        // doesn't skip the next element.
        private void RetryPendingNonPlayers()
        {
            for (int i = _pendingNonPlayers.Count - 1; i >= 0; i--)
            {
                Collider occupant = _pendingNonPlayers[i];
                _pendingNonPlayers.RemoveAt(i);

                if (occupant == null)
                {
                    continue;
                }

                TeleportOccupant(occupant, isPlayer: false);
                return;
            }
        }

        // Flashing takes priority over Idle/Active and is now driven purely by THIS portal's own
        // cooldown timer - see teleportCooldownSeconds' own comment for why that's keyed per
        // portal instance rather than per character standing in it.
        private VisualState ResolveVisualState()
        {
            if (IsOnCooldown())
            {
                return VisualState.Flashing;
            }

            return _occupantCount > 0 ? VisualState.Active : VisualState.Idle;
        }

        private bool IsOnCooldown()
        {
            return Time.time < _cooldownUntil;
        }

        // "Any character" - the same signal PlayerCombat.IsAnyDamageableInRange already uses to
        // tell "a real character" apart from plain scenery, kept consistent rather than inventing
        // a second convention (e.g. a tag) for the same question.
        private bool ResolvesAsCharacter(Collider other)
        {
            return detectAnyCharacter && other.GetComponentInParent<Health>() != null;
        }

        private void TeleportOccupant(Collider occupantCollider, bool isPlayer)
        {
            _occupantCount = Mathf.Max(0, _occupantCount - 1);

            if (isPlayer)
            {
                CharacterController playerController = _playerInside;
                _playerInside = null;
                Move(playerController != null ? playerController.transform : null, playerController);
                return;
            }

            Move(occupantCollider.transform.root, occupantCollider.GetComponentInParent<CharacterController>());
        }

        private void Move(Transform target, CharacterController controller)
        {
            if (target == null || destination == null)
            {
                return;
            }

            // arrivalOffset means "how far above the destination's ground the CAPSULE should
            // land", not "the root" - real bug report ("076有成功傳送到鳥居但消失") root cause:
            // 076's CharacterController.center carries a large negative local offset (the trick
            // that keeps a 5x-scaled giant's actual hitbox grounded while its root sits up near
            // its visual pivot - see EnemyAI's own scale-compensation comment). Placing the ROOT
            // directly at the arrival point ignored that offset entirely: 076's capsule ended up
            // roughly 1.5m below the sky island's floor and fell straight through, out of view,
            // while Enemy (whose center is (0,0,0), no offset) landed correctly by coincidence.
            // Solving for root position from "the capsule's world center lands here" instead
            // works for both - a no-op for a normal character, correct for a rescaled one.
            Vector3 targetCapsuleCenter = destination.position + arrivalOffset;
            if (controller != null)
            {
                // arrivalOffset alone is only a small clearance BUFFER above the ground surface,
                // not the full height the capsule's center needs to sit at - real bug report
                // ("傳送到鳥居後卡在傳送點附近 沒辦法前進") root cause: without this, the capsule
                // center landed only arrivalOffset (0.1m) above ground instead of half the
                // character's own height above it, so the capsule's BOTTOM ended up embedded
                // (height/2 - arrivalOffset) into the solid floor - 0.4m for the player - and
                // CharacterController.Move fought its own depenetration every frame instead of
                // letting movement through. height is in local space and scales with the
                // character's own transform (see EnemyAI's scale-compensation comment for why a
                // rescaled giant's CharacterController.height isn't already world-space), so this
                // has to go through lossyScale.y rather than being added as a flat constant.
                float worldHalfHeight = controller.height * 0.5f * target.lossyScale.y;
                targetCapsuleCenter += Vector3.up * worldHalfHeight;

                Vector3 worldCenterOffset = target.TransformVector(controller.center);
                controller.enabled = false;
                target.position = targetCapsuleCenter - worldCenterOffset;
                controller.enabled = true;
            }
            else
            {
                target.position = targetCapsuleCenter;
            }

            // See teleportCooldownSeconds' own comment - both ends of the trip go on cooldown,
            // not just this one, so the destination can't immediately fire again either.
            float cooldownExpiry = Time.time + teleportCooldownSeconds;
            _cooldownUntil = cooldownExpiry;
            Portal destinationPortal = destination.GetComponent<Portal>();
            if (destinationPortal != null)
            {
                destinationPortal._cooldownUntil = cooldownExpiry;
            }
        }

        private enum VisualState
        {
            Idle,
            Active,
            Flashing,
        }

        // Idle green / occupied red (same size as idle - see cooldownFlashSpeed's own field
        // comment) / flashing between the two colors whenever THIS portal is on cooldown,
        // regardless of current occupancy. A no-op (besides the initial Awake call) when
        // visualRoot/visualRenderer aren't wired, so this is safe to leave unset on a portal that
        // doesn't want the reactive look (Portal_GroundToSky).
        private void ApplyVisualState(VisualState state)
        {
            if (visualRenderer == null)
            {
                return;
            }

            // Lazy - a script recompile DURING Play triggers a domain reload that nulls this
            // non-serialized field while Update() keeps running (Awake isn't re-called), which
            // otherwise throws ArgumentNullException in GetPropertyBlock every frame.
            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            Color color;
            if (state == VisualState.Flashing)
            {
                float blend = Mathf.Sin(Time.time * cooldownFlashSpeed) * 0.5f + 0.5f;
                color = Color.Lerp(idleColor, activeColor, blend);
            }
            else
            {
                color = state == VisualState.Active ? activeColor : idleColor;
            }

            visualRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorPropertyId, color);
            visualRenderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
