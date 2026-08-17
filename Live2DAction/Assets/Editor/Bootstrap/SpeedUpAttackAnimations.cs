using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Live2DAction.EditorTools
{
    // Speeds up the Attack1/Attack2/Attack3 AnimatorStates CombatAnimatorSetup wired into
    // both Maya's AND Arisa's NewAnimator.controller (shared clips - see CombatAnimatorSetup's
    // own comment on why one set of 3 Mixamo clips covers both characters).
    //
    // 2026-08-13, first version: only bumped Maya's (the player's) speed on an explicit "出拳
    // 更快" request, to 1.4/1.4/1.3 - Arisa's (the enemy's) was deliberately left untouched
    // since that request was player-only.
    //
    // 2026-08-17 rewrite, explicit user request ("調整玩家 敵人的攻擊動畫與攻擊判定幀的匹配性"):
    // the 2026-08-13 speeds only ever addressed "feels slow", not whether the swing's actual
    // visual impact frame lines up with when ComboAttackState resolves the hit
    // (AttackData.StartupSeconds, the Startup->Active transition). Measured live (Animator.Play
    // sampled at fixed normalizedTime values + screenshots, since normalizedTime maps to a fixed
    // fraction of the clip regardless of playback speed) that CrossPunch's fist reaches full
    // extension at roughly 40-45% through the clip - at the old 2x speed (1.0s effective) that's
    // ~0.42s after the swing starts, but LightAttack1.startupFrames was only 6 (0.1s @ 60fps) -
    // the hit was resolving a third of a second before the fist visually got anywhere near the
    // target, i.e. exactly "the swing drags after the hit already landed", just never fixed at
    // the root (frame data vs. animation timing) rather than papered over with more speed.
    // HookPunch/Uppercut weren't independently measured the same way - assumed the same ~40-45%
    // impact fraction (Uppercut estimated slightly later at ~48%, a rising strike naturally
    // connects a bit further into its motion) since both are the same genre of Mixamo strike clip.
    //
    // This pass moves the fix onto BOTH ends: speed is picked so the animation stays reasonably
    // snappy (not the extreme ~6-9x that would be needed to drag the visual impact all the way
    // down to the OLD 6-10 frame startup), and LightAttack1/2/3 + EnemyAttack's startupFrames
    // were raised to match wherever the now-chosen speed actually puts that impact frame (see
    // each asset's own updated values) - the two were solved together, not speed-only. Also
    // finally applies the same treatment to Arisa (Player4's attack animation, driven by
    // EnemyAttack/CrossPunch/Attack1) - the enemy's swing had exactly the same mismatch and had
    // never been touched at all (still 1x, i.e. CrossPunch's already-late impact frame landing
    // even later in real time).
    internal static class SpeedUpAttackAnimations
    {
        private static readonly string[] ControllerPaths =
        {
            "Assets/_Project/Characters/Placeholder/MayaAnime/Animator/NewAnimator.controller",
            "Assets/_Project/Characters/Placeholder/ArisaAnime/Animator/NewAnimator.controller",
        };

        // Chosen together with each LightAttack*/EnemyAttack asset's startupFrames - see this
        // class's own header comment for the measured impact fractions and reasoning. Recovery
        // frames were also retuned so Startup+Active+Recovery lands close to where
        // CombatAnimatorSetup's own Attack->Locomotion exit transition fires (normalizedTime
        // 0.9 of the clip), so the gameplay state and the visual state return to neutral at
        // roughly the same time instead of one trailing the other.
        private static readonly (string state, float speed)[] Speeds =
        {
            ("Attack1", 3.4f), // CrossPunch, 2.0s raw, ~42% impact -> hit lands at ~0.25s (matches LightAttack1/EnemyAttack startupFrames=15)
            ("Attack2", 3.2f), // HookPunch, 2.167s raw, ~42% impact -> ~0.28s (LightAttack2 startupFrames=17)
            ("Attack3", 2.1f), // Uppercut, 1.333s raw, ~48% impact -> ~0.30s (LightAttack3 startupFrames=18)
            // 2026-08-17, explicit user request ("把它變成滑鼠左鍵續力攻擊") - MmaKick, 1.667s
            // raw. Measured directly (AnimationMode.SampleAnimationClip on the actual rig,
            // tracking RightFoot's distance from Hips across the clip - the chamber/extend/
            // retract pattern peaks at t=0.46, i.e. 0.767s raw) rather than eyeballed like the
            // three punches above, since a kick's impact point isn't as visually obvious from a
            // few spaced screenshots. Chosen as the heaviest hit (4th combo step, a kick
            // "finisher") - slower than the punches on purpose so its startup reads as weightier
            // -> hit lands at ~0.33s (LightAttack4 startupFrames=20).
            ("Attack4", 2.3f),
        };

        [MenuItem("Tools/Live2DAction/Speed Up Player And Enemy Attack Animations")]
        public static void Apply()
        {
            foreach (string controllerPath in ControllerPaths)
            {
                ApplyToController(controllerPath);
            }
        }

        private static void ApplyToController(string controllerPath)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                Debug.LogError("Could not load AnimatorController at " + controllerPath);
                return;
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach ((string stateName, float speed) in Speeds)
            {
                AnimatorState state = FindState(stateMachine, stateName);
                if (state == null)
                {
                    Debug.LogError($"Could not find state '{stateName}' in {controllerPath} - run Wire Combat Animations Into Both Animator Controllers first.");
                    continue;
                }

                state.speed = speed;
                Debug.Log($"{controllerPath}: {stateName}.speed = {speed}");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("Synced attack animation speed to hit-frame timing in " + controllerPath);
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == name)
                {
                    return child.state;
                }
            }

            return null;
        }
    }
}
