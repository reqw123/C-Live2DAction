using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // 2026-09-11, user request ("先在此人物放在露營區土地上") - places the new 猜猜看 character
    // (a Meshy AI "Studio Portrait" biped, user-supplied zip: Assets/_Project/Characters/
    // Placeholder/GuessWho/) standing on the 露營區 ground in Map_Camp.unity. Visual placement
    // only - no CharacterController/Health/AI yet (Map_Camp itself is greybox-only per this
    // session's earlier "場地優先，角色晚點做" decision; this is the next small step, not the
    // full combat wiring TenLeggedBugSetup-style tools do for other characters).
    //
    // Generic rig (not Humanoid) - the FBX ships its own Armature + two baked clips
    // (Walking/Running), no retargeting needed since nothing else will ever play on this rig.
    internal static class GuessWhoCharacterSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/Map_Camp.unity";
        private const string CharacterFolder = "Assets/_Project/Characters/Placeholder/GuessWho";
        private const string WalkFbxPath = CharacterFolder + "/FBX/GuessWho_Walking.fbx";
        private const string MaterialsFolder = CharacterFolder + "/Materials";
        private const string ControllerPath = CharacterFolder + "/GuessWho_Animator.controller";
        private const string GroundName = "露營區";
        private const string CharacterName = "猜猜看";

        [MenuItem("Tools/Live2DAction/Place GuessWho Placeholder On Camp Ground")]
        public static void Apply()
        {
            ConfigureTextureImports();
            Material mat = BuildMaterial();

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WalkFbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError("[GuessWhoCharacterSetup] Could not load FBX at " + WalkFbxPath);
                return;
            }

            AnimatorController controller = BuildAnimatorController(fbxAsset);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject ground = GameObject.Find(GroundName);
            if (ground == null)
            {
                Debug.LogError($"[GuessWhoCharacterSetup] '{GroundName}' not found in {ScenePath}.");
                return;
            }

            GameObject existing = GameObject.Find(CharacterName);
            if (existing != null) Object.DestroyImmediate(existing);

            GameObject character = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
            character.name = CharacterName;

            // "Icosphere" (scale 100, radius ~1, fully engulfing the character) is a Meshy export
            // artifact, not part of the actual model - it washes out screenshots pure white/grey
            // (camera ends up rendering from inside a giant grey sphere). Found 2026-09-11.
            Transform icosphere = character.transform.Find("Icosphere");
            if (icosphere != null) Object.DestroyImmediate(icosphere.gameObject);

            // Stand on the ground's actual top face, centred on the plot (a few metres off the
            // north wall gap so it doesn't block the CampGate_Exit sightline).
            Collider groundCollider = ground.GetComponent<Collider>();
            float groundTopY = groundCollider != null ? groundCollider.bounds.max.y : ground.transform.position.y + 0.5f;
            Vector3 spawnXZ = ground.transform.position + new Vector3(0f, 0f, 10f);

            SkinnedMeshRenderer smr = character.GetComponentInChildren<SkinnedMeshRenderer>(true);
            float feetLocalY = smr != null ? smr.bounds.min.y : 0f;
            character.transform.position = new Vector3(spawnXZ.x, groundTopY - feetLocalY, spawnXZ.z);
            character.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face south, into the plot / away from the gate

            if (smr != null) smr.sharedMaterial = mat;

            Animator animator = character.GetComponent<Animator>();
            if (animator == null) animator = character.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            EditorUtility.SetDirty(character);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[GuessWhoCharacterSetup] Placed '{CharacterName}' on '{GroundName}' at " +
                      character.transform.position.ToString("F2") + " with a looping Walking idle. " +
                      "Visual placeholder only - no CharacterController/Health/AI yet.");
        }

        // Single default state looping the FBX's own Walking clip, so the placeholder reads as
        // "alive" standing there instead of a frozen bind/T-pose. No parameters - nothing drives
        // this yet.
        private static AnimatorController BuildAnimatorController(GameObject fbxAsset)
        {
            AnimationClip walkClip = null;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(WalkFbxPath))
            {
                if (o is AnimationClip clip && !clip.name.StartsWith("__preview__") && clip.name.EndsWith("Walking"))
                {
                    walkClip = clip;
                    break;
                }
            }

            if (walkClip == null)
            {
                Debug.LogWarning("[GuessWhoCharacterSetup] No Walking clip found on " + WalkFbxPath + " - character will T-pose.");
                return null;
            }

            EnsureFolder(MaterialsFolder); // shares the character folder tree
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                AnimatorStateMachine sm = controller.layers[0].stateMachine;
                AnimatorState idle = sm.AddState("Walking");
                idle.motion = walkClip;
                sm.defaultState = idle;
            }
            else
            {
                controller.layers[0].stateMachine.defaultState.motion = walkClip;
            }

            return controller;
        }

        private static void ConfigureTextureImports()
        {
            string folder = CharacterFolder + "/Textures";
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null) continue;

                bool changed = false;
                if (path.Contains("_Normal") && importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    changed = true;
                }
                else if ((path.Contains("_Metallic") || path.Contains("_Roughness")) && importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                }
            }
        }

        // BaseColor + Normal wired; Metallic/Roughness approximated as flat values rather than
        // channel-packed into URP/Lit's single _MetallicGlossMap - same call as
        // Player5WeaponSetup.BuildMaterial (packing two source textures into one isn't worth it
        // for a prototype placeholder).
        private static Material BuildMaterial()
        {
            string matPath = MaterialsFolder + "/GuessWho_Body.mat";
            EnsureFolder(MaterialsFolder);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool isNew = material == null;
            if (isNew) material = new Material(urpLit);

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterFolder + "/Textures/GuessWho_BaseColor.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(CharacterFolder + "/Textures/GuessWho_Normal.png");

            if (baseColor != null) material.SetTexture("_BaseMap", baseColor);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetFloat("_Metallic", 0.0f);
            material.SetFloat("_Smoothness", 0.4f);

            if (isNew) AssetDatabase.CreateAsset(material, matPath);
            else EditorUtility.SetDirty(material);

            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
