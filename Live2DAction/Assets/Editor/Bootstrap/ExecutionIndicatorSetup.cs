using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Combat;
using Live2DAction.UI;

namespace Live2DAction.EditorTools
{
    // 2026-08-18, explicit user request ("架勢條滿格時，角色的胸口上出現紅色圓圈 圓圈內有個紅色小
    // 點在閃爍，整體視覺幫我用最專業的精緻外觀來設計") - adds a world-space "execution ready"
    // indicator (soft-glow red ring + blinking core dot, see ExecutionRing.png/ExecutionDot.png's
    // own procedural-generation comment for the visual design) at chest height on every character
    // that has a StancePoise component, rather than a hardcoded name list like StanceBarSetup's
    // own Apply()/ApplyToMecha split - this indicator only ever needs to exist alongside
    // StancePoise itself, so deriving the target list from that component directly means a new
    // stance-bearing character automatically gets it without a matching new menu item.
    internal static class ExecutionIndicatorSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string RingSpritePath = "Assets/_Project/UI/Textures/ExecutionRing.png";
        private const string DotSpritePath = "Assets/_Project/UI/Textures/ExecutionDot.png";

        // 2026-08-18, real playtested bug ("這個處決的紅圈提示太大了 而且沒有定位在角色胸口") -
        // the original 0.42/0.15 sizing was picked before ever screenshotting Enemy
        // specifically, and turned out to span nearly the character's ENTIRE torso width
        // (Enemy's actual body is much narrower than Player's - see this file's own history:
        // 0.42 was roughly 87% of Enemy's measured body width, vs under 10% of Player's,
        // because both characters share one fixed absolute size despite very different builds).
        // Shrunk to a size that reads as "a marker ON the chest" rather than "a ring spanning
        // shoulder to shoulder" - re-verified by screenshot on Enemy specifically this time
        // (the tightest-proportioned of the three), same ~0.36 dot/ring ratio kept from before.
        private static readonly Vector2 RingSize = new Vector2(0.2f, 0.2f);
        private static readonly Vector2 DotSize = new Vector2(0.07f, 0.07f);

        // Fraction of MeasureVisualTopLocalY (the head-top reference HealthBarSetup's own bar
        // stack is measured from) used to place this at chest height instead - roughly the
        // torso/sternum band on a humanoid figure, well below the head-level bar stack
        // (Health/Energy/Stance bars all sit AT headTop + a small margin) and well above the hips.
        // 2026-08-18: lowered from 0.6 to 0.5 in the same pass as the size fix above - 0.6 read
        // closer to the collar/neckline than the chest once the ring was small enough to actually
        // judge its position precisely instead of being lost inside an oversized circle.
        private const float ChestHeightFraction = 0.5f;

        // 2026-08-18, real bug caught by screenshot verification - the Canvas's local X/Z
        // defaulted to (0, 0), i.e. the character's own root pivot, which sits INSIDE the torso
        // volume (the ring/dot are a paper-thin quad, not a real 3D object) - the opaque body
        // mesh fully occluded it from every angle tested, so it never actually became visible
        // despite its alpha being computed correctly. Offsetting forward (local +Z, this rig's
        // own forward axis regardless of current world yaw) by this amount pushes it clear of the
        // chest surface so it renders in front of the body instead of buried inside it.
        private const float ChestForwardOffset = 0.28f;

        [MenuItem("Tools/Live2DAction/Add Execution Ready Indicator To All Stance Characters")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            StancePoise[] allStance = Object.FindObjectsByType<StancePoise>(FindObjectsSortMode.None);
            if (allStance.Length == 0)
            {
                Debug.LogError("No StancePoise components found in " + ScenePath);
                return;
            }

            Sprite ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RingSpritePath);
            Sprite dotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DotSpritePath);
            if (ringSprite == null || dotSprite == null)
            {
                Debug.LogError("Execution indicator sprites not found - expected " + RingSpritePath + " and " + DotSpritePath);
                return;
            }

            foreach (StancePoise stance in allStance)
            {
                AddIndicator(stance, ringSprite, dotSprite);
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Added execution-ready indicators to " + allStance.Length + " StancePoise character(s).");
        }

        private static void AddIndicator(StancePoise stance, Sprite ringSprite, Sprite dotSprite)
        {
            GameObject owner = stance.gameObject;

            ExecutionReadyIndicator existing = owner.GetComponentInChildren<ExecutionReadyIndicator>(true);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            var canvasGo = new GameObject("ExecutionReadyIndicatorCanvas");

            // 2026-08-19, real playtested bug ("蹲下時準確地落在胸口 所有角色都一樣") - this used
            // to be a fixed local Y computed once from the STANDING pose's measured bounds. Fine
            // for WorldSpaceHealthBar/StanceBar (a floating UI readout meant to stay a constant
            // height above the head regardless of pose), but this indicator is meant to sit ON the
            // body itself - a root-relative offset stayed pinned at "chest height while standing"
            // the whole time, floating well above the character's actual chest once the Staggered
            // kneel pose visibly bent it over.
            //
            // First attempt: parent directly to the Animator's Chest bone with a fixed LOCAL
            // offset, letting Unity's animation system move it automatically. Screenshot-verified
            // this still landed WRONG in the actual kneel pose - KneelingDown.fbx folds the whole
            // spine forward so far that the Chest bone's own local "forward" axis ends up pointing
            // down toward the thighs, not out toward the camera, so the fixed local offset put the
            // ring on the knee instead of the chest. Fixed properly on the ExecutionReadyIndicator
            // side instead (see its own positionAnchor comment): still parented under the
            // character's root here for a tidy Hierarchy, but positionAnchor is wired to the Chest
            // bone so LateUpdate can recompute a camera-facing offset from its LIVE position every
            // frame, at whatever rotation the current pose has bent it to.
            Animator animator = owner.GetComponentInChildren<Animator>();
            Transform chestBone = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.Chest)
                : null;

            canvasGo.transform.SetParent(owner.transform, false);
            if (chestBone == null)
            {
                // Fallback for anything with no Humanoid Animator to hang a bone off of (e.g.
                // Player2/機甲戰士 - see this file's own "無 Animator" precedent in
                // DeathAnimationSetup) - same root-relative approach as before. No pose-tracking
                // problem to solve here either: characters without an Animator have no
                // StaggerAnimationLink-driven kneel pose to track in the first place, so a fixed
                // offset from the standing measurement is already correct for every pose they can
                // actually be in.
                float chestY = ResolveChestLocalY(owner);
                canvasGo.transform.localPosition = new Vector3(0f, chestY, ChestForwardOffset);
            }

            canvasGo.transform.localRotation = Quaternion.identity;
            canvasGo.transform.localScale = Vector3.one;

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            canvasRect.sizeDelta = RingSize;

            Image ring = CreateCenteredImage(canvasGo.transform, "Ring", ringSprite, RingSize);
            Image dot = CreateCenteredImage(canvasGo.transform, "Dot", dotSprite, DotSize);

            // Deliberately NOT pre-zeroed here (an earlier version of this method did, to start
            // invisible before IsStaggered ever goes true) - ExecutionReadyIndicator captures
            // whatever color is on the Image the first time its own Update() runs and treats that
            // as the "fully visible" reference alpha to pulse/blink against (see its own
            // _colorsCaptured comment). Zeroing it here meant it captured 0 as that reference and
            // multiplying by 0 forever, so the ring/dot NEVER became visible even while genuinely
            // staggered - real bug caught the first time this was actually screenshotted. Leaving
            // full alpha (whatever the sprite's own baked color is) here and trusting
            // ExecutionReadyIndicator's own Update() to zero it out on frame one while not
            // staggered instead.
            ExecutionReadyIndicator indicator = canvasGo.AddComponent<ExecutionReadyIndicator>();
            var so = new SerializedObject(indicator);
            so.FindProperty("stance").objectReferenceValue = stance;
            so.FindProperty("ringImage").objectReferenceValue = ring;
            so.FindProperty("dotImage").objectReferenceValue = dot;
            // Null for the no-Animator fallback (chestBone == null above) - ExecutionReadyIndicator's
            // own LateUpdate only does the camera-facing recompute when this is actually set, and
            // simply keeps whatever local position was set above otherwise.
            so.FindProperty("positionAnchor").objectReferenceValue = chestBone;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 2026-08-18, real playtested bug (same report as the size/position fix above) - this
        // used to call HealthBarSetup.MeasureVisualTopLocalY(owner) directly every time, but that
        // re-measures the "Visual" hierarchy's renderer bounds FRESH each run, and Player's Visual
        // hierarchy now includes a large held sword prop (this session's own "Genshin sword scene
        // decoration") whose bounds extend well past the character's actual head - so a fresh
        // measurement today came back much taller than reality, landing this indicator up at the
        // head-level bar stack's own height instead of down at the chest. The EXISTING
        // WorldSpaceHealthBar (if one's already been placed) is a known-good, already-correct
        // reference for where headTop actually is - reading its own local Y back out (undoing
        // HealthBarSetup.MarginAboveHead) avoids re-measuring at all. Only falls back to a fresh
        // MeasureVisualTopLocalY call if no health bar exists yet to read from (matches
        // StanceBarSetup.ResolveStanceBarLocalY's own fallback chain for the same reason).
        private static float ResolveChestLocalY(GameObject owner)
        {
            WorldSpaceHealthBar healthBar = owner.GetComponentInChildren<WorldSpaceHealthBar>(true);
            if (healthBar != null)
            {
                float headTop = healthBar.transform.localPosition.y - HealthBarSetup.MarginAboveHead;
                return headTop * ChestHeightFraction;
            }

            return HealthBarSetup.MeasureVisualTopLocalY(owner) * ChestHeightFraction;
        }

        private static Image CreateCenteredImage(Transform parent, string name, Sprite sprite, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;

            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white; // sprite already carries the red/glow color baked in
            image.raycastTarget = false;
            // 2026-08-19, real playtested bug ("硬直紅圈特效外邊有的透明正方形框 請移除") - Image's
            // default (useSpriteMesh=false) always draws a full rectangular quad sized to the
            // RectTransform, sampling the sprite's texture rect - the PNG's own alpha is correctly
            // 0 in the corners (verified pixel-by-pixel), but GPU texture compression on import
            // (Standalone target defaults to a compressed format) can lift those corners just
            // barely above 0, which is imperceptible on its own but reads as a faint square outline
            // once anything downstream (bloom, additive blending) amplifies it. Sprite import is
            // already spriteMeshType=Tight, so setting useSpriteMesh=true here makes uGUI build
            // geometry from that tight silhouette instead of a full quad - the transparent corners
            // have no geometry at all any more, not just near-zero alpha, so this is immune to the
            // compression artifact regardless of its exact cause.
            image.useSpriteMesh = true;
            return image;
        }
    }
}
