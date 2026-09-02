using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Live2DAction.CameraSystem;
using Live2DAction.Characters;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // 2026-08-29, user request ("讓 player 守望者/cat 三者可以互相切換視角"). Cross-wires the cat and
    // the Watcher director so T works from the cat too, and C is ignored while watching:
    //   - ViewFocusDirector.catCamera / catController -> CatCamera (so ActiveCamera() picks it up
    //     when the cat is possessed).
    //   - the cat's control set is appended to ViewFocusDirector.suspendWhileWatching (so W/A/S/D
    //     in the Watcher view doesn't drive the cat; the director's snapshot-restore handles the
    //     "already disabled because you were the player" case).
    //   - CameraPossessionSwitcher.viewDirector -> the director (so C is a no-op mid-Watcher-view).
    //
    // Order-independent: called from the end of BOTH CatCharacterSetup and WatcherSetup, so
    // whichever menu the user runs second completes the link. No-op if either side is missing.
    internal static class WatcherCatWiring
    {
        public static void Wire()
        {
            ViewFocusDirector director = FindInScene<ViewFocusDirector>();
            GameObject catCameraGo = FindRootByName("CatCamera");
            GameObject catGo = FindRootByName("Cat");
            CameraPossessionSwitcher switcher = FindInScene<CameraPossessionSwitcher>();

            if (switcher != null && director != null)
            {
                var swSo = new SerializedObject(switcher);
                swSo.FindProperty("viewDirector").objectReferenceValue = director;
                swSo.ApplyModifiedPropertiesWithoutUndo();
            }

            if (director == null || catCameraGo == null || catGo == null)
            {
                return;
            }

            var so = new SerializedObject(director);
            so.FindProperty("catCamera").objectReferenceValue = catCameraGo.GetComponent<Camera>();
            so.FindProperty("catController").objectReferenceValue = catCameraGo.GetComponent<ThirdPersonCameraController>();

            // Append the cat's control set to suspendWhileWatching (dedup against what's there).
            SerializedProperty suspend = so.FindProperty("suspendWhileWatching");
            var current = new List<Object>();
            for (int i = 0; i < suspend.arraySize; i++)
            {
                current.Add(suspend.GetArrayElementAtIndex(i).objectReferenceValue);
            }

            void AddIfMissing(Behaviour b)
            {
                if (b != null && !current.Contains(b))
                {
                    current.Add(b);
                }
            }
            AddIfMissing(catGo.GetComponent<CharacterMovement>());
            AddIfMissing(catGo.GetComponent<PlayerCombat>());
            AddIfMissing(catGo.GetComponent<CatChargeAttack>());
            AddIfMissing(catGo.GetComponent<CatPounce>());
            AddIfMissing(catGo.GetComponent<CatAerialJudgment>());

            suspend.arraySize = current.Count;
            for (int i = 0; i < current.Count; i++)
            {
                suspend.GetArrayElementAtIndex(i).objectReferenceValue = current[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("WatcherCatWiring: cat <-> ViewFocusDirector linked - T now works from the cat, C is ignored while watching.");
        }

        private static T FindInScene<T>() where T : Component
        {
            foreach (T c in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                return c;
            }
            return null;
        }

        private static GameObject FindRootByName(string name)
        {
            foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (g != null && g.name == name && g.transform.parent == null)
                {
                    return g;
                }
            }
            return null;
        }
    }
}
