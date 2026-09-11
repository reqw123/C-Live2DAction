using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Live2DAction.AI.Boss.Yuanpei;

namespace Live2DAction.EditorTools
{
    // 續190, user request ("讓元培boss新增攻擊技能 重新生成魂類黑色刀刃劍氣的版本.mp4 仿3d") -
    // adds the 魂刃劍氣 / SoulBladeQi ranged attack to yuanpei_LogoSky (YuanpeiBoss in Map_School):
    //   * configures the video-baked flipbook atlas import (SoulBladeQi_Atlas.png, 12x6 / 72 real
    //     frames, one cycle spark->blade->burst - ffmpeg recipe below)
    //   * builds Live2DAction/VFX/SlashFlipbook material for it (same premult-alpha setup as the
    //     Crimson Void Spear / cat DarkSwordQi flipbooks)
    //   * creates/updates YuanpeiAttack_SoulBladeQi.asset (ALL numbers live there, CLAUDE.md rule 7)
    //   * adds the def to YuanpeiBoss.attackPool and wires soulBladeFlipbookMaterial onto YuanpeiAttacks
    // Re-runnable. The runtime VFX card is built at runtime by YuanpeiAttacks.BuildSoulBladeFlipbook
    // (billboard, plays the whole sheet once over the flight), so there is no prefab to bake here.
    //
    // ffmpeg bake (from the original Downloads copy - CJK path needs an ASCII copy first):
    //   ffmpeg -y -i swordqi.mp4 -vf "select='between(n,48,119)',setpts=N/(24*TB),\
    //     drawbox=x=1030:y=535:w=210:h=185:color=black:t=fill,scale=160:90,tile=12x6,format=rgba,\
    //     geq=r='r(X,Y)':g='g(X,Y)':b='b(X,Y)':\
    //     a='255*pow(clip((max(max(r(X,Y),g(X,Y)),b(X,Y))-40)/150,0,1),1.1)'" \
    //     -update 1 -frames:v 1 SoulBladeQi_Atlas.png
    // Source clip is opaque H.264 on a TRUE black background (corners sampled RGB 0-4), so the luma
    // key floor is low (40) with range 150; a drawbox paints out the generator's 4-point sparkle
    // watermark (bottom-right ~x1030-1240 / y535-720 of 1280x720) with black before the key.
    internal static class YuanpeiSoulBladeSetup
    {
        const string ScenePath   = "Assets/_Project/Scenes/Map_School.unity";
        const string VfxDir       = "Assets/_Project/VFX/Boss/SoulBladeQi";
        const string AtlasPath    = VfxDir + "/SoulBladeQi_Atlas.png";
        const string MaterialPath = VfxDir + "/SoulBladeQiFlipbook.mat";
        const string FlipbookShader = "Live2DAction/VFX/SlashFlipbook";
        const string AssetDir  = "Assets/_Project/Settings/Combat/Yuanpei";
        const string AssetPath = AssetDir + "/YuanpeiAttack_SoulBladeQi.asset";

        [MenuItem("Tools/Live2DAction/Setup Yuanpei Soul Blade Qi (魂刃劍氣)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }

            // --- 1. atlas import ---
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath) == null)
            {
                Debug.LogError("YuanpeiSoulBladeSetup: atlas missing at " + AtlasPath + " - run the ffmpeg bake (recipe in this file's header).");
                return;
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }

            // --- 2. material ---
            Shader shader = Shader.Find(FlipbookShader);
            if (shader == null) { Debug.LogError("Shader not found: " + FlipbookShader + " (still compiling?)."); return; }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool matNew = mat == null;
            if (matNew) mat = new Material(shader);
            else if (mat.shader != shader) mat.shader = shader;
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));
            mat.SetColor("_Color", Color.white);              // atlas already carries the void-purple
            mat.SetFloat("_SrcBlend", 1f);                    // One (shader premultiplies rgb by a)
            mat.SetFloat("_DstBlend", 10f);                   // OneMinusSrcAlpha
            mat.SetFloat("_Brightness", 2.1f);               // white-hot blade core should blow past 1.0 for Bloom
            mat.SetFloat("_Opacity", 1f);
            mat.SetFloat("_ZTest", 4f);                       // normal depth test - flies through the world
            if (matNew) AssetDatabase.CreateAsset(mat, MaterialPath);
            else EditorUtility.SetDirty(mat);

            // --- 3. the ScriptableObject ---
            var def = AssetDatabase.LoadAssetAtPath<YuanpeiAttackDef>(AssetPath);
            bool defNew = def == null;
            if (defNew)
            {
                if (!Directory.Exists(AssetDir)) Directory.CreateDirectory(AssetDir);
                def = ScriptableObject.CreateInstance<YuanpeiAttackDef>();
                AssetDatabase.CreateAsset(def, AssetPath);
            }
            def.attackId = YuanpeiAttackId.SoulBladeQi;
            def.displayName = "魂刃劍氣";
            def.requiredPhase = 1;               // 續191 - phase 1 so it actually shows up in a normal fight (was 2)
            def.energyCost = 22f;
            def.cooldownSeconds = 8f;
            def.minRange = 5f;                   // stand-still telescoping lance - usable close or far
            def.maxRange = 28f;                  // long range
            def.isMajorHazard = true;
            def.telegraphSeconds = 0.4f;         // 續191c - the real tell is SoulBladeArmAndSnap (body charge + random hold); keep Run()'s disc pulse short
            def.windupSeconds = 0.1f;
            def.activeSeconds = 0.8f;
            def.recoverySeconds = 0.7f;
            def.healthDamage = 46f;
            def.playerPostureDamage = 0f;
            def.maxHitsPerTarget = 1;
            def.baseWeight = 1f;
            def.situationalWeightBonus = 2.5f;
            def.number1 = 0.5f;                  // extend duration (s) - blade telescopes 0 -> reach
            def.number2 = 2.6f;                  // blade width (m)
            def.number3 = 0.28f;                 // hold-at-full-extension (s) - burst plays over this
            def.number4 = 1.0f;                  // 續191c - MIN armed-hold (s); random 1.0 .. 3.0 before it locks + fires
            def.number5 = 1.0f;                  // line-hit radius (m)
            def.count = 1;                       // one-shot jab
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();

            // --- 4. wire onto the boss in Map_School ---
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!scene.isLoaded) { scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive); opened = true; }

            var boss = Object.FindObjectsByType<YuanpeiBoss>(FindObjectsSortMode.None)
                             .FirstOrDefault(b => b.gameObject.scene == scene);
            if (boss == null)
            {
                Debug.LogError("YuanpeiSoulBladeSetup: no YuanpeiBoss in " + ScenePath + " (assets still created/updated).");
                if (opened) EditorSceneManager.CloseScene(scene, true);
                return;
            }

            var bso = new SerializedObject(boss);
            var pool = bso.FindProperty("attackPool");
            bool already = false;
            for (int i = 0; i < pool.arraySize; i++)
                if (pool.GetArrayElementAtIndex(i).objectReferenceValue == def) { already = true; break; }
            if (!already)
            {
                pool.InsertArrayElementAtIndex(pool.arraySize);
                pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = def;
            }
            bso.ApplyModifiedProperties();
            EditorUtility.SetDirty(boss);

            var attacks = boss.GetComponent<YuanpeiAttacks>();
            if (attacks != null)
            {
                var aso = new SerializedObject(attacks);
                var mp = aso.FindProperty("soulBladeFlipbookMaterial");
                if (mp != null) mp.objectReferenceValue = mat;
                var cols = aso.FindProperty("soulBladeFlipbookCols"); if (cols != null) cols.intValue = 12;
                var rows = aso.FindProperty("soulBladeFlipbookRows"); if (rows != null) rows.intValue = 6;
                var frames = aso.FindProperty("soulBladeFlipbookFrames"); if (frames != null) frames.intValue = 72;
                aso.ApplyModifiedProperties();
                EditorUtility.SetDirty(attacks);
            }
            else Debug.LogWarning("YuanpeiSoulBladeSetup: YuanpeiAttacks not found on the boss - material not wired.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.SaveAssets();

            Debug.Log($"YuanpeiSoulBladeSetup: {(defNew ? "created" : "updated")} {AssetPath}, " +
                      $"{(matNew ? "created" : "updated")} {MaterialPath}, " +
                      $"pool {(already ? "already had it" : "+= SoulBladeQi")}. " +
                      "魂刃劍氣 (續191 伸縮長矛): phase 1+, 5-28m, stand-still telescoping lance, 46 dmg, cd 8s, one-shot line hit.");
        }
    }
}
