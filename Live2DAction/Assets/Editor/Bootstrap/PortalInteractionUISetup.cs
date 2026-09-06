using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Live2DAction.World;

namespace Live2DAction.EditorTools
{
    // 2026-09-06, user request - builds the shared Boss-map portal interaction prompt UI (a
    // singleton Canvas in the persistent GreyboxTest scene) and configures EVERY SceneGate -
    // enter and exit, all scenes - to use it: interact key F, per-gate prompt line, and a
    // full-height Blocker wall so the far side of a portal can't be crossed. Re-runnable.
    //
    // Creates / touches:
    //   Assets/_Project/VFX/Gate/RT_PortalDialogueFrame.renderTexture   (1280x720)
    //   Assets/_Project/VFX/Gate/Mat_PortalDialogueFrame.mat            (Live2DAction/UI/PortalDialogueFrame)
    //   Assets/_Project/VFX/Gate/PortalDialogueFrameVideo.mp4           (imported as VideoClip)
    //   GreyboxTest > PortalInteractionUI (Canvas + CanvasGroup + PortalInteractionUIController)
    //     > VideoContainer > AnimatedFrameVideo (RawImage + VideoPlayer)
    //     > PromptText (Text + CanvasGroup)   > OptionalKeyHint (disabled)
    //   Every SceneGate in GreyboxTest + Map_School/Map_Nijigen/Map_Xianshi:
    //     interactKey=F, interactKeyLabel="F", showInteractionUI=true, promptMessage=<per gate>,
    //     Blocker -> 16 x 22 x 1.5 wall on the portal plane.
    internal static class PortalInteractionUISetup
    {
        const string ScenePath  = "Assets/_Project/Scenes/GreyboxTest.unity";
        const string GateDir     = "Assets/_Project/VFX/Gate";
        const string VideoPath    = GateDir + "/PortalDialogueFrameVideo.mp4";
        const string RtPath       = GateDir + "/RT_PortalDialogueFrame.renderTexture";
        const string MatPath      = GateDir + "/Mat_PortalDialogueFrame.mat";
        const string ShaderName   = "Live2DAction/UI/PortalDialogueFrame";
        const string RootName     = "PortalInteractionUI";

        [MenuItem("Tools/Live2DAction/Setup Portal Interaction UI (地圖外傳送門互動提示)")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("Exit Play Mode first."); return; }

            // --- 1. video clip -------------------------------------------------------------------
            if (!File.Exists(VideoPath))
            {
                Debug.LogError($"PortalInteractionUISetup: {VideoPath} not found. Copy 對話系統ui框.mp4 there first.");
                return;
            }
            AssetDatabase.ImportAsset(VideoPath, ImportAssetOptions.ForceSynchronousImport);
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
            if (clip == null) { Debug.LogError("PortalInteractionUISetup: failed to import the mp4 as a VideoClip."); return; }

            // --- 2. RenderTexture (1280x720, matches the source) --------------------------------
            var oldRt = AssetDatabase.LoadAssetAtPath<RenderTexture>(RtPath);
            if (oldRt != null) AssetDatabase.DeleteAsset(RtPath);
            var rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32)
            {
                name = "RT_PortalDialogueFrame",
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            rt.Create();
            AssetDatabase.CreateAsset(rt, RtPath);

            // --- 3. Material ------------------------------------------------------------------------
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"PortalInteractionUISetup: shader '{ShaderName}' not found - let it compile first, then re-run.");
                return;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, MatPath); }
            else mat.shader = shader;
            mat.mainTexture = rt;
            mat.SetFloat("_BlackThreshold", 0.055f);
            mat.SetFloat("_Softness", 0.14f);
            mat.SetFloat("_Intensity", 1.05f);
            mat.SetFloat("_GlowBoost", 0.35f);
            mat.SetFloat("_CropInset", 0f);       // the RawImage uvRect crops to the frame box; shader crop off
            mat.SetFloat("_CropSoftness", 0.05f);
            mat.SetFloat("_MasterAlpha", 1f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            // --- 4. scene ------------------------------------------------------------------------
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var oldRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);

            // Canvas ---------------------------------------------------------------------------------
            // No GraphicRaycaster - this UI never takes input (spec 三: 不阻擋滑鼠射線).
            var rootGo = new GameObject(RootName, typeof(RectTransform), typeof(Canvas),
                                        typeof(CanvasScaler), typeof(CanvasGroup));
            SceneManager_MoveToScene(rootGo, scene);
            var canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 700;   // above HUDs (500) / victory banner (850 is higher, fine), below death screen (880) + curtain (32000)
            var scaler = rootGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            var rootCg = rootGo.GetComponent<CanvasGroup>();
            rootCg.alpha = 0f;
            rootCg.interactable = false;
            rootCg.blocksRaycasts = false;

            // VideoContainer -----------------------------------------------------------------------
            // The mp4 is a wide dialogue-frame box sitting in the LOWER portion of an otherwise
            // black 1280x720 frame. We crop to just that box with the RawImage uvRect so the
            // container IS the frame - then the text simply centres in the container (spec 2/四:
            // 文字放在 UI 框裡面). Measured box in the source: x 55..1225, y(top-left) 400..715.
            //   uvRect (origin bottom-left): x=55/1280, w=1170/1280, y=1-(715/720), h=315/720
            var uvRect = new Rect(0.043f, 0.007f, 0.914f, 0.4375f);
            float boxAspect = (uvRect.width * 1280f) / (uvRect.height * 720f);   // ~3.71:1
            float containerW = 1150f;
            var container = NewRect("VideoContainer", rootGo.transform);
            Center(container, new Vector2(containerW, containerW / boxAspect), Vector2.zero);

            // AnimatedFrameVideo (RawImage + VideoPlayer) fills the container, showing only the box
            var videoGo = NewRect("AnimatedFrameVideo", container);
            Stretch(videoGo);
            var raw = videoGo.gameObject.AddComponent<RawImage>();
            raw.texture = rt;
            raw.material = mat;
            raw.raycastTarget = false;
            raw.color = Color.white;
            raw.uvRect = uvRect;

            var vp = videoGo.gameObject.AddComponent<VideoPlayer>();
            vp.source = VideoSource.VideoClip;
            vp.clip = clip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.playOnAwake = false;
            vp.waitForFirstFrame = true;
            vp.skipOnDrop = true;
            vp.isLooping = false;
            vp.audioOutputMode = VideoAudioOutputMode.None;

            // (2026-09-06 user request: 互動文字不要有背景 - the DarkBackdrop panel was removed.
            //  The dialogue box's own dark glass interior is the backing now.)

            // PromptText - centred in the frame box (its own CanvasGroup for the delayed fade-in)
            var textGo = NewRect("PromptText", rootGo.transform);
            Center(textGo, new Vector2(containerW * 0.8f, 110f), Vector2.zero);
            var textCg = textGo.gameObject.AddComponent<CanvasGroup>();
            textCg.alpha = 0f;
            textCg.interactable = false;
            textCg.blocksRaycasts = false;
            var txt = textGo.gameObject.AddComponent<Text>();
            txt.text = "按下 F 進入 Boss 地圖";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 42;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = new Color(0.92f, 0.95f, 1f, 1f);
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;   // spec 四 - never wraps to two lines
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            var tOutline = txt.gameObject.AddComponent<Outline>();
            tOutline.effectColor = new Color(0.12f, 0.3f, 0.55f, 0.9f);
            tOutline.effectDistance = new Vector2(2f, -2f);

            // OptionalKeyHint - a small "F" cap. Present per spec 三; disabled by default.
            var hintGo = NewRect("OptionalKeyHint", rootGo.transform);
            Center(hintGo, new Vector2(64f, 64f), new Vector2(-250f, 0f));
            var hintBg = hintGo.gameObject.AddComponent<Image>();
            hintBg.color = new Color(0.06f, 0.09f, 0.16f, 0.85f);
            hintBg.raycastTarget = false;
            var hintTextGo = NewRect("Label", hintGo);
            Stretch(hintTextGo);
            var hintTxt = hintTextGo.gameObject.AddComponent<Text>();
            hintTxt.text = "F";
            hintTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintTxt.fontSize = 34;
            hintTxt.fontStyle = FontStyle.Bold;
            hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.color = new Color(0.9f, 0.95f, 1f, 1f);
            hintTxt.raycastTarget = false;
            hintGo.gameObject.SetActive(false);

            // Controller + wiring ----------------------------------------------------------------
            var ctrl = rootGo.AddComponent<PortalInteractionUIController>();
            var so = new SerializedObject(ctrl);
            SetRef(so, "canvasGroup", rootCg);
            SetRef(so, "videoContainer", container.gameObject);
            SetRef(so, "animatedFrameVideo", raw);
            SetRef(so, "videoPlayer", vp);
            SetRef(so, "promptTextGroup", textCg);
            SetRef(so, "promptText", txt);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(ctrl);

            // --- configure every SceneGate: interact key F + prompt UI on + per-gate message +
            //     a full-height Blocker wall (all three follow-up user requests). The shared UI is
            //     a singleton now, so gates in the streamed Map_* scenes are handled by opening
            //     each below - no cross-scene reference needed.
            int greyboxGates = ConfigureGatesIn(scene);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            int exitGates = 0;
            foreach (var mapPath in new[]
            {
                "Assets/_Project/Scenes/Map_School.unity",
                "Assets/_Project/Scenes/Map_Nijigen.unity",
                "Assets/_Project/Scenes/Map_Xianshi.unity",
            })
            {
                var ms = EditorSceneManager.GetSceneByPath(mapPath);
                bool msOpened = false;
                if (!ms.isLoaded)
                {
                    if (!File.Exists(mapPath)) continue;
                    ms = EditorSceneManager.OpenScene(mapPath, OpenSceneMode.Additive);
                    msOpened = true;
                }
                int n = ConfigureGatesIn(ms);
                exitGates += n;
                if (n > 0)
                {
                    EditorSceneManager.MarkSceneDirty(ms);
                    EditorSceneManager.SaveScene(ms);
                }
                if (msOpened) EditorSceneManager.CloseScene(ms, true);
            }

            Debug.Log("PortalInteractionUISetup: done.\n" +
                      $"  RenderTexture : {RtPath}\n" +
                      $"  Material      : {MatPath}\n" +
                      $"  Video         : {VideoPath}\n" +
                      $"  UI root       : {RootName} (singleton, in GreyboxTest)\n" +
                      $"  Gates configured: {greyboxGates} in GreyboxTest + {exitGates} in Map_* (F key, prompt UI, blocker wall)");
        }

        // interactKey=F + label + showInteractionUI + per-gate promptMessage + Blocker -> full wall.
        static int ConfigureGatesIn(UnityEngine.SceneManagement.Scene scn)
        {
            int count = 0;
            foreach (var g in Object.FindObjectsByType<SceneGate>(FindObjectsSortMode.None))
            {
                if (g.gameObject.scene != scn) continue;
                count++;

                var gso = new SerializedObject(g);
                var ik = gso.FindProperty("interactKey");
                if (ik != null) ik.boxedValue = UnityEngine.InputSystem.Key.F;
                Set(gso, "interactKeyLabel", "F");
                var su = gso.FindProperty("showInteractionUI");
                if (su != null) su.boolValue = true;
                Set(gso, "promptMessage", PromptFor(g.gameObject.name));
                SetF(gso, "uiShowRange", 1.5f);       // DEPTH - == interactRange so the prompt never shows before F works
                SetF(gso, "interactRange", 1.5f);     // DEPTH
                SetF(gso, "lateralHalfWidth", 6f);    // ACROSS the portal - wide, covers the whole quad / road
                SetF(gso, "hideGraceSeconds", 0.6f);
                gso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(g);

                // 2026-09-06 ("讓傳送門的另一面無法被跨過 直接擋住") - the road ends ~3m past each
                // gate, then void. A full wall centred on the portal plane: 16 across (road ~8),
                // 22 tall, THIN (0.6) so an on-foot player can press right up against it (~0.8 m
                // from the gate) and the tiny interactRange still fires. Invisible; same local
                // axes for the rot-0 and rot-Y90 gates.
                var blocker = g.transform.Find("Blocker");
                var box = blocker != null ? blocker.GetComponent<BoxCollider>() : null;
                if (box != null)
                {
                    blocker.localPosition = new Vector3(0f, 10f, 0f);
                    box.center = Vector3.zero;
                    box.size = new Vector3(16f, 22f, 0.6f);
                    box.isTrigger = false;
                    EditorUtility.SetDirty(box);
                    EditorUtility.SetDirty(blocker);
                }
            }
            return count;
        }

        static string PromptFor(string gateName)
        {
            // "<name>Gate_Enter" / "<name>Gate_Exit" -> a matching line. {KEY} is substituted at runtime.
            bool exit = gateName.EndsWith("_Exit");
            string place =
                gateName.StartsWith("School")  ? (exit ? "離開元培大學" : "進入 Boss 地圖") :
                gateName.StartsWith("Nijigen") ? (exit ? "離開二次元"   : "進入二次元") :
                gateName.StartsWith("Xianshi") ? (exit ? "離開現世"     : "進入現世") :
                (exit ? "離開" : "進入");
            return "按下 {KEY} " + place;
        }

        static void Set(SerializedObject so, string prop, string value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.stringValue = value;
        }

        static void SetF(SerializedObject so, string prop, float value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.floatValue = value;
        }

        // ---- helpers ---------------------------------------------------------------------------

        static void SceneManager_MoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene)
        {
            if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
        }

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Center(RectTransform rt, Vector2 size, Vector2 anchoredPos)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetRef(SerializedObject so, string prop, Object value)
        {
            var p = so.FindProperty(prop);
            if (p != null) p.objectReferenceValue = value;
            else Debug.LogWarning($"PortalInteractionUISetup: '{prop}' not found on {so.targetObject}");
        }
    }
}
