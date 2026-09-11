using UnityEngine;

namespace Live2DAction.Characters
{
    // 2026-09-11, user request ("猜猜看 alt靜走時請讓他把手放下來呈現伸直狀態，不然慢走的擺動姿勢看起來
    // 奇怪") - 猜猜看 shares Player's Humanoid-retargeted walk clip, whose arm-swing was choreographed
    // for Player's own proportions; on 猜猜看's body it just reads as an odd, mismatched wobble.
    //
    // Follow-up same day ("猜猜看 alt靜走狀態下的姿勢不對 你恢復成原模型的姿勢就行 然後隨著步伐緩慢擺動")
    // - the first version forced the arms to a HAND-GUESSED Humanoid muscle pose (HumanPoseHandler,
    // "Arm Down-Up" etc.), which looked wrong (guessed values, no visual feedback loop available -
    // Editor lacked OS focus so Play couldn't be watched). Replaced with something that can't be
    // wrong in the same way: restore each arm bone to 猜猜看's OWN AUTHORED bind pose (captured in
    // Awake, before the Animator ever evaluates a frame - at that instant the imported model is
    // still in its original rest pose), expressed RELATIVE TO THE CHARACTER ROOT so it still turns
    // correctly with him, then layer a small sway on top around the root's OWN world-space right
    // axis (not the bone's local axis, which is an unknown, rig-specific convention we'd otherwise
    // have to guess again) - swinging around a world-space direction anchored to the character's
    // current facing produces a correct forward/back swing regardless of that convention.
    //
    // Follow-up ("alt時手往下伸直 不要手背彎曲") - the first pass only restored upper arm + forearm,
    // leaving the WRIST (hand) still bent from the shared walk clip. Hands now get the same
    // bind-pose restore (no sway - just straightened).
    //
    // Player is untouched - this component only ever exists on 猜猜看's own GameObject.
    [RequireComponent(typeof(Animator))]
    public class HumanoidWalkArmsDown : MonoBehaviour
    {
        [Tooltip("Only overrides the arms while this is walking (CharacterMovement.IsWalking) - " +
                 "running/combat/guard poses are left alone.")]
        [SerializeField] private CharacterMovement movement;

        [Header("Sway (gentle, synced to a walking pace)")]
        [Tooltip("Seconds for one full left-right-left swing cycle - roughly a walking gait cycle.")]
        [SerializeField] private float swayCycleSeconds = 1.1f;
        [Tooltip("How far the upper arm swings forward/back from its resting pose, in degrees.")]
        [SerializeField] private float swayAmplitudeDegrees = 10f;
        [Tooltip("How fast the pose blends in/out when walking starts/stops (per second).")]
        [SerializeField] private float blendSpeed = 8f;

        private Animator _animator;
        private Transform _root;
        private bool _validAvatar;
        private float _blend;
        private float _phase;

        private Transform _leftUpperArm, _leftLowerArm, _leftHand, _rightUpperArm, _rightLowerArm, _rightHand;
        private Quaternion _leftUpperBind, _leftLowerBind, _leftHandBind, _rightUpperBind, _rightLowerBind, _rightHandBind;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _root = _animator.transform;
            _validAvatar = _animator.avatar != null && _animator.avatar.isHuman;
            if (!_validAvatar)
            {
                Debug.LogWarning("[HumanoidWalkArmsDown] " + name + ": Animator has no valid Humanoid avatar - disabled.", this);
                enabled = false;
                return;
            }

            _leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            _rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);

            // Captured here, before the Animator has evaluated a single frame - this IS 猜猜看's
            // own authored rest pose (the model's bind pose), not a guess. Stored relative to the
            // root so it stays correct as he turns.
            if (_leftUpperArm != null) _leftUpperBind = RelativeToRoot(_leftUpperArm);
            if (_leftLowerArm != null) _leftLowerBind = RelativeToRoot(_leftLowerArm);
            if (_leftHand != null) _leftHandBind = RelativeToRoot(_leftHand);
            if (_rightUpperArm != null) _rightUpperBind = RelativeToRoot(_rightUpperArm);
            if (_rightLowerArm != null) _rightLowerBind = RelativeToRoot(_rightLowerArm);
            if (_rightHand != null) _rightHandBind = RelativeToRoot(_rightHand);
        }

        private Quaternion RelativeToRoot(Transform bone) => Quaternion.Inverse(_root.rotation) * bone.rotation;

        private void LateUpdate()
        {
            if (!_validAvatar) return;

            bool walking = movement != null && movement.IsWalking;
            _blend = Mathf.MoveTowards(_blend, walking ? 1f : 0f, blendSpeed * Time.deltaTime);
            if (_blend <= 0.0001f) return;

            if (walking && swayCycleSeconds > 0.01f)
            {
                _phase += Time.deltaTime / swayCycleSeconds;
            }
            float swing = Mathf.Sin(_phase * Mathf.PI * 2f) * swayAmplitudeDegrees * _blend;

            ApplyArm(_leftUpperArm, _leftUpperBind, -swing);
            ApplyArm(_rightUpperArm, _rightUpperBind, swing); // opposite phase - natural counter-swing
            ApplyArm(_leftLowerArm, _leftLowerBind, 0f);
            ApplyArm(_rightLowerArm, _rightLowerBind, 0f);
            ApplyArm(_leftHand, _leftHandBind, 0f);
            ApplyArm(_rightHand, _rightHandBind, 0f);
        }

        // Restores the bone to its bind pose (turning correctly with the character), then swings
        // it around the character's CURRENT world-space right axis - a direction anchored to his
        // live facing, not the bone's own local axes, so this can't come out sideways/backwards
        // regardless of how this particular rig's bones happen to be oriented.
        private void ApplyArm(Transform bone, Quaternion bindRelativeToRoot, float swingDegrees)
        {
            if (bone == null) return;
            Quaternion bindWorldNow = _root.rotation * bindRelativeToRoot;
            Quaternion target = swingDegrees == 0f
                ? bindWorldNow
                : Quaternion.AngleAxis(swingDegrees, _root.right) * bindWorldNow;
            bone.rotation = _blend >= 0.999f ? target : Quaternion.Slerp(bone.rotation, target, _blend);
        }
    }
}
