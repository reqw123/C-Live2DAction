using UnityEngine;
using Live2DAction.AI;
using Live2DAction.Core;
using Live2DAction.Input;
using Live2DAction.Targeting;

namespace Live2DAction.Combat
{
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private AttackData[] comboAttacks = new AttackData[3];
        [SerializeField] private Transform attackOrigin;

        // 2026-08-18, explicit user request (aerial combat grilling session, Q3/Q6). Optional
        // (null-safe below) - on Player4 this is left unset and EnemyAI drives
        // UseSphericalJudgment directly instead (see that class's own Aerial Combat comment),
        // since Player4 has no lock-on concept of its own. On the player, wiring this to the
        // same TargetLockController CharacterMovement already uses lets PlayerCombat
        // independently notice "my locked target is far above/below me" without either
        // component needing a reference to the other - matches this codebase's established
        // preference for small, decoupled components over cross-wiring.
        [SerializeField] private MonoBehaviour lockOnSource;

        // Same 3m the aerial-combat-partner side (EnemyAI.aerialCombatEnterHeight) uses - kept
        // as its own field rather than a shared constant since the two are conceptually
        // independent checks that just happen to agree on the same number right now (see
        // CONTEXT.md's own "Aerial Combat trigger" decision).
        [SerializeField] private float aerialHeightThreshold = 3f;

        // 2026-08-18, explicit user request (aerial combat grilling session, Q3/Q6) - "地面近戰
        // 判定完全不動" (ground melee judgment untouched): the standard forward capsule reaches
        // a fixed distance directly ahead and nowhere else, which is exactly wrong when the
        // attacker's own pitch is aiming up/down at a vertically-offset target (a slightly-off
        // pitch angle would otherwise still whiff completely). True while EITHER this
        // character's own lock-on detects a vertically-far target (see lockOnSource above) OR -
        // for Player4 - EnemyAI.IsAerialCombat says so directly. Settable so EnemyAI can drive it
        // for a character with no lock-on concept of its own.
        public bool UseSphericalJudgment { get; set; }

        private ILockOnSource LockOnSource => lockOnSource as ILockOnSource;

        // 2026-08-17, explicit user request ("敵我雙方都套用架式條") - optional (null-safe
        // below), mirrors CharacterMovement's own "stance" field for the same reason: a
        // staggered character shouldn't be able to start swinging. Only blocks STARTING a new
        // attack (attackPressed forced false) - a swing already resolving when stagger begins
        // is allowed to finish rather than being torn out mid-animation, same "keep this minimal"
        // scope as the movement-side gate.
        [SerializeField] private StancePoise stance;

        // 2026-08-18, explicit user request (death animation) - optional (null-safe below),
        // same "only blocks STARTING a new attack" scope as `stance` above: a swing already
        // resolving when the killing blow lands is allowed to finish (a whiffed apparent freeze
        // mid-animation would look worse than the actual attack itself already committed), but
        // a dead character shouldn't be able to start throwing new punches during its own death
        // animation.
        [SerializeField] private Health health;

        // Spawned at each landed hit's actual impact point (2026-08-12, explicit user
        // request: "攻擊特效" - a hit spark, not a swing trail or a hit-stop/screen-shake
        // effect). Optional - null just means no visual, doesn't affect damage.
        [SerializeField] private GameObject hitEffectPrefab;

        private ComboAttackState _state;

        // Resolved on every use rather than cached in Awake(), so assigning inputSource
        // after the component has already Awoken (e.g. from a test) still takes effect.
        private IInputCommand InputCommand => inputSource as IInputCommand;

        public AttackPhase CurrentPhase => _state != null ? _state.Phase : AttackPhase.Idle;
        public int ComboIndex => _state != null ? _state.ComboIndex : -1;
        public float PhaseProgress => _state != null ? _state.PhaseProgress : 0f;

        // 2026-08-13, real bug report ("我已經盡到敵人範圍內，線條從紅色變成黃色，但敵人尚未
        // 作出攻擊") - lets EnemyAI read the actual AttackData it's about to swing with, so its
        // own "am I close enough to attack" decision can be computed from the SAME Range/Radius
        // the real hit judgment (and this class's own Gizmo) uses, instead of a separately-
        // tuned attackRange float that has to be manually kept in sync (see EnemyAI's own
        // "combat" field comment for the full story). Enemies only ever have one combo step
        // (comboAttacks.Length == 1, see GreyboxSceneBuilder.CreateEnemy/Player4EnemyAISetup),
        // so "primary" unambiguously means "the attack this component will actually use".
        public AttackData PrimaryAttack => comboAttacks != null && comboAttacks.Length > 0 ? comboAttacks[0] : null;

        // 2026-08-13, explicit user request (ultimate skill: "attack1傷害乘10倍") - set/reset
        // by UltimateAbility while its buff window is active. 1 = no effect, the default/
        // inactive state.
        //
        // 2026-08-18 rewrite, real bug report ("確定玩家施展必殺技時 5秒內每次攻擊都是10倍傷害
        // 嗎") - originally only applied to PrimaryAttack (Attack1) specifically, matching the
        // literal original request text ("attack1傷害乘10倍"). Explicit follow-up confirmed the
        // actual intent was every hit landed during the buff window, not just the combo's first
        // step - renamed from Attack1DamageMultiplier to reflect that it's no longer combo-step-
        // scoped, see ResolveActiveHit below (now applied unconditionally, not gated on
        // attackData == PrimaryAttack).
        public float UltimateDamageMultiplier { get; set; } = 1f;

        private void Awake()
        {
            if (attackOrigin == null)
            {
                attackOrigin = transform;
            }
        }

        private void Update()
        {
            // Built lazily rather than in Awake, same reasoning as InputCommand above: tests
            // assign comboAttacks via reflection right after AddComponent, which already runs
            // Awake synchronously.
            if (_state == null)
            {
                _state = new ComboAttackState(comboAttacks);
            }

            // Auto-detect from lock-on, if wired (see lockOnSource's own comment - unset on
            // Player4, which drives UseSphericalJudgment directly from EnemyAI instead).
            if (LockOnSource != null)
            {
                Transform locked = LockOnSource.LockedTarget;
                UseSphericalJudgment = locked != null && Mathf.Abs(locked.position.y - transform.position.y) > aerialHeightThreshold;
            }

            IInputCommand inputCommand = InputCommand;
            bool attackPressed = inputCommand != null && inputCommand.AttackPressed;
            if (stance != null && stance.IsStaggered)
            {
                attackPressed = false;
            }
            if (health != null && health.IsDead)
            {
                attackPressed = false;
            }
            if (_state.Tick(Time.deltaTime, attackPressed))
            {
                ResolveActiveHit(_state.CurrentAttack);
            }
        }

        private void ResolveActiveHit(AttackData attackData)
        {
            if (attackData == null)
            {
                return;
            }

            // A single sphere at the far end of Range whiffs anything standing well short of
            // that distance (e.g. point-blank/overlapping, which is exactly what the two
            // combatants' CharacterControllers settle into once blocked - see
            // CharacterCollisionBlockingTests.WalkingIntoPlayer4_DoesNotClimbOnTop's 2026-08-12
            // bug report investigation) - a capsule spanning from the attacker out to Range
            // covers the whole reach instead of only a thin shell right at the tip.
            //
            // 2026-08-18: UseSphericalJudgment (Aerial Combat only - see that property's own
            // comment) swaps this for a sphere centered on attackOrigin instead, so a slightly-
            // off pitch angle at a vertically-offset target still lands. Ground combat's
            // judgment shape is completely untouched either way.
            Vector3 near = attackOrigin.position;
            Vector3 far = near + attackOrigin.forward * attackData.Range;
            Collider[] candidates = UseSphericalJudgment
                ? Physics.OverlapSphere(near, attackData.Range + attackData.Radius)
                : Physics.OverlapCapsule(near, far, attackData.Radius);
            // hitOrigin: the point ResolveHits/effect-spawning treats as "where the attack
            // landed" - the capsule's own tip normally, but a sphere has no meaningful "tip", so
            // its own center (attackOrigin) is used instead.
            Vector3 hitOrigin = UseSphericalJudgment ? near : far;
            // Applies to every combo step while the ultimate is active - see
            // UltimateDamageMultiplier's own comment for why this is no longer gated to
            // PrimaryAttack specifically.
            var hitPoints = AttackResolver.ResolveHits(hitOrigin, attackData, transform.root, candidates, UltimateDamageMultiplier);

            // Per-attack override (e.g. LightAttack3's dedicated slash VFX) takes priority
            // over the shared spark prefab - see AttackData.HitEffectOverride's own comment.
            GameObject effectPrefab = attackData.HitEffectOverride != null ? attackData.HitEffectOverride : hitEffectPrefab;
            if (effectPrefab != null)
            {
                // attackOrigin.rotation, not Quaternion.identity - 2026-08-13 real user
                // report ("通常視角會從側面看，才會有劍氣掃過去的畫面"): a world-aligned
                // slash VFX (see Attack3SlashEffectSetup's renderer.alignment) needs the
                // attacker's actual facing to stand in the right plane; the shared spark
                // prefab stays Billboard-rendered so this doesn't change its look (Billboard
                // ignores the spawned rotation for its camera-facing, and its emission shape
                // is an omnidirectional sphere - see HitEffectSetup).
                if (hitPoints.Count > 0)
                {
                    foreach (Vector3 point in hitPoints)
                    {
                        Instantiate(effectPrefab, point, attackOrigin.rotation);
                    }
                }
                else if (attackData.AlwaysSpawnHitEffect)
                {
                    // 2026-08-13 explicit user request ("打空氣時也有特效出來") - no target
                    // was hit (hitPoints is empty), but this attack's VFX represents the
                    // swing itself rather than an impact (see AttackData.AlwaysSpawnHitEffect's
                    // own comment), so it still spawns at the same "far" point the hit query
                    // itself just missed against - the tip of the attack's reach, not the
                    // attacker's own feet, so a whiffed Attack3 still visibly reaches out to
                    // where the blade/qi actually swung. hitOrigin, not far, for the same
                    // "sphere has no tip" reason noted above.
                    Instantiate(effectPrefab, hitOrigin, attackOrigin.rotation);
                }
            }
        }

        // 2026-08-12, explicit user request ("如何看到兩個角色的攻擊範圍?") - draws the same
        // capsule ResolveActiveHit actually queries against (near = attackOrigin, far =
        // attackOrigin + forward*Range, radius = Radius), one per combo step. 2026-08-13,
        // explicit user request ("我需要分開敵人與玩家的攻擊判定 攻擊距離物件 並且顏色都要有區
        // 別，最好攻擊判定頂端要有更明顯的視覺效果") - player and enemy now use entirely
        // different color families (green vs red) instead of sharing the same red/orange/
        // yellow palette, so which side an attack range belongs to is obvious at a glance
        // (also keeps both clear of the cyan used by TargetLockController/EnemyAI's own
        // "警備距離" Gizmo). "Enemy" is detected via GetComponent<EnemyAI>() - PlayerCombat
        // itself has no player/enemy flag (Player4 reuses the exact same component driven by
        // EnemyAI instead of real input, see EnemyAI's own class comment), and EnemyAI's
        // presence is already the reliable, existing signal for "this is an AI-driven attacker"
        // used nowhere else in the codebase, so no new field/wiring was needed. Only ever
        // called by the Editor (Gizmos methods are stripped from Player builds automatically,
        // no #if UNITY_EDITOR needed) - visible in the Scene view whenever Player/Player4 is
        // selected, and in the Game view too if its Gizmos toggle is on, including during Play.
        private void OnDrawGizmosSelected()
        {
            if (comboAttacks == null)
            {
                return;
            }

            Transform origin = attackOrigin != null ? attackOrigin : transform;
            bool isEnemyAttacker = GetComponent<EnemyAI>() != null;
            for (int i = 0; i < comboAttacks.Length; i++)
            {
                if (comboAttacks[i] != null)
                {
                    DrawAttackRangeGizmo(origin, comboAttacks[i], i, isEnemyAttacker, transform.root);
                }
            }
        }

        // Green family = player's own attacks (LightAttack1/2/3, shading darker per combo
        // step); red family = enemy attacks (EnemyAttack) - deliberately a different hue
        // entirely, not just a different shade, so "whose attack is this" reads instantly even
        // at a glance, and neither clashes with the cyan "警備距離" Gizmo.
        private static readonly Color[] PlayerGizmoColors =
        {
            new Color(0.3f, 1f, 0.3f, 0.5f),
            new Color(0.2f, 0.8f, 0.35f, 0.5f),
            new Color(0.1f, 0.6f, 0.25f, 0.5f),
        };

        private static readonly Color[] EnemyGizmoColors =
        {
            new Color(1f, 0.2f, 0.2f, 0.5f),
            new Color(0.85f, 0.1f, 0.1f, 0.5f),
            new Color(0.65f, 0.05f, 0.05f, 0.5f),
        };

        // Bright, fully-opaque "something is actually in range right now" signal - deliberately
        // not a hue used anywhere else in this gizmo system (not the green/red attacker
        // palettes, not the cyan 警備距離 circles), so it reads as a distinct alert state
        // rather than "yet another color meaning something else".
        private static readonly Color InRangeHighlightColor = new Color(1f, 1f, 0f, 0.85f);

        private static void DrawAttackRangeGizmo(Transform origin, AttackData data, int comboIndex, bool isEnemyAttacker, Transform selfRoot)
        {
            Color[] palette = isEnemyAttacker ? EnemyGizmoColors : PlayerGizmoColors;
            Color baseColor = palette[comboIndex % palette.Length];

            Vector3 near = origin.position;
            Vector3 far = near + origin.forward * data.Range;

            // 2026-08-13, third real usability report ("還是很難判斷 有沒有明確的視覺表達方式
            // 能讓我知道究竟有沒有進入到攻擊範圍") - a static outline still makes the player eyeball
            // "is that character inside this shape", which is genuinely hard to judge from an
            // arbitrary camera angle in 3D. Runs the EXACT SAME query ResolveActiveHit actually
            // uses (Physics.OverlapCapsule with this same near/far/radius, excluding the
            // attacker's own root) so the gizmo answers "would this attack land right now" with
            // a real yes/no instead of an eyeballed guess - and it's the same answer whether
            // this runs in Edit mode (colliders are already in the scene, no Play needed) or
            // during Play.
            bool targetInRange = IsAnyDamageableInRange(near, far, data.Radius, selfRoot);

            // 2026-08-13, fourth real usability report ("不要這樣包覆整個物體 看不見... 判定區
            // 域邊界最外圍畫一條線或是製作一個圓點標記，代表極限距離") - the filled sphere tried
            // right before this (only when something was in range) swallowed the character
            // standing inside it, exactly the opposite of "let me see what's happening". Fixed
            // for good this time by never filling anything: the boundary is always just thin
            // lines - "in range" is now expressed by the boundary LINE itself changing color
            // and gaining a couple of tightly-offset concentric rings (still just outlines, not
            // fills, so nothing is ever occluded), not by any shape growing to cover space.
            Color lineColor = targetInRange ? InRangeHighlightColor : baseColor;
            Gizmos.color = lineColor;

            // 2026-08-13, real usability report with a screenshot ("線條很多，紅色的有兩圈銜
            // 接，我分不清楚攻擊距離的邊界在哪") - the original version drew a wireframe sphere
            // at BOTH near and far. When Radius is large relative to Range (exactly the enemy's
            // current tuning - a short, fat capsule), those two circles sit almost on top of
            // each other and merge into a confusing tangle. Fix: no circle at near at all - the
            // attacker's own body already marks "where this starts". Four lines still trace the
            // capsule's width along its length (an honest depiction of the actual query shape).
            Vector3 right = origin.right * data.Radius;
            Vector3 up = origin.up * data.Radius;
            Gizmos.DrawLine(near + right, far + right);
            Gizmos.DrawLine(near - right, far - right);
            Gizmos.DrawLine(near + up, far + up);
            Gizmos.DrawLine(near - up, far - up);

            // The one true boundary marker: a thin wireframe circle at the capsule's actual
            // Radius, sitting exactly where a target would have to be touching to get hit -
            // "碰到這個一定被攻擊" (touch this, you get hit), literally accurate since this is
            // drawn at the same Radius Physics.OverlapCapsule queries with.
            Gizmos.DrawWireSphere(far, data.Radius);

            if (targetInRange)
            {
                // Two extra rings, barely offset from the true boundary, make the line read as
                // noticeably thicker/more urgent when something is actually in range - Gizmos
                // has no line-width control, so stacking thin circles is the way to fake it.
                // Still purely outlines - nothing here ever fills the interior.
                Gizmos.DrawWireSphere(far, data.Radius * 0.95f);
                Gizmos.DrawWireSphere(far, data.Radius * 1.05f);
            }

            // Small reference dot marking the exact center/tip - kept deliberately tiny
            // (well under the character's own body size) so it can never wrap around or hide
            // whoever is standing there, unlike the full-Radius filled sphere this replaced.
            Color tipColor = lineColor;
            tipColor.a = 1f;
            Gizmos.color = tipColor;
            Gizmos.DrawSphere(far, data.Radius * 0.12f);
        }

        // Read-only mirror of ResolveActiveHit's own candidate search (same OverlapCapsule
        // shape, same self-exclusion via transform.root) - deliberately not routed through
        // AttackResolver.ResolveHits, which applies damage as a side effect; this only needs
        // to know whether something WOULD be hit, not actually hit it.
        private static bool IsAnyDamageableInRange(Vector3 near, Vector3 far, float radius, Transform selfRoot)
        {
            Collider[] candidates = Physics.OverlapCapsule(near, far, radius);
            foreach (Collider candidate in candidates)
            {
                if (candidate == null || candidate.transform.root == selfRoot)
                {
                    continue;
                }

                if (candidate.TryGetComponent(out IDamageable _))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
