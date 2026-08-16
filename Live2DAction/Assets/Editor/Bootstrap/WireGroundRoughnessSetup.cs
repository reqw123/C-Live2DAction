using System.IO;
using UnityEditor;
using UnityEngine;

namespace Live2DAction.EditorTools
{
    // 2026-08-16, explicit user request ("讓場景變得好看"): Ground_StoneFloor.mat has always
    // had a flat, uniform Smoothness (0.2 everywhere, Metallic 0) because the downloaded
    // stone_floor_rough_1k.jpg roughness map was never wired up (see ASSET_LICENSES.md's own
    // note on this). It can't just be dropped straight into URP Lit's _MetallicGlossMap -
    // that slot expects a packed Mask Map (R=Metallic, G=Occlusion, B=unused, A=Smoothness),
    // not a plain single-channel roughness image, and Smoothness is roughness's inverse
    // (1-roughness) - so this bakes a proper Mask Map from the raw roughness source once, at
    // Editor time, rather than trying to reinterpret the wrong texture format at runtime.
    internal static class WireGroundRoughnessSetup
    {
        private const string RoughnessSourcePath = "Assets/_Project/Environment/Textures/StoneFloor/stone_floor_rough_1k.jpg";
        private const string MaskMapOutputPath = "Assets/_Project/Environment/Textures/StoneFloor/stone_floor_mask_1k.png";
        private const string MaterialPath = "Assets/_Project/Environment/Materials/Ground_StoneFloor.mat";

        [MenuItem("Tools/Live2DAction/Wire Ground Roughness Into Mask Map")]
        public static void Apply()
        {
            Texture2D roughnessSource = LoadReadableLinearTexture(RoughnessSourcePath);
            if (roughnessSource == null)
            {
                return;
            }

            Texture2D maskMap = BakeMaskMap(roughnessSource);
            File.WriteAllBytes(ToAbsolutePath(MaskMapOutputPath), maskMap.EncodeToPNG());
            Object.DestroyImmediate(maskMap);
            AssetDatabase.ImportAsset(MaskMapOutputPath);

            // Mask maps are data, not color - must be linear (sRGBTexture=false) so the GPU
            // doesn't apply an sRGB->linear decode to values that are already linear (metallic/
            // occlusion/smoothness scalars, not perceptual color).
            var maskImporter = (TextureImporter)AssetImporter.GetAtPath(MaskMapOutputPath);
            maskImporter.textureType = TextureImporterType.Default;
            maskImporter.sRGBTexture = false;
            maskImporter.SaveAndReimport();

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Debug.LogError("Could not load material at " + MaterialPath);
                return;
            }

            Texture2D bakedMask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskMapOutputPath);
            material.SetTexture("_MetallicGlossMap", bakedMask);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            // 1 = read Smoothness from the Metallic/Mask map's alpha channel (what we just
            // baked into it) instead of 0 = the base map's alpha channel.
            material.SetFloat("_SmoothnessTextureChannel", 1f);
            // Multiplier on top of the baked alpha - 1 means "use the baked value as-is".
            material.SetFloat("_Smoothness", 1f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            Debug.Log("Baked stone_floor_rough_1k.jpg into a proper URP Mask Map and wired it into Ground_StoneFloor.mat's Metallic/Smoothness.");
        }

        private static Texture2D LoadReadableLinearTexture(string assetPath)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            if (importer == null)
            {
                Debug.LogError("Could not find a TextureImporter at " + assetPath);
                return null;
            }

            bool changed = false;
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                changed = true;
            }
            if (importer.sRGBTexture)
            {
                // Read as raw linear data, not perceptual color - this is a roughness/data map,
                // not something meant to look right on screen by itself.
                importer.sRGBTexture = false;
                changed = true;
            }
            if (changed)
            {
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static Texture2D BakeMaskMap(Texture2D roughnessSource)
        {
            var mask = new Texture2D(roughnessSource.width, roughnessSource.height, TextureFormat.RGBA32, true);
            Color[] roughnessPixels = roughnessSource.GetPixels();
            var maskPixels = new Color[roughnessPixels.Length];

            for (int i = 0; i < roughnessPixels.Length; i++)
            {
                float roughness = roughnessPixels[i].grayscale;
                float smoothness = 1f - roughness;
                // R=Metallic (stone is a dielectric, not a metal), G=Occlusion (no separate AO
                // map available, 1 = no extra darkening), B=unused, A=Smoothness.
                maskPixels[i] = new Color(0f, 1f, 0f, smoothness);
            }

            mask.SetPixels(maskPixels);
            mask.Apply();
            return mask;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        }
    }
}
