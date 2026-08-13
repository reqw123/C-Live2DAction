using System.IO;
using UnityEditor;
using UnityEngine;

namespace Live2DAction.EditorTools
{
    // Composites the 17 individually-trimmed slash-effect PNG frames the user provided
    // (2026-08-13, C:\Users\homec\Downloads\b8f61ffe-c4f8-4408-ac1d-242dc7d9c619-sprites\
    // sprite-001.png..sprite-017.png) into a single uniform-grid atlas - Shuriken's Texture
    // Sheet Animation module needs equal-size cells, but unlike the earlier single-sheet
    // sources these came as 17 SEPARATE files each auto-trimmed to its own visible content,
    // so every frame is a different pixel size (62x48 growing up to 138x115, back down to
    // 78x60 - see the source files themselves). Genuine alpha this time (all 17 are real
    // RGBA PNGs, unlike the previous JPG sheet), so this preserves it untouched rather than
    // relying on the additive-blend-hides-black trick from that fix.
    //
    // No trim-offset metadata shipped alongside the frames (no JSON/XML, just the loose
    // PNGs), so this assumes each frame's art is meant to grow/shrink from a common CENTER
    // point (the standard convention for an impact/swing VFX - the hit position stays fixed
    // while the effect expands around it) and centers every frame within its grid cell on
    // that basis. If the result looks off-center once previewed, that assumption is the
    // first thing to revisit.
    internal static class Attack3SlashFrameAtlasBuilder
    {
        private const string SourceFolder = @"C:\Users\homec\Downloads\b8f61ffe-c4f8-4408-ac1d-242dc7d9c619-sprites";
        private const string FramePrefix = "sprite-";
        private const int FrameCount = 17;

        public const string AtlasPath = "Assets/_Project/VFX/Slash/T_SlashCrescent_Frames17.png";
        public const int TilesX = 6;
        public const int TilesY = 3;

        private const int CellWidth = 160;
        private const int CellHeight = 140;

        [MenuItem("Tools/Live2DAction/Build Attack3 Slash Frame Atlas")]
        public static void Build()
        {
            int atlasWidth = CellWidth * TilesX;
            int atlasHeight = CellHeight * TilesY;
            int totalCells = TilesX * TilesY;
            var atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);

            var clear = new Color32[atlasWidth * atlasHeight];
            atlas.SetPixels32(clear); // Color32 default is (0,0,0,0) - fully transparent

            Texture2D lastFrame = null;
            for (int cellIndex = 0; cellIndex < totalCells; cellIndex++)
            {
                Texture2D frame;
                // 6x3=18 cells but only 17 real frames - rather than leave the last cell
                // truly blank (which would need the Texture Sheet Animation curve tuned to
                // land EXACTLY short of it, fragile to get right without being able to
                // preview the result here), it just repeats the final frame. Worst case is
                // an extra beat holding on the last frame instead of a jarring pop to
                // nothing, regardless of exactly how Shuriken's grid iteration order lines up.
                if (cellIndex < FrameCount)
                {
                    string path = Path.Combine(SourceFolder, $"{FramePrefix}{(cellIndex + 1):000}.png");
                    if (!File.Exists(path))
                    {
                        Debug.LogError("Missing frame: " + path);
                        continue;
                    }

                    frame = LoadTextureFromDisk(path);
                    lastFrame = frame;
                }
                else
                {
                    frame = lastFrame;
                    if (frame == null)
                    {
                        continue;
                    }
                }

                int col = cellIndex % TilesX;
                int row = cellIndex / TilesX; // 0 = visually TOP row, reading order left-to-right/top-to-bottom

                // Texture2D pixel row 0 is the BOTTOM of the image as encoded/viewed (both
                // Unity's SetPixels array and the PNG format itself agree on this) - flip so
                // logical row 0 (top when you look at the sheet) lands in the atlas's highest
                // Y rows instead of its lowest.
                int cellYMinFromTop = row * CellHeight;
                int cellYMinTexture = atlasHeight - cellYMinFromTop - CellHeight;
                int cellXMin = col * CellWidth;

                int pasteX = cellXMin + (CellWidth - frame.width) / 2;
                int pasteY = cellYMinTexture + (CellHeight - frame.height) / 2;

                atlas.SetPixels32(pasteX, pasteY, frame.width, frame.height, frame.GetPixels32());
            }

            atlas.Apply();

            byte[] png = atlas.EncodeToPNG();
            Object.DestroyImmediate(atlas);

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullAtlasPath = Path.Combine(projectRoot, AtlasPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullAtlasPath));
            File.WriteAllBytes(fullAtlasPath, png);

            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"Built {TilesX}x{TilesY} atlas ({FrameCount} real frames + {totalCells - FrameCount} repeated) at {AtlasPath}");
        }

        private static Texture2D LoadTextureFromDisk(string absolutePath)
        {
            byte[] bytes = File.ReadAllBytes(absolutePath);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes); // auto-resizes to the PNG's actual dimensions
            return tex;
        }
    }
}
