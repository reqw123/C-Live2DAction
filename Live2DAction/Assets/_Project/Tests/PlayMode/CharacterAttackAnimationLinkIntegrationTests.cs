using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Input;
using Object = UnityEngine.Object;

// Loads the real GreyboxTest scene and verifies CharacterAttackAnimationLink's trigger
// actually drives Player's real (CombatAnimatorSetup-wired) Animator into the Attack1 state -
// the EditMode tests only cover the pure index->trigger-name mapping, not that SetTrigger()
// cascades into a real state change against the actual project Animator Controller.
// Deliberately doesn't build a synthetic AnimatorController in-test (that needs
// UnityEditor.Animations, which the PlayMode test assembly doesn't reference and isn't meant
// to for a player-buildable test assembly) - the real scene's wiring is exactly what needs
// covering anyway.
public class CharacterAttackAnimationLinkIntegrationTests
{
    private class StubInputBehaviour : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; set; }
        public bool AttackPressed { get; set; }
        public bool DodgePressed { get; set; }
        public bool LockOnPressed { get; set; }
        public bool JumpPressed { get; set; }
        public bool UltimatePressed { get; set; }
        public bool FlyPressed { get; set; }
        public bool FlyDescendPressed { get; set; }
        public bool BoostPressed { get; set; } // 2026-08-20, flight system design - interface addition, stub needs it to compile
        public bool AimPressed { get; set; } // 2026-08-23, ranged weapon - interface addition, stub needs it to compile
        public bool FirePressed { get; set; }
        public bool ViewTogglePressed { get; set; } // 2026-08-23, first-person toggle - interface addition, stub needs it to compile
        public bool ZoomInPressed { get; set; } // 2026-08-23, aim-zoom controls - interface addition, stub needs it to compile
        public bool ZoomOutPressed { get; set; }
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected a private field named '{fieldName}' on {target.GetType().Name}");
        field.SetValue(target, value);
    }

    [UnityTest]
    public IEnumerator PlayerAttacking_TransitionsRealAnimatorToAttack1State()
    {
        SceneManager.LoadScene("GreyboxTest");
        yield return null;
        yield return null;

        GameObject player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in GreyboxTest scene");

        CharacterAttackAnimationLink link = player.GetComponent<CharacterAttackAnimationLink>();
        Assert.IsNotNull(link, "Player should have a CharacterAttackAnimationLink");

        Animator animator = (Animator)typeof(CharacterAttackAnimationLink)
            .GetField("animator", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(link);
        Assert.IsNotNull(animator, "CharacterAttackAnimationLink should have an Animator wired");

        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        var stub = player.AddComponent<StubInputBehaviour>();
        SetField(combat, "inputSource", stub);

        stub.AttackPressed = true;
        float start = Time.realtimeSinceStartup;
        bool reachedAttack1 = false;
        while (Time.realtimeSinceStartup - start < 1f)
        {
            yield return null;
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Attack1"))
            {
                reachedAttack1 = true;
                break;
            }
        }

        Assert.IsTrue(reachedAttack1, "Pressing attack should transition Player's real Animator into the Attack1 state within 1s");

        Object.Destroy(stub);
    }
}
