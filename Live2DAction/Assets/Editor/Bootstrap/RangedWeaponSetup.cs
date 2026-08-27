using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.Combat;
using Live2DAction.Input;

namespace Live2DAction.EditorTools
{
    // 2026-08-23, explicit user request ("加入瞄準-射擊機制 模擬槍戰...做簡單判定 簡單特效") - wires
    // RangedWeapon onto Player: a simple screen-center crosshair (shown only while aiming) plus a
    // LineRenderer tracer, reusing HitEffectSetup's own shared hit-spark prefab so a gunshot's
    // impact reads consistently with melee hits rather than needing a second VFX asset.
    internal static class RangedWeaponSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string TracerMaterialPath = "Assets/_Project/VFX/RangedTracer.mat";

        [MenuItem("Tools/Live2DAction/Add Ranged Weapon To Player")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            PlayerInputProvider input = player.GetComponent<PlayerInputProvider>();
            if (input == null)
            {
                Debug.LogError("Player has no PlayerInputProvider - can't wire RangedWeapon's input source.");
                return;
            }

            GameObject crosshair = EnsureCrosshair();
            GameObject hitEffectPrefab = HitEffectSetup.CreateOrLoadHitEffectPrefab();

            // 2026-08-23, explicit user request ("獨立新的元件管理攻擊距離") - ensured explicitly
            // rather than relying on RangedWeapon's [RequireComponent(typeof(RangedAttackDistance))]
            // alone: that attribute only auto-adds the sibling when RangedWeapon itself is freshly
            // added via AddComponent, not retroactively onto an instance that already existed
            // before this component was introduced (exactly Player's situation here, re-running
            // this same setup after the fact).
            if (player.GetComponent<RangedAttackDistance>() == null)
            {
                player.AddComponent<RangedAttackDistance>();
            }

            RangedWeapon weapon = player.GetComponent<RangedWeapon>();
            bool isNew = weapon == null;
            if (isNew)
            {
                weapon = player.AddComponent<RangedWeapon>();
            }

            LineRenderer tracer = player.GetComponent<LineRenderer>();
            ConfigureTracer(tracer);

            var so = new SerializedObject(weapon);
            so.FindProperty("inputSource").objectReferenceValue = input;
            so.FindProperty("hitEffectPrefab").objectReferenceValue = hitEffectPrefab;
            so.FindProperty("crosshair").objectReferenceValue = crosshair;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired RangedWeapon (aim + hitscan fire) onto Player. Hold right mouse to aim, left-click to fire.");
        }

        // Idempotent - safe to re-run without piling up duplicate canvases, same convention as
        // BoundaryWallBlockEffectSetup.EnsureHud.
        private static GameObject EnsureCrosshair()
        {
            GameObject existingCanvas = GameObject.Find("RangedWeaponHud");
            if (existingCanvas != null)
            {
                Transform existingDot = existingCanvas.transform.Find("Crosshair");
                if (existingDot != null)
                {
                    return existingDot.gameObject;
                }
            }

            GameObject canvasGo = existingCanvas != null ? existingCanvas : new GameObject("RangedWeaponHud");
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            var dotGo = new GameObject("Crosshair");
            dotGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rect = dotGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(6f, 6f);
            rect.anchoredPosition = Vector2.zero;

            Image image = dotGo.AddComponent<Image>();
            image.sprite = null; // default white square, plenty for a simple dot
            image.color = new Color(1f, 1f, 1f, 0.9f);
            image.raycastTarget = false;

            dotGo.SetActive(false); // RangedWeapon toggles this on only while actually aiming

            return dotGo;
        }

        private static void ConfigureTracer(LineRenderer tracer)
        {
            tracer.enabled = false;
            tracer.useWorldSpace = true;
            tracer.positionCount = 2;
            tracer.startWidth = 0.03f;
            tracer.endWidth = 0.03f;
            tracer.numCapVertices = 2;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;

            Material material = EnsureTracerMaterial();
            tracer.sharedMaterial = material;
            tracer.startColor = new Color(1f, 0.9f, 0.3f, 1f);
            tracer.endColor = new Color(1f, 0.9f, 0.3f, 0.2f);
        }

        private static Material EnsureTracerMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(TracerMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("Could not find Universal Render Pipeline/Particles/Unlit shader.");
                return null;
            }

            var material = new Material(shader);
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 1f); // Additive - reads as a bright laser-like tracer
            material.SetColor("_BaseColor", Color.white);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (!AssetDatabase.IsValidFolder("Assets/_Project/VFX"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "VFX");
            }
            AssetDatabase.CreateAsset(material, TracerMaterialPath);
            return material;
        }
    }
}
