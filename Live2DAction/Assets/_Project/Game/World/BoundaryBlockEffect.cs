using UnityEngine;
using Live2DAction.Core;
using Live2DAction.Input;

namespace Live2DAction.World
{
    // Explicit user request ("本地周圍有一道看不到的牆碰撞體，這是對的，但能不能在任何角色碰撞此牆
    // 時設計一個被阻擋的特效，像是原神玩家不小心走到的未開放地圖邊界一樣") - "本地" is the user's own
    // chosen name (going forward) for the main ground spawn/rest area, i.e. GreyboxSceneBuilder's
    // BoundaryWall_North/South/East/West (see that class's own comment - invisible collider-only
    // walls just past Ground's 30x30 footprint). The walls themselves already correctly block
    // movement and are staying exactly as they are (explicit "這是對的") - this only ADDS a visual
    // cue on touch, it never changes the blocking behavior.
    //
    // A CharacterController colliding with a plain (non-Rigidbody) solid BoxCollider does NOT
    // raise any event on the COLLIDER's own side (only OnControllerColliderHit on the toucher's
    // own GameObject) - detecting "something touched this wall" generically, without adding a
    // script to every current and future character, needs a second, purely-detection Collider
    // instead. BoundaryWallBlockEffectSetup adds a slightly larger (padded) trigger BoxCollider
    // alongside the wall's original solid one on this same GameObject - both live side by side,
    // the solid one keeps blocking movement exactly as before, this one only detects the touch
    // just before/at the moment of contact.
    public class BoundaryBlockEffect : MonoBehaviour
    {
        // Keeps repeatedly grinding against the wall from spamming the effect every single
        // physics step - same per-instance-cooldown idiom Portal.cs already uses
        // (teleportCooldownSeconds), just keyed to this wall instead of to a character.
        [SerializeField] private float cooldownSeconds = 0.6f;

        // 2026-08-22, real playtested bug ("我在圍牆內自由行走穿梭後 牆體會變形") - this used to be
        // GetComponent<ParticleSystem>() (the ParticleSystem lived directly on the WALL's own
        // GameObject, enforced by a [RequireComponent(typeof(ParticleSystem))] this class no
        // longer has). OnTriggerEnter below repositions/rotates "the ripple's transform" to the
        // contact point every time a character brushes the wall - when the ripple IS the wall's
        // own transform, that call was relocating and rotating the wall itself (mesh, BoxCollider,
        // everything sharing that Transform) to wherever the player last touched it, which reads
        // exactly like the wall warping as you slide along it. Now a serialized reference to a
        // dedicated CHILD GameObject's ParticleSystem (see BoundaryWallBlockEffectSetup, which
        // creates that child) - repositioning the child moves only the visual burst, never the
        // wall's own Transform.
        [SerializeField] private ParticleSystem ripple;
        private Collider _solidWall;

        private float _cooldownUntil = -1f;

        private void Awake()
        {
            // This GameObject carries two BoxColliders (the original solid block + the new
            // padded trigger this component reacts to) - find the non-trigger one specifically
            // so the ripple's contact point is measured against the actual blocking surface, not
            // the larger detection volume.
            foreach (Collider candidate in GetComponents<Collider>())
            {
                if (!candidate.isTrigger)
                {
                    _solidWall = candidate;
                    break;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // Same "is this a real character, not scenery" signal Portal.cs's own
            // detectAnyCharacter path already uses (ResolvesAsCharacter) - kept consistent
            // rather than inventing a second convention for the same question.
            if (other.GetComponentInParent<Health>() == null)
            {
                return;
            }

            if (Time.time < _cooldownUntil)
            {
                return;
            }

            _cooldownUntil = Time.time + cooldownSeconds;

            // World-space cue, visible to anyone watching (not just whoever touched it) -
            // repositioned to the actual contact point on the solid wall's own surface each
            // time, so it reads as "the barrier pushed back right where you hit it" instead of a
            // fixed decoration sitting at the wall's own pivot.
            Vector3 contactPoint = _solidWall != null
                ? _solidWall.ClosestPoint(other.bounds.center)
                : transform.position;

            // Faces away from the map center ("本地" is centered on the world origin - see
            // GreyboxSceneBuilder's own CreateBoundaryWalls) so the shimmer reads as pushing
            // outward against the barrier, regardless of which of the four walls fired it.
            if (ripple != null)
            {
                Vector3 outward = new Vector3(contactPoint.x, 0f, contactPoint.z);
                if (outward.sqrMagnitude > 0.01f)
                {
                    ripple.transform.rotation = Quaternion.LookRotation(outward.normalized, Vector3.up);
                }
                ripple.transform.position = contactPoint;
                ripple.Play();
            }

            // The screen-edge vignette (BoundaryBlockHud) only means anything for whoever is
            // actually looking through the camera - see that class's own comment for why this is
            // player-only while the ripple above fires for any character.
            if (other.GetComponentInParent<PlayerInputProvider>() != null && BoundaryBlockHud.Instance != null)
            {
                BoundaryBlockHud.Instance.Pulse();
            }
        }
    }
}
