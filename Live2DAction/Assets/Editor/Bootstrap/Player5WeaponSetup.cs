using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Live2DAction.EditorTools
{
    // Attaches the "Wolf's Gravestone" claymore (2026-08-13, explicit user request - a direct
    // reproduction of a Genshin Impact/HoYoverse weapon asset, same unverified/non-commercial
    // risk tier as MechaModel_DoNotShip; user confirmed personal prototype use only, never
    // ship - see Docs/ASSET_LICENSES.md) to Player5's right hand.
    //
    // Static prop, not a Humanoid asset - imported as Generic (default), no Avatar needed.
    // Parented under the "Rhand_Weapon2" bone specifically (not "WeaponSocket", "wq_root_R",
    // or any of Player5's other candidate socket bones) per explicit user choice - that bone
    // sits at the end of the Bip001-R-Hand chain (see Player5's own bone-tree dump from its
    // original setup), so it inherits the hand's Humanoid-retargeted animation naturally.
    //
    // Grip position/scale are USER-TUNED VALUES, not derived from a formula - same
    // "authoritative until the user says otherwise" status as ThirdPersonCameraController's
    // distance/targetOffset (see memory: camera-user-tuned-values-are-authoritative). The
    // first version of this tool computed LocalPosition/Scale from the FBX's own measured
    // bounds (grip cylinder position, raw-length-to-target-length ratio) - untestable
    // visually in this environment, and indeed wrong: 2026-08-13, the user manually
    // corrected both Scale and Position by eye in the Inspector after previewing in Play
    // mode. The values below are copied from that corrected scene state (Transform dump:
    // localPosition=(-0.03,-0.18,-0.05), localScale=(0.03,0.03,0.03), localRotation
    // identity) so re-running this tool reproduces the user's actual calibration instead of
    // overwriting it back to the original blind guess. Don't "fix" these back toward a
    // recomputed formula without asking first.
    internal static class Player5WeaponSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string FbxPath = "Assets/_Project/Characters/Placeholder/Weapons/WolfsGravestone/Genshin_WGS.fbx";
        private const string TextureFolder = "Assets/_Project/Characters/Placeholder/Weapons/WolfsGravestone/Textures";
        private const string MaterialsFolder = "Assets/_Project/Characters/Placeholder/Weapons/WolfsGravestone/Materials";
        private const string HandBoneName = "Rhand_Weapon2";

        // Full path (for reference, also recorded in Docs/ASSET_LICENSES.md and
        // Docs/CHANGELOG.md): Player/Visual/player_004_lacrimosa_skin_LOD1_Skeleton/root/
        // Bip001/Bip001-Pelvis/Bip001-Spine/Bip001-Spine1/Bip001-Spine2/Bip001-R-Clavicle/
        // Bip001-R-UpperArm/Bip001-R-Forearm/Bip001-R-Hand/Bip001-Prop1/Rhand_Weapon2/
        // WolfsGravestone
        private static readonly Vector3 GripLocalPosition = new Vector3(-0.03f, -0.18f, -0.05f);
        private static readonly Vector3 GripLocalScale = new Vector3(0.03f, 0.03f, 0.03f);

        [MenuItem("Tools/Live2DAction/Attach Wolf's Gravestone Weapon To Player5")]
        public static void Apply()
        {
            ConfigureTextureImports();
            Material topMat = BuildMaterial("Top");
            Material bottomMat = BuildMaterial("Bottom");

            GameObject fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError("Could not load FBX at " + FbxPath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            Transform handBone = FindDeepChild(player.transform, HandBoneName);
            if (handBone == null)
            {
                Debug.LogError($"Could not find '{HandBoneName}' bone under Player - is Player5's Visual attached?");
                return;
            }

            Transform existingWeapon = handBone.Find("WolfsGravestone");
            if (existingWeapon != null)
            {
                Object.DestroyImmediate(existingWeapon.gameObject);
            }

            GameObject weapon = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, handBone);
            weapon.name = "WolfsGravestone";
            weapon.transform.localRotation = Quaternion.identity;
            weapon.transform.localScale = GripLocalScale;
            weapon.transform.localPosition = GripLocalPosition;

            // Scene-instance material override (Renderer.sharedMaterial set directly on the
            // instantiated copy), NOT a ModelImporter.AddRemap onto the source FBX - unlike
            // Player5's own materials (see Player5VisualSetup's history), this override lives
            // in the scene file itself and isn't at risk of being wiped by the FBX's own
            // reimport regenerating embedded materials, so the simpler approach is safe here.
            foreach (Renderer renderer in weapon.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                    {
                        continue;
                    }

                    mats[i] = mats[i].name.Contains("Top") ? topMat : bottomMat;
                }

                renderer.sharedMaterials = mats;
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"Attached Wolf's Gravestone to Player5's {HandBoneName} bone. scale={GripLocalScale} position={GripLocalPosition}");
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        // Normal maps need TextureImporterType.NormalMap to be read correctly by _BumpMap;
        // Metallic/Roughness are non-color data (sRGB would incorrectly gamma-correct them);
        // BaseColor/Emissive stay on the default sRGB color import.
        private static void ConfigureTextureImports()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null)
                {
                    continue;
                }

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

        // Only wires BaseColor, Normal, and Emissive - the three maps with the biggest visual
        // impact. Metallic/Roughness are approximated as flat values instead of properly
        // channel-packed into URP/Lit's single _MetallicGlossMap (which expects metallic in
        // RGB and smoothness in alpha of ONE texture) - packing these two separate source
        // textures into that format would need real image compositing (same category of work
        // as Attack3SlashFrameAtlasBuilder), judged not worth it for a prototype prop weapon;
        // flat values still read as a reasonably metallic blade under URP/Lit's standard PBR
        // response.
        private static Material BuildMaterial(string partName)
        {
            string matPath = MaterialsFolder + "/WGS_" + partName + ".mat";
            EnsureFolder(MaterialsFolder);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool isNew = material == null;
            if (isNew)
            {
                material = new Material(urpLit);
            }

            Texture2D baseColor = LoadTexture(partName, "BaseColor");
            Texture2D normal = LoadTexture(partName, "Normal");
            Texture2D emissive = LoadTexture(partName, "Emissive");

            if (baseColor != null)
            {
                material.SetTexture("_BaseMap", baseColor);
            }

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            if (emissive != null)
            {
                material.SetTexture("_EmissionMap", emissive);
                material.SetColor("_EmissionColor", Color.white);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            material.SetFloat("_Metallic", 0.8f);
            material.SetFloat("_Smoothness", 0.5f);

            if (isNew)
            {
                AssetDatabase.CreateAsset(material, matPath);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static Texture2D LoadTexture(string partName, string kind)
        {
            string path = $"{TextureFolder}/Genshin_WGS_WGS_{partName}_{kind}.tga.png";
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning("Weapon texture not found: " + path);
            }

            return tex;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
