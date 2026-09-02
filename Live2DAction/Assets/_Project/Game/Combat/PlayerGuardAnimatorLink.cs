using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-09-01, Sekiro deflect (spec 二 - "在 Animator 使用或整合防禦狀態，例如 IsGuarding Bool").
    // Minimal for now: mirrors PlayerGuard's live state onto the shared Animator so an authored
    // guard/parry clip added later (Phase 3) has parameters to transition on. No guard STATE is
    // wired yet - the procedural PlayerGuard pose is the current visual.
    public class PlayerGuardAnimatorLink : MonoBehaviour
    {
        [SerializeField] private PlayerGuard guard;
        [SerializeField] private Animator animator;
        [SerializeField] private string isGuardingBool = "IsGuarding";
        [SerializeField] private string parryTrigger = "GuardParry"; // optional - only SetTrigger'd if the param exists

        private int _isGuardingHash;
        private int _parryHash;
        private bool _hasIsGuarding;
        private bool _hasParry;

        private void Awake()
        {
            if (guard == null) guard = GetComponent<PlayerGuard>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            _isGuardingHash = Animator.StringToHash(isGuardingBool);
            _parryHash = Animator.StringToHash(parryTrigger);
            CacheParams();
        }

        private void CacheParams()
        {
            _hasIsGuarding = false;
            _hasParry = false;
            if (animator == null) return;
            foreach (var p in animator.parameters)
            {
                if (p.name == isGuardingBool) _hasIsGuarding = true;
                if (p.name == parryTrigger) _hasParry = true;
            }
        }

        private void OnEnable()
        {
            if (guard != null)
            {
                guard.Parried += OnClashFlash;
                guard.Guarded += OnClashFlash;
            }
        }

        private void OnDisable()
        {
            if (guard != null)
            {
                guard.Parried -= OnClashFlash;
                guard.Guarded -= OnClashFlash;
            }
            if (animator != null && _hasIsGuarding) animator.SetBool(_isGuardingHash, false);
        }

        private void Update()
        {
            if (animator == null || guard == null || !_hasIsGuarding) return;
            // spec item 2: the Animator follows the SAME "defensive action active" signal the
            // GuardVolume + movement + pose use - so a released tap can't leave the anim out of
            // guard while the capsule is still live.
            animator.SetBool(_isGuardingHash, guard.DefenseActionActive);
        }

        // Fired on a parry OR a guard - the quick arm-raise reaction. spec item 2 §3.4: play it on
        // a successful parry EVEN IF the guard button is still held (otherwise a held deflect has
        // no visible kick).
        private void OnClashFlash(Vector3 _)
        {
            if (animator == null || !_hasParry) return;
            animator.SetTrigger(_parryHash);
        }

        public void EditorConfigure(PlayerGuard g, Animator a)
        {
            guard = g;
            animator = a;
        }
    }
}
