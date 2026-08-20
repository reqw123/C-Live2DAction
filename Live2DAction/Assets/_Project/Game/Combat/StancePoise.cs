using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.Combat
{
    // Souls-like "poise/架勢" bar (2026-08-17, explicit user request: "想要製作斬殺系統，像魂類遊
    // 戲一樣，有架勢條，滿格會陷入僵直，按下f進行斬殺"). Deliberately a separate component from
    // Health rather than folding this into it - Health already has its own single responsibility
    // (HP/death) shared by every damageable thing in the game (portal-teleported characters,
    // training dummies, etc.), most of which will never have a stance bar at all; this only goes
    // on whoever should support the stagger/execution mechanic.
    //
    // 2026-08-17 follow-up, explicit user request ("敵我雙方都套用架式條 處刑 蹲下動作這套機
    // 制") - originally only accumulated from the PLAYER's own hits (this was Player4-only at the
    // time). Now that BOTH the player and Enemy carry this component, "builds from the
    // player's hits specifically" no longer makes sense as a rule - the correct general rule is
    // "builds from whatever hit THIS character", full stop, since Health.Damaged only ever fires
    // from a real incoming attack by an opponent (nothing in this codebase damages its own
    // character), so no source-identity filtering is needed at all any more.
    [RequireComponent(typeof(Health))]
    public class StancePoise : MonoBehaviour
    {
        // 2026-08-18, explicit user request ("架式條改為60") - lowered from the original 100
        // (which happened to exactly match Health's own default maxHealth, meaning a plain
        // combo would usually kill before the stance bar ever filled - see this session's own
        // "balance coincidence" note) so the stagger window is reliably reachable before death.
        [SerializeField] private float maxStance = 60f;

        // How long the stagger window stays open before the character recovers on its own if
        // never executed - a Souls-like stagger isn't "stunned forever until punished", it's a
        // limited opening. EnemyAI treats CurrentState == Staggered the same way regardless of
        // whether this timer or ExecutionAbility is what eventually ends it.
        [SerializeField] private float staggerDurationSeconds = 6f;

        // 2026-08-18, explicit user request ("一段時間沒有受到攻擊就回復(符合魂類設計)") - real
        // Souls-style poise/stance passively drains back down once the character stops being hit
        // for a while, instead of staying banked forever - a slow chip-away combo should mostly
        // wash out between engagements rather than permanently stacking toward a stagger over an
        // entire fight. Regen only runs while NOT staggered (see Update) - once the bar is full
        // and the character is staggered, this timer/regen is irrelevant until EndStagger.
        //
        // 2026-08-19, explicit user request ("改成1.5秒") - shortened from the original 3s so the
        // no-hit gap before stance starts draining is tighter/more forgiving of brief lulls.
        [SerializeField] private float regenDelaySeconds = 1.5f;
        [SerializeField] private float regenPerSecond = 20f;

        // 2026-08-18, explicit user request ("同時減緩架勢條成長速度") - applied to the incoming
        // damage amount before it's added to _currentStance, so poise now fills SLOWER than raw
        // damage dealt rather than 1:1 with it. Originally 0.5, calibrated against this project's
        // THEN-common 10-damage light attacks (5 stance/hit, 12 hits to fill a 60 maxStance bar -
        // double the un-multiplied 6 hits).
        //
        // 2026-08-18 follow-up, real playtested bug ("目前還是存在連續硬直問題") - basic attack
        // damage was later independently raised to 25 (this session's own "普通攻擊傷害改為25"
        // change), and because stance gain is still proportional to raw damage, that silently
        // undid the slowdown above: 25 * 0.5 = 12.5 stance/hit, only 5 hits to refill 60 - 2.5x
        // FASTER than the 12-hit cadence this multiplier was actually meant to produce. The new
        // postStaggerGraceSeconds grace window only delays a re-stagger by a fixed amount, it
        // can't fix a bar that refills in a single combo regardless. Re-lowered to 0.2 (25 * 0.2 =
        // 5 stance/hit) to restore the original ~12-hit cadence at the CURRENT damage value -
        // this needs to be revisited again any time basic attack damage changes, since the two
        // are coupled by design (stance gain is derived from damage dealt, not tracked
        // independently).
        [SerializeField] private float stanceGainMultiplier = 0.2f;

        // 2026-08-18, real playtested bug ("硬直觸發後架勢條來不及歸0馬上受到攻擊又進入硬直") -
        // EndStagger() already zeroes _currentStance instantly (confirmed correct earlier this
        // session), but zero doesn't mean SAFE - normal attacks now deal 25 damage (this
        // session's own "普通攻擊傷害改為25" change), so with stanceGainMultiplier applied that's
        // 12.5 poise per hit against a 60 maxStance bar - just 5 hits refills it completely,
        // trivial for an attacker whose combo doesn't pause to land within a couple of seconds of
        // recovery. A Souls-like stagger is supposed to be a punished OPENING, not something that
        // can immediately re-trigger itself before the character has even finished standing back
        // up - originally only STANCE accumulation was skipped during this window (incoming hits
        // still dealt full health damage); see the 2026-08-19 final-fix note below for why that
        // changed to a real invulnerability window instead.
        //
        // 2026-08-19 follow-up, real playtested bug ("我連續攻擊會讓敵人連續兩次進入硬直(做了兩
        // 次蹲下的動作)") - tried making the countdown REFRESH on every hit landed while still
        // locked (only clears after a genuine no-hit gap), fixing the double-stagger. But that
        // introduced a worse asymmetric bug: against an attacker whose own combo-to-combo gap is
        // shorter than postStaggerGraceSeconds (real measured EnemyAI cadence: ~0.3-0.65s between
        // landed hits, chaining Attack1-4 back into Attack1 with no idle beat - see EnemyAI/
        // ComboAttackState) the lock could NEVER expire, permanently walling the target out of
        // ever being staggered again for the rest of the fight ("玩家...目前好像只會硬直一次 架勢
        // 條就不會漲了") - not a numbers mismatch between Player/Player4 (same script, same
        // values), just this refresh-while-hit rule's inevitable outcome once an attacker never
        // naturally pauses.
        //
        // 2026-08-19 final fix, explicit user request ("改成所有角色復活後1.5內無敵 不受傷害") -
        // replaced the whole "gate stance accumulation, refresh on hit" scheme with a real, TIME-
        // BOUNDED invulnerability window on Health itself (see Health.SetInvulnerable) covering
        // both stance AND health damage. This sidesteps the refresh-forever trap entirely: while
        // invulnerable, Health.ApplyDamage returns before even firing Damaged, so OnDamaged below
        // never runs and there's nothing left to "refresh" - the window is a plain countdown that
        // ALWAYS expires in exactly postStaggerGraceSeconds regardless of how relentlessly the
        // attacker keeps swinging through it. The instant it ends, the character is immediately
        // vulnerable again and the very next hit accumulates stance normally, so a sustained fight
        // against a never-pausing attacker can still re-stagger the target - it just always waits
        // out the same fixed beat first, same as any other recovering character.
        [SerializeField] private float postStaggerGraceSeconds = 1.5f;
        private float _postStaggerGraceRemaining;

        private Health _health;
        private float _currentStance;
        private float _staggerElapsed;
        private float _timeSinceLastHit;

        public bool IsStaggered { get; private set; }
        public float CurrentStance => _currentStance;
        public float MaxStance => maxStance;

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;

            // Release our own invulnerability grant if this component goes away mid-window
            // (disabled/destroyed while still recovering) - otherwise the character would be
            // stuck permanently invulnerable, since nothing else would ever call
            // SetInvulnerable(this, false) again to clear it.
            _health.SetInvulnerable(this, false);
        }

        private void Update()
        {
            if (IsStaggered)
            {
                _staggerElapsed += Time.deltaTime;
                if (_staggerElapsed >= staggerDurationSeconds)
                {
                    EndStagger();
                }
                return;
            }

            if (_postStaggerGraceRemaining > 0f)
            {
                _postStaggerGraceRemaining -= Time.deltaTime;
                if (_postStaggerGraceRemaining <= 0f)
                {
                    // The ONLY place this window ever ends - a plain, unconditional countdown
                    // (see postStaggerGraceSeconds' own comment for why this replaced the old
                    // hit-refreshed version). No OnDamaged call can extend or re-arm it, since
                    // OnDamaged simply never runs while _health.IsInvulnerable is true.
                    _health.SetInvulnerable(this, false);
                }
            }

            if (_currentStance <= 0f)
            {
                return;
            }

            _timeSinceLastHit += Time.deltaTime;
            if (_timeSinceLastHit >= regenDelaySeconds)
            {
                _currentStance = Mathf.Max(0f, _currentStance - regenPerSecond * Time.deltaTime);
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            if (IsStaggered || _health.IsDead)
            {
                return;
            }

            // No postStaggerGraceRemaining check needed here any more - while that window is
            // open, _health.IsInvulnerable is true, so Health.ApplyDamage returns before it even
            // fires Damaged and this method never runs at all (see postStaggerGraceSeconds' own
            // comment). Anything that reaches this point is, by construction, past the window.
            _timeSinceLastHit = 0f;
            _currentStance = Mathf.Min(maxStance, _currentStance + info.Amount * stanceGainMultiplier);
            if (_currentStance >= maxStance)
            {
                // 2026-08-19, explicit user request ("讓架勢條滿格後進入硬直的同時 清空快速架勢
                // 條") - previously left at maxStance (visually still full) for the ENTIRE
                // staggerDurationSeconds window, only actually clearing on EndStagger() (timeout
                // or execution). WorldSpaceStanceBar reads CurrentStance directly every frame with
                // no smoothing (see its own Update), so the bar visually stayed full/glowing the
                // whole time the character was staggered - only zeroing the moment the stagger
                // itself ended. Zeroing right here instead means the bar empties in the SAME frame
                // stagger triggers, matching "滿格的瞬間" rather than "staggered AND full" for the
                // whole window.
                IsStaggered = true;
                _staggerElapsed = 0f;
                _currentStance = 0f;
            }
        }

        // Called by ExecutionAbility once the finishing blow lands, or by the stagger-duration
        // timeout above - either way, the character leaves the stagger window and stance starts
        // accumulating from 0 again (after postStaggerGraceSeconds of real invulnerability - see
        // that field's own comment).
        public void EndStagger()
        {
            IsStaggered = false;
            _currentStance = 0f;
            _staggerElapsed = 0f;
            _timeSinceLastHit = 0f;
            _postStaggerGraceRemaining = postStaggerGraceSeconds;
            _health.SetInvulnerable(this, postStaggerGraceSeconds > 0f);
        }

        // 2026-08-19, bug report ("血量歸0應該是做出死亡動作然後消失 並非硬直蹲下") - a fatal hit
        // that lands WHILE the target is staggered but ISN'T the F-key execution (ExecutionAbility.
        // ResolveExecution already calls EndStagger() itself before/around its own kill) used to
        // leave IsStaggered stuck true forever, since nothing else ever clears it. StaggerAnimationLink
        // keeps calling animator.SetBool("Staggered", true) every single frame regardless of Health's
        // state, and SpecialMoveAnimatorSetup's AnyState -> Staggered transition can interrupt ANY
        // state including Dead (see WireBoolState's own comment - it's deliberately unconditional so
        // a fresh stagger can cut off a mid-swing attack). So the frame right after Dead's own AnyState
        // transition fired and the Dying clip started, this stale true flag flipped the Animator right
        // back into the kneeling Staggered state, where it stayed until DeathAnimationLink deactivated
        // the GameObject - the character visibly knelt back down instead of playing out its death.
        // Clearing the flag here, the instant Health fires Died, removes the condition that transition
        // needs before StaggerAnimationLink's next Update can re-assert it. No invulnerability grant
        // needed here (unlike EndStagger() above) - the character is already dead, and Health.ApplyDamage
        // already refuses all further damage for a dead target on its own.
        private void OnDied()
        {
            IsStaggered = false;
            _currentStance = 0f;
            _staggerElapsed = 0f;
        }
    }
}
