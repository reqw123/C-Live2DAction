using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;

namespace Live2DAction.EditorTools
{
    // Swaps the Player's visual for "Player5" - a Chinese mobile-game character asset
    // (codenamed "lacrimosa" in its own material/texture names) the user dropped into
    // Player5Anime/_FBX/Player5.fbx (see Docs/ASSET_LICENSES.md for provenance caveats -
    // unverified, same category as MechaModel_DoNotShip). Replaces Maya, who stays in the
    // project as a backup/spare (same reasoning as PlayerMayaVisualSetup replacing the
    // Universal Base Characters placeholder).
    //
    // Unlike Maya, this asset ships as a raw FBX only - no pre-built Prefab, no Materials
    // folder, no AnimatorController, and (before this tool first runs) no Humanoid Avatar:
    // 2026-08-13 investigation found the ModelImporter defaulted to Generic because Unity
    // doesn't auto-detect Humanoid on import, even though the actual skeleton is a standard
    // 3ds Max Biped ("Bip001-Pelvis", "Bip001-L-Thigh", etc.) buried under ~274 bones of
    // hair/cloth/finger/weapon-socket rigging - EnsureHumanoidRig below flips ModelImporter
    // to Human with CreateFromThisModel, which Unity's own name-based auto-mapper resolves
    // cleanly (confirmed: produces an isValid=true Avatar) without needing a hand-built
    // HumanDescription. That's also why this borrows Maya's NewAnimator.controller instead
    // of using the FBX's own single embedded clip (a 13s "stand_show" turntable pose, not a
    // Idle/Walk/Run loop) - a valid Humanoid Avatar means Maya's Humanoid-authored clips
    // retarget onto this skeleton directly.
    //
    // The raw mesh also imports at ~183 units tall (authored in centimeters, no corrective
    // Scale Factor baked into the FBX) - ScaleToMatchMaya below rescales it to Maya's own
    // measured height instead of a hardcoded constant, same "derive it, don't hardcode it"
    // reasoning as VisualFeetOffset, so the two characters stay proportionate even if either
    // asset is ever swapped for a revised version.
    internal static class Player5VisualSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string FbxPath = "Assets/_Project/Characters/Placeholder/Player5Anime/_FBX/Player5.fbx";
        private const string TextureFolder = "Assets/_Project/Characters/Placeholder/Player5Anime/Texture";
        private const string MayaPrefabPath = "Assets/_Project/Characters/Placeholder/MayaAnime/Prefabs/Maya.prefab";
        private const string MayaControllerPath = "Assets/_Project/Characters/Placeholder/MayaAnime/Animator/NewAnimator.controller";

        // Best-effort name match between the FBX's material names and the loose texture
        // files that happen to sit next to it - the two naming schemes don't agree
        // ("MI_player_004_lacrimosa_01" vs "T_player_004_lacrimosa_01_d"), so Unity's own
        // importer-level texture search (materialSearch: Local) never found them on import.
        // Only 7 of the FBX's 10 materials have a texture available in this folder at all -
        // eyelash/gaoguang(highlight)/03 are missing from what the user provided, so those
        // three are left on the importer's default (untextured URP/Lit) rather than guessed
        // at; a real texture for them would need to be added to this folder and this table
        // extended by whoever manages the source asset.
        private static readonly Dictionary<string, string> MaterialToTexture = new Dictionary<string, string>
        {
            { "MI_player_004_lacrimosa_01", "T_player_004_lacrimosa_01_d" },
            { "MI_player_004_lacrimosa_02", "T_player_004_lacrimosa_02_d" },
            { "MI_player_004_lacrimosa_face", "T_player_004_lacrimosa_face_d" },
            { "MI_player_004_lacrimosa_eye", "T_player_004_lacrimosa_eye_d" },
            { "MI_player_004_lacrimosa_hair_01", "T_player_004_lacrimosa_hair_01_d" },
            { "MI_player_004_lacrimosa_hair_02", "T_player_004_lacrimosa_hair_02_d" },
            { "MI_common_face_mask", "common_face_d" },
        };

        private const string MaterialsFolder = "Assets/_Project/Characters/Placeholder/Player5Anime/Materials";

        [MenuItem("Tools/Live2DAction/Replace Player Visual With Player5 (Lacrimosa)")]
        public static void Apply()
        {
            EnsureHumanoidRig();
            ExtractAndAssignMaterials();

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError("Could not load FBX at " + FbxPath);
                return;
            }

            RuntimeAnimatorController mayaController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MayaControllerPath);
            if (mayaController == null)
            {
                Debug.LogError("Could not load Maya's AnimatorController at " + MayaControllerPath);
                return;
            }

            float targetHeight = MeasureMayaHeight();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            // Only the existing "Visual" child, NOT every child of Player (as an earlier
            // version of this method did, copying PlayerMayaVisualSetup's loop verbatim) -
            // 2026-08-13 real bug report ("現在看不到玩家血量條"): Player's HealthBarCanvas
            // (added by HealthBarSetup, see AddHealthBar call below) is ALSO a direct child
            // of Player, sitting as a sibling of Visual, so the old blanket
            // "destroy every child" loop silently took it out as collateral damage the very
            // first time this ran. EnemyHumanoidVisualSetup already has the right pattern
            // (transform.Find("Visual") specifically) - this now matches it, so nothing
            // else parented under Player survives a re-run unscathed.
            Transform existingVisual = player.transform.Find("Visual");
            if (existingVisual != null)
            {
                Object.DestroyImmediate(existingVisual.gameObject);
            }

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, player.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            float scale = ScaleToHeight(visual, targetHeight);
            visual.transform.localScale = Vector3.one * scale;
            // VisualFeetOffset is derived from the CharacterController alone (not from the
            // visual), so it doesn't need recomputing post-scale: Player5's mesh origin sits
            // at its feet same as Maya's (bounds dump confirmed min.y close to 0 before any
            // scaling), and scaling around that same local origin keeps it there.
            visual.transform.localPosition = PlayerMayaVisualSetup.VisualFeetOffset(player);

            Animator animator = visual.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("Player5 prefab has no Animator - Humanoid avatar setup may have failed.");
            }
            else
            {
                animator.runtimeAnimatorController = mayaController;
                // Same reasoning as PlayerMayaVisualSetup: CharacterController drives
                // position, so root motion would fight it.
                animator.applyRootMotion = false;
            }

            PlayerMayaVisualSetup.RemoveEmbeddedCameraRig(visual);
            PlayerMayaVisualSetup.RemoveEmbeddedPhysicsRig(visual);

            RewireAnimatorLinks(player, animator);

            // Re-measures against Player5's actual head height (MeasureVisualTopLocalY reads
            // the "Visual" child's real Renderer bounds) - HealthBarSetup.AddHealthBar already
            // replaces any existing bar first, so this is safe to call unconditionally
            // whether or not the destroy-all-children bug above ever ran on this scene.
            HealthBarSetup.AddHealthBar(player);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Replaced Player visual with Player5 (lacrimosa placeholder). scale={scale} targetHeight={targetHeight}");
        }

        // Destroying and recreating Player's Visual child every run (see Apply() above)
        // orphans any existing component on Player whose serialized field pointed at Maya's
        // now-destroyed Animator instance - it silently goes null (Unity doesn't null-check
        // component references across a DestroyImmediate, it just leaves a dangling fileID: 0
        // in the scene file). 2026-08-13, found via scene YAML inspection after the first
        // successful Player5 swap: CharacterAttackAnimationLink's "animator" field (wired by
        // WireCharacterAttackAnimationLink) AND CharacterAnimatorLink's (wired by whatever
        // set Maya up originally) had both gone null - attacks/locomotion-blend still worked
        // on Enemy/the enemy (a different GameObject, untouched by this swap) but neither
        // fired on Player itself. Re-points both at Player5's own Animator here instead of
        // requiring a separate manual re-run of those tools - same SerializedObject-based
        // wiring WireCharacterAttackAnimationLink.WireCharacter() uses. No new
        // AnimatorController wiring is needed for attacks specifically - Attack1/2/3 already
        // live on Maya's shared NewAnimator.controller (see class comment) - this only fixes
        // the reference.
        private static void RewireAnimatorLinks(GameObject player, Animator animator)
        {
            if (animator == null)
            {
                return;
            }

            RewireAnimatorField<CharacterAttackAnimationLink>(player, animator);
            RewireAnimatorField<CharacterAnimatorLink>(player, animator);
        }

        private static void RewireAnimatorField<T>(GameObject player, Animator animator) where T : MonoBehaviour
        {
            T link = player.GetComponent<T>();
            if (link == null)
            {
                link = player.AddComponent<T>();
            }

            var so = new SerializedObject(link);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Flips the importer to Humanoid with Unity's own automatic bone mapping (as opposed
        // to a hand-authored HumanDescription) - see the class comment for why this works
        // despite the importer defaulting to Generic. Idempotent: re-running Apply() after
        // the first successful run is a no-op here since the .meta already has this set.
        private static void EnsureHumanoidRig()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
            if (importer.animationType == ModelImporterAnimationType.Human)
            {
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        // The FBX's materials come in as transient sub-assets generated by the ModelImporter
        // (materialLocation is set to External, but nothing has ever actually been
        // extracted - no Materials folder existed before this ran) - editing them in place
        // with SetTexture/SetDirty/SaveAssets, as an earlier version of this method did,
        // silently didn't stick: they're regenerated from the FBX on every reimport, so the
        // very next Editor session that touched this asset wiped the textures back to
        // NULL (confirmed empirically - see 2026-08-13 diagnostic run, BaseMap read back
        // NULL from a fresh process after a save that reported no errors). Real .mat asset
        // files under Materials/, remapped onto the importer via AddRemap - the same thing
        // the Editor's own "Extract Materials..." button does - are what Maya's own working
        // Materials folder relies on, so this does the same rather than fighting the
        // importer's regeneration.
        private static void ExtractAndAssignMaterials()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            var importer = (ModelImporter)AssetImporter.GetAtPath(FbxPath);

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Characters/Placeholder/Player5Anime", "Materials");
            }

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            bool anyRemapped = false;

            foreach (Object asset in allAssets)
            {
                if (!(asset is Material embeddedMaterial))
                {
                    continue;
                }

                string matPath = MaterialsFolder + "/" + embeddedMaterial.name + ".mat";
                Material persisted = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (persisted == null)
                {
                    persisted = new Material(embeddedMaterial);
                    AssetDatabase.CreateAsset(persisted, matPath);
                }

                if (urpLit != null && persisted.shader != urpLit)
                {
                    persisted.shader = urpLit;
                }

                if (MaterialToTexture.TryGetValue(embeddedMaterial.name, out string textureName))
                {
                    string texturePath = TextureFolder + "/" + textureName + ".png";
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (texture != null)
                    {
                        persisted.SetTexture("_BaseMap", texture);
                    }
                    else
                    {
                        Debug.LogWarning("Player5 material texture not found: " + texturePath);
                    }
                }

                EditorUtility.SetDirty(persisted);

                var sourceId = new AssetImporter.SourceAssetIdentifier(typeof(Material), embeddedMaterial.name);
                importer.AddRemap(sourceId, persisted);
                anyRemapped = true;
            }

            if (anyRemapped)
            {
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                AssetDatabase.SaveAssets();
            }
        }

        // Measured fresh each run rather than hardcoded, so Player5 stays proportionate to
        // whatever Maya's asset actually is even if it's ever revised (same "derive it, don't
        // hardcode it" reasoning as VisualFeetOffset). Instantiated standalone (not parented
        // into the scene) purely to read its renderer bounds, then discarded.
        private static float MeasureMayaHeight()
        {
            GameObject mayaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MayaPrefabPath);
            if (mayaPrefab == null)
            {
                Debug.LogWarning("Could not load Maya prefab to measure height, falling back to 1.75m.");
                return 1.75f;
            }

            GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(mayaPrefab);
            float height = MeasureHeight(temp);
            Object.DestroyImmediate(temp);
            return height;
        }

        // Same pattern as MechaVisualSetup's own scale-to-target-height step (that
        // asset has the identical "raw import is some arbitrary size" problem).
        private static float ScaleToHeight(GameObject visual, float targetHeight)
        {
            float rawHeight = Mathf.Max(MeasureHeight(visual), 0.0001f);
            return targetHeight / rawHeight;
        }

        private static float MeasureHeight(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return 0f;
            }

            Bounds combined = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                combined.Encapsulate(r.bounds);
            }

            return combined.size.y;
        }
    }
}
