using System.IO;
using UnityEditor;
using UnityEngine;

namespace Live2DAction.EditorTools
{
    // Removes a flat grey background baked into T_SlashCrescent_XSlash8x8.png as OPAQUE
    // pixels (2026-08-13 real user report - "背景是偏白色的畫布"). Diagnosed by sampling the
    // actually-imported texture's pixels directly: a background spot inside a real content
    // cell reads RGBA(0.235, 0.231, 0.235, 1.0) - not the near-black/fully-transparent
    // background the two earlier sources had. This project's Additive blend fix (see
    // Attack3SlashEffectSetup.CreateOrUpdateMaterial) only ever made black (0,0,0) vanish -
    // adding a substantial grey value across the whole quad's footprint every frame instead
    // washes the scene out, exactly matching the report. (The 2 genuinely-unused trailing
    // grid cells, by contrast, sampled as true RGBA(0,0,0,0) - only the "real content" cells
    // have this opaque grey pedestal, presumably left over from whatever tool exported this
    // sheet compositing over a grey checkerboard-preview matte instead of true transparency.)
    //
    // Fix: subtract the measured background color from every pixel (clamped at 0) so real
    // background regions collapse to true (0,0,0) - actually invisible under the Additive
    // blend - while bright flame/glow content, being far brighter than the small subtraction,
    // is barely affected. Also derives a real alpha channel from the result (rather than
    // leaving alpha at 1 everywhere) so this source would also work correctly under ordinary
    // alpha blending if it's ever needed, not just Additive.
    internal static class Attack3SlashBackgroundCleaner
    {
        private const string SourcePath = "Assets/_Project/VFX/Slash/T_SlashCrescent_XSlash8x8.png";
        public const string CleanedPath = "Assets/_Project/VFX/Slash/T_SlashCrescent_XSlash8x8_Clean.png";

        [MenuItem("Tools/Live2DAction/Clean Attack3 Slash Sheet Background")]
        public static void Clean()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SourcePath);
            if (importer == null)
            {
                Debug.LogError("Could not find texture importer at " + SourcePath);
                return;
            }

            // Self-contained rather than relying on Attack3SlashEffectSetup having already
            // configured the raw source's import settings first (that tool now points its
            // own ConfigureTextureImport at THIS class's cleaned output, not the raw file -
            // see its own comment) - reads at the same 4096 cap it'll ultimately ship at
            // either way, so this doesn't process pixels at a resolution nobody uses.
            bool wasReadable = importer.isReadable;
            bool needsReimport = !wasReadable || importer.maxTextureSize != 4096;
            if (needsReimport)
            {
                importer.isReadable = true;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }

            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            Color32[] pixels = source.GetPixels32();
            int width = source.width;
            int height = source.height;

            Color background = MeasureBackground(source, width, height);
            Debug.Log($"Measured background color: {background}");

            var cleaned = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                float r = Mathf.Max(0f, c.r - background.r);
                float g = Mathf.Max(0f, c.g - background.g);
                float b = Mathf.Max(0f, c.b - background.b);
                // Boosted so faint leftover subtraction noise still fades to ~0 alpha instead
                // of leaving a dim halo - 4x is a mild boost, not a hard cutoff.
                float alpha = Mathf.Clamp01(Mathf.Max(r, Mathf.Max(g, b)) * 4f);
                cleaned[i] = new Color(r, g, b, alpha * (c.a / 255f));
            }

            var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            output.SetPixels32(cleaned);
            output.Apply();

            byte[] png = output.EncodeToPNG();
            Object.DestroyImmediate(output);

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectRoot, CleanedPath);
            File.WriteAllBytes(fullPath, png);

            if (!wasReadable)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            AssetDatabase.ImportAsset(CleanedPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("Wrote background-cleaned sheet to " + CleanedPath);
        }

        // Averages a handful of corner samples from a few different cells (rather than a
        // single point) so one unlucky sample landing on a compression artifact or stray
        // spark pixel can't skew the whole subtraction.
        private static Color MeasureBackground(Texture2D tex, int width, int height)
        {
            int cellW = width / 8;
            int cellH = height / 8;
            Vector2Int[] cells = { new Vector2Int(0, 0), new Vector2Int(2, 1), new Vector2Int(4, 2), new Vector2Int(1, 4) };

            Color sum = Color.black;
            int count = 0;
            foreach (Vector2Int cell in cells)
            {
                int baseX = cell.x * cellW;
                int baseY = cell.y * cellH;
                // Corner of each cell, a few pixels in - background-only, away from any
                // frame's central content.
                sum += tex.GetPixel(baseX + 3, baseY + 3);
                sum += tex.GetPixel(baseX + cellW - 4, baseY + 3);
                count += 2;
            }

            return sum / count;
        }
    }
}
