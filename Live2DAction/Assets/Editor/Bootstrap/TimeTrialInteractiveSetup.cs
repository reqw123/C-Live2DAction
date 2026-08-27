using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Live2DAction.World;

namespace Live2DAction.EditorTools
{
    // 2026-08-20, explicit user request ("空中跑酷的規則和overlay好像消失了，並且我希望改為互動式，
    // 與機關對話後，會觸發短時間內的跑酷計時") - two things in one pass:
    // (1) TimeTrialHudCanvas (SkyIslandTimeTrialSetup.CreateStatusUi's own output) had genuinely
    // gone missing from the live scene (confirmed live - the GameObject wasn't in the scene at
    // all, not just hidden), most likely collateral damage from an unrelated manual Hierarchy edit
    // in the same session (deleting/recreating the Ground boundary walls). TimeTrialController's
    // own checkpoint/run logic was untouched and still fully intact - only the statusText
    // reference had gone null (Update()'s existing null-guard just silently stopped writing to
    // it, no error). Recreated here exactly like SkyIslandTimeTrialSetup.CreateStatusUi does.
    // (2) Chosen design, from three clarifying questions the user answered explicitly: (a) proximity
    // + press-E interact (not walk-into-auto-start), (b) a TIME-LIMITED challenge with a fail
    // condition (not just an unlock-then-open-ended-stopwatch), (c) mechanism placed on the ground
    // near Updraft_MainArea. See TimeTrialController's own Locked/BeginChallenge/FailRun rewrite
    // and TimeTrialStartMechanism (the new "機關" component) for the actual state machine - this
    // tool only builds and wires the scene objects.
    internal static class TimeTrialInteractiveSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string MechanismMaterialPath = "Assets/_Project/VFX/TimeTrialMechanism.mat";

        // 2026-08-22, explicit user request ("讓[圖片]作為空中飛行小遊戲機關互動時的文字ui") - the
        // "想要起飛唯" fire-text image, already copied into the project at this path. Imported as a
        // Sprite (2D and UI) here rather than checked in pre-configured, matching this file's own
        // "bootstrap tool builds everything from source assets" convention.
        //
        // 2026-08-23, real bug found while re-running this tool ("does not exist" on the old .jpg
        // path) - the source asset was converted to .png at some later point (adding a
        // transparency channel - see TimeTrialPromptFrame.png sitting alongside it, from that same
        // pass), but this constant was never updated to follow it. Fixed to the file that actually
        // exists on disk now.
        private const string PromptImagePath = "Assets/_Project/UI/TimeTrialPromptText.png";

        // 2026-08-23, real playtested bug ("並沒有置中在框架上 並且整體大小也不夠大") - measured the
        // source PNG's own alpha channel directly: at alpha>0.5 the visible flame-text content's
        // bounding box is x=[100,1018] y=[122,504] out of the full 1024x559 canvas - its own center
        // (~559,313) sits about 47px right and 33px down from the RAW IMAGE's geometric center
        // (512,280). Centering the FULL image (its previous treatment) therefore always centered
        // a mostly-empty rectangle, with the actual readable text sitting visibly off to one side.
        // Baked once into a tightly-cropped PNG (see EnsureCroppedPromptSprite) so the sprite's own
        // bounds match its visible content - fixes the offset AND makes the text fill much more of
        // its allotted box (same box size, way less wasted transparent margin = reads bigger).
        private const string CroppedPromptImagePath = "Assets/_Project/UI/TimeTrialPromptTextCropped.png";
        private const float PromptContentAlphaThreshold = 0.5f;
        // Extra margin kept around the measured content bbox so the flame's own soft glow doesn't
        // get hard-clipped right at the crop edge.
        private const int PromptContentCropPadding = 24;

        // 2026-08-23, real playtested bug ("背景框也不見了...我似乎有提供過一張ui文字樣式背景框的
        // 綜合圖片") - the ornate border already chosen and sitting in the project, never actually
        // wired into CreatePrompt below (see that method's own comment for the full story).
        private const string PromptFramePath = "Assets/_Project/UI/TimeTrialPromptFrame.png";

        // On the direct Player-spawn (-2.5, *, 0) -> Updraft_MainArea (-2.5, 0, -6) line, 2.5 units
        // short of the updraft's own CapsuleCollider (center (-2.5,5,-6) local, radius 2) - close
        // enough to read as "the thing guarding the lift", far enough that its own detection
        // trigger doesn't force an overlap with the updraft's lift trigger the instant it's built.
        //
        // 2026-08-20, real bug fix ("機關沒有著地") - Y was 0.4f here, which floated the pedestal
        // 0.4 units above Ground's actual surface (Y=0). BuildPedestalMesh already bakes the mesh
        // with its own base at LOCAL Y=0 (see that method's own comment - "base now at local Y=0"),
        // specifically so this transform's position.y could just BE ground level directly. Adding
        // another 0.4f here double-counted that offset - confirmed live (raycast/mesh-bounds check
        // showed the visible mesh's world base sitting at Y=0.4, not Y=0).
        private static readonly Vector3 MechanismPosition = new Vector3(-2.5f, 0f, -3.5f);
        private const float InteractionRadius = 2f;

        [MenuItem("Tools/Live2DAction/Make Sky Island Time Trial Interactive")]
        public static void Apply()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject root = GameObject.Find("SkyIslandTimeTrial");
            if (root == null)
            {
                Debug.LogError("SkyIslandTimeTrial not found - run 'Add Sky Island Time Trial Course' first.");
                return;
            }

            Transform controllerTransform = root.transform.Find("TimeTrialController");
            TimeTrialController controller = controllerTransform != null
                ? controllerTransform.GetComponent<TimeTrialController>()
                : null;
            if (controller == null)
            {
                Debug.LogError("TimeTrialController not found under SkyIslandTimeTrial.");
                return;
            }

            Text statusText = RestoreHudIfMissing(root.transform, controller);
            CreateMechanism(controller);

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Sky Island time trial is now interactive: talk to TimeTrialStartMechanism (near Updraft_MainArea) and press E to start the timed challenge. HUD status text: " + (statusText != null ? "OK" : "MISSING"));
        }

        // Identical construction to SkyIslandTimeTrialSetup.CreateStatusUi - kept as a separate
        // copy rather than calling into that internal method across files, matching this project's
        // existing convention of small self-contained bootstrap tools rather than a shared UI
        // helper library.
        private static Text RestoreHudIfMissing(Transform root, TimeTrialController controller)
        {
            Transform existingHud = root.Find("TimeTrialHudCanvas");
            Text statusText;
            if (existingHud != null)
            {
                statusText = existingHud.GetComponentInChildren<Text>(true);
            }
            else
            {
                var canvasGo = new GameObject("TimeTrialHudCanvas");
                canvasGo.transform.SetParent(root, false);

                Canvas canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasGo.AddComponent<GraphicRaycaster>();

                var textGo = new GameObject("StatusText");
                textGo.transform.SetParent(canvasGo.transform, false);
                RectTransform rect = textGo.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(24f, -24f);
                rect.sizeDelta = new Vector2(480f, 140f);

                Text text = textGo.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 26;
                text.color = Color.white;
                text.alignment = TextAnchor.UpperLeft;
                text.text = "空島競速";

                statusText = text;
            }

            var so = new SerializedObject(controller);
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();

            return statusText;
        }

        private static void CreateMechanism(TimeTrialController controller)
        {
            GameObject mechanism = GameObject.Find("TimeTrialStartMechanism");
            if (mechanism == null)
            {
                mechanism = new GameObject("TimeTrialStartMechanism");
            }
            mechanism.transform.position = MechanismPosition;
            mechanism.transform.localScale = Vector3.one;

            MeshFilter filter = mechanism.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = mechanism.AddComponent<MeshFilter>();
            }
            filter.sharedMesh = BuildPedestalMesh();

            MeshRenderer renderer = mechanism.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = mechanism.AddComponent<MeshRenderer>();
            }
            renderer.sharedMaterial = EnsureMechanismMaterial();

            // Trigger-only (like Portal/CheckpointGate) - a small waist-high pedestal player
            // should be able to walk right up next to, not something that needs to physically
            // block movement.
            SphereCollider trigger = mechanism.GetComponent<SphereCollider>();
            if (trigger == null)
            {
                trigger = mechanism.AddComponent<SphereCollider>();
            }
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.4f, 0f); // waist height, not the pedestal's own ground-level pivot
            trigger.radius = InteractionRadius;

            // Rebuilt from scratch every run rather than patched in place - it's a small, fully
            // self-contained decorative child with no external references except the ones this
            // method rewires immediately below, so destroy+recreate is simpler than keeping every
            // property of an existing one in sync by hand (see this file's 2026-08-20 two-line
            // prompt update, which would otherwise have silently no-op'd against an already-built
            // single-line PromptCanvas from an earlier run).
            Transform existingPrompt = mechanism.transform.Find("PromptCanvas");
            if (existingPrompt != null)
            {
                Object.DestroyImmediate(existingPrompt.gameObject);
            }
            PromptWidgets prompt = CreatePrompt(mechanism.transform);

            TimeTrialStartMechanism component = mechanism.GetComponent<TimeTrialStartMechanism>();
            if (component == null)
            {
                component = mechanism.AddComponent<TimeTrialStartMechanism>();
            }
            var so = new SerializedObject(component);
            so.FindProperty("controller").objectReferenceValue = controller;
            so.FindProperty("promptCanvasRoot").objectReferenceValue = prompt.CanvasRoot;
            so.FindProperty("promptText").objectReferenceValue = prompt.CancelText;
            so.FindProperty("promptVisualRoot").objectReferenceValue = prompt.VisualRoot;
            so.FindProperty("actionHintText").objectReferenceValue = prompt.ActionHintText;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Baked squat cylinder (not CreatePrimitive - keeping the GameObject's own transform at
        // scale (1,1,1) so the SphereCollider's radius/center above stay in true world units,
        // same reasoning as BoundaryWallVisibilityToggle.BuildBoxMesh).
        private static Mesh BuildPedestalMesh()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Mesh mesh = Object.Instantiate(temp.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(temp);

            // Default cylinder mesh is 2 units tall (Y -1..1), radius 0.5 - scale down to a squat
            // 0.8-tall, 0.5-radius pedestal and shift up so its base sits at local Y=0 (this
            // GameObject's own transform.position is ground level).
            var size = new Vector3(0.5f, 0.4f, 0.5f);
            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                v.x *= size.x;
                v.y = (v.y + 1f) * size.y; // -1..1 -> 0..2*size.y, base now at local Y=0
                v.z *= size.z;
                vertices[i] = v;
            }
            mesh.vertices = vertices;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material EnsureMechanismMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(MechanismMaterialPath);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            // Teal/cyan - distinct from the gold checkpoint rings and the orange boundary-wall
            // debug material, reads as its own "interact with me" object category.
            material.SetColor("_BaseColor", new Color(0.1f, 0.85f, 0.9f));
            material.SetColor("_EmissionColor", new Color(0.05f, 0.4f, 0.45f));
            material.EnableKeyword("_EMISSION");

            if (!AssetDatabase.IsValidFolder("Assets/_Project/VFX"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "VFX");
            }
            AssetDatabase.CreateAsset(material, MechanismMaterialPath);
            return material;
        }

        private readonly struct PromptWidgets
        {
            public readonly Transform CanvasRoot;
            public readonly Text CancelText;
            public readonly RectTransform VisualRoot;
            public readonly Text ActionHintText;

            public PromptWidgets(Transform canvasRoot, Text cancelText, RectTransform visualRoot, Text actionHintText)
            {
                CanvasRoot = canvasRoot;
                CancelText = cancelText;
                VisualRoot = visualRoot;
                ActionHintText = actionHintText;
            }
        }

        // World-space prompt floating above the pedestal - billboarded to the camera by
        // TimeTrialStartMechanism.LateUpdate (via CanvasRoot, always active), shown/hidden by its
        // Update based on proximity + TimeTrialController.CanBeginChallenge/IsRunning.
        //
        // 2026-08-22, explicit user request ("讓[圖片]作為空中飛行小遊戲機關互動時的文字ui...按e...
        // 快速將文字收起，像是關閉電視螢幕那樣") - CanvasRoot itself now stays permanently active (see
        // TimeTrialStartMechanism.promptCanvasRoot's own comment for why); the two prompt STATES
        // toggle independently as children: CancelText for the mid-run "按 E 結束挑戰" line (same
        // plain-text look as before), and VisualRoot (image + its own action-hint line) for the
        // BEGIN-state "want to fly?" prompt, TV-off-collapsed by TimeTrialStartMechanism on E press.
        private static PromptWidgets CreatePrompt(Transform parent)
        {
            var canvasGo = new GameObject("PromptCanvas");
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasGo.GetComponent<RectTransform>();
            // 2026-08-23, real playtested bug ("開啟與關閉的ui樣式大小差很多 而且"想要起飛嗎"並沒有
            // 與框架對齊") - the old (3.4, 1.8) box (aspect 1.89) was nowhere close to
            // TimeTrialPromptFrame.png's own real proportions (563x140, aspect 4.02), so
            // preserveAspect on the frame left it letterboxed down to under half the box's height -
            // a small banner floating in a mostly-empty tall box, which read as "much smaller than
            // CancelText" (CancelText fills this exact same box edge-to-edge as plain text, no
            // aspect constraint to fight). Resized so the frame+text region below is close to the
            // frame's own aspect instead, with a dedicated shorter strip under it for the hint line
            // - see FrameAndTextRoot/hintFraction below for the actual split.
            canvasRect.sizeDelta = new Vector2(3.6f, 1.2f);
            // 2026-08-23, explicit user request ("格式對了 現再依照比例放大的更 "還是做不到嗎" 等同
            // 大小") - a uniform scale on the whole canvas transform (rather than touching
            // sizeDelta/any individual child) scales the frame, text, and hint line all together
            // with their proportions/relative alignment completely unchanged. First pass (0.5->1.0)
            // measured by screenshot at the mechanism's own interaction range (~1.8 units away,
            // matching InteractionRadius) as only ~22% of screen width - ChallengeStartTaunt's
            // screen-space banner is 900/1920 = 47% of screen width regardless of distance. Raised
            // to 2.0 (measured ~52% - close enough to read as comparable), then explicitly dialed
            // back down to 1.5 by the user's own follow-up request.
            canvasGo.transform.localScale = Vector3.one * 1.5f;

            var cancelTextGo = new GameObject("CancelText");
            cancelTextGo.transform.SetParent(canvasGo.transform, false);
            RectTransform cancelRect = cancelTextGo.AddComponent<RectTransform>();
            cancelRect.anchorMin = Vector2.zero;
            cancelRect.anchorMax = Vector2.one;
            cancelRect.sizeDelta = Vector2.zero;

            Text cancelText = cancelTextGo.AddComponent<Text>();
            cancelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cancelText.fontSize = 22;
            cancelText.alignment = TextAnchor.MiddleCenter;
            cancelText.color = new Color(0.1f, 0.95f, 1f);
            cancelText.text = "空島競速\n按 E 結束挑戰"; // overwritten every frame by TimeTrialStartMechanism.Update once active - this is just the initial/inspector-visible value
            cancelText.gameObject.SetActive(false); // TimeTrialStartMechanism.Update turns this on only during a live, cancellable run

            var visualRootGo = new GameObject("PromptVisualRoot");
            visualRootGo.transform.SetParent(canvasGo.transform, false);
            RectTransform visualRoot = visualRootGo.AddComponent<RectTransform>();
            visualRoot.anchorMin = Vector2.zero;
            visualRoot.anchorMax = Vector2.one;
            visualRoot.sizeDelta = Vector2.zero;

            // 2026-08-23, real playtested bug ("這個ui樣式跟以前不一樣 背景框也不見了...我似乎有提供過
            // 一張ui文字樣式背景框的綜合圖片") - TimeTrialPromptFrame.png (the ornate border chosen
            // from that reference sheet earlier this project) was still sitting in the project
            // completely unused - CreatePrompt here never actually wired it in, so every rebuild of
            // this mechanism (there have been several this session) silently dropped it even though
            // the asset itself survived.
            //
            // 2026-08-23 follow-up, real playtested bug ("想要起飛嗎並沒有與框架對齊") - the frame
            // spanned the FULL visualRoot (0-1) while PromptImage only spanned a 0.3-1.0 slice of
            // it - two DIFFERENT boxes, each independently preserveAspect-fit from a DIFFERENT
            // source aspect (frame 4.02, text image 1.832), so nothing about their fitted positions
            // had any reason to line up. Restructured: FrameAndTextRoot is one shared sub-box (its
            // own height chosen to closely match the frame's own aspect, see canvasRect.sizeDelta's
            // own comment) - the frame fills it edge-to-edge, and the text image is INSET within
            // that exact same box (not a separately-anchored slice), so both are centered on the
            // same region and the text reads as sitting inside the frame's border, not floating
            // off to one side of it.
            const float hintFraction = 0.25f;

            var frameAndTextRootGo = new GameObject("FrameAndTextRoot");
            frameAndTextRootGo.transform.SetParent(visualRootGo.transform, false);
            RectTransform frameAndTextRoot = frameAndTextRootGo.AddComponent<RectTransform>();
            frameAndTextRoot.anchorMin = new Vector2(0f, hintFraction);
            frameAndTextRoot.anchorMax = Vector2.one;
            frameAndTextRoot.sizeDelta = Vector2.zero;

            Sprite promptFrameSprite = EnsurePromptFrameSprite();
            var frameGo = new GameObject("PromptFrame");
            frameGo.transform.SetParent(frameAndTextRootGo.transform, false);
            RectTransform frameRect = frameGo.AddComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.sizeDelta = Vector2.zero;
            Image frameImage = frameGo.AddComponent<Image>();
            frameImage.sprite = promptFrameSprite;
            frameImage.preserveAspect = true;
            frameImage.raycastTarget = false;

            Sprite promptSprite = EnsureCroppedPromptSprite();

            var imageGo = new GameObject("PromptImage");
            imageGo.transform.SetParent(frameAndTextRootGo.transform, false);
            RectTransform imageRect = imageGo.AddComponent<RectTransform>();
            // Inset from the frame's own outer edge (where the border art itself is drawn) so the
            // text sits visibly INSIDE the frame rather than overlapping/crowding its border lines.
            imageRect.anchorMin = new Vector2(0.1f, 0.12f);
            imageRect.anchorMax = new Vector2(0.9f, 0.88f);
            imageRect.sizeDelta = Vector2.zero;

            Image image = imageGo.AddComponent<Image>();
            image.sprite = promptSprite;
            image.preserveAspect = true;

            var hintTextGo = new GameObject("ActionHintText");
            hintTextGo.transform.SetParent(visualRootGo.transform, false);
            RectTransform hintRect = hintTextGo.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(1f, hintFraction);
            hintRect.sizeDelta = Vector2.zero;

            Text hintText = hintTextGo.AddComponent<Text>();
            hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintText.fontSize = 20;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = new Color(0.1f, 0.95f, 1f);
            hintText.text = "按 E 開始挑戰"; // overwritten every frame by TimeTrialStartMechanism.Update once active - this is just the initial/inspector-visible value

            visualRootGo.SetActive(false); // TimeTrialStartMechanism.Update turns this on when in range and CanBeginChallenge

            return new PromptWidgets(canvasGo.transform, cancelText, visualRoot, hintText);
        }

        // Imports the "想要起飛唯" fire-text artwork (already copied to PromptImagePath - see that
        // const's own comment) as a Sprite (2D and UI) the first time this tool runs, so the
        // bootstrap tool is the one place that turns a raw source image into a usable UI asset,
        // same as EnsureMechanismMaterial does for the pedestal's material.
        private static Sprite EnsurePromptSprite()
        {
            AssetDatabase.ImportAsset(PromptImagePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(PromptImagePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("TimeTrialPromptText image not found at " + PromptImagePath);
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = false;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(PromptImagePath);
        }

        // See CroppedPromptImagePath's own comment for the bug this fixes. Reads the SOURCE PNG's
        // raw bytes directly off disk (LoadImage on an in-memory Texture2D is always readable
        // regardless of the source asset's own import settings - same technique
        // ChallengeStartTauntSetup.BakeTransparentPng uses), finds the tight bounding box of its
        // already-real alpha channel, crops with a small padding margin, and writes a new PNG at
        // CroppedPromptImagePath. Always re-bakes rather than skip-if-exists, so editing
        // PromptContentAlphaThreshold/CropPadding and re-running this tool actually takes effect.
        private static Sprite EnsureCroppedPromptSprite()
        {
            string sourceFullPath = Path.Combine(Application.dataPath, "..", PromptImagePath);
            if (!File.Exists(sourceFullPath))
            {
                Debug.LogError("TimeTrialPromptText source image not found at " + PromptImagePath);
                return null;
            }

            byte[] sourceBytes = File.ReadAllBytes(sourceFullPath);
            var sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!sourceTexture.LoadImage(sourceBytes))
            {
                Debug.LogError("Failed to decode TimeTrialPromptText source image at " + PromptImagePath);
                Object.DestroyImmediate(sourceTexture);
                return null;
            }

            int w = sourceTexture.width;
            int h = sourceTexture.height;
            Color[] pixels = sourceTexture.GetPixels();

            int minX = w, maxX = 0, minY = h, maxY = 0;
            bool any = false;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a > PromptContentAlphaThreshold)
                    {
                        any = true;
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (!any)
            {
                Debug.LogError("TimeTrialPromptText has no pixels above the alpha threshold - cannot crop, falling back to the uncropped source.");
                Object.DestroyImmediate(sourceTexture);
                return EnsurePromptSprite();
            }

            minX = Mathf.Max(0, minX - PromptContentCropPadding);
            minY = Mathf.Max(0, minY - PromptContentCropPadding);
            maxX = Mathf.Min(w - 1, maxX + PromptContentCropPadding);
            maxY = Mathf.Min(h - 1, maxY + PromptContentCropPadding);
            int cropWidth = maxX - minX + 1;
            int cropHeight = maxY - minY + 1;

            var croppedTexture = new Texture2D(cropWidth, cropHeight, TextureFormat.RGBA32, false);
            croppedTexture.SetPixels(sourceTexture.GetPixels(minX, minY, cropWidth, cropHeight));
            croppedTexture.Apply();
            Object.DestroyImmediate(sourceTexture);

            byte[] pngBytes = croppedTexture.EncodeToPNG();
            Object.DestroyImmediate(croppedTexture);

            string croppedFullPath = Path.Combine(Application.dataPath, "..", CroppedPromptImagePath);
            File.WriteAllBytes(croppedFullPath, pngBytes);
            AssetDatabase.ImportAsset(CroppedPromptImagePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(CroppedPromptImagePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("Cropped prompt image not found at " + CroppedPromptImagePath + " after import.");
                return null;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(CroppedPromptImagePath);
        }

        // Same "bootstrap tool turns a raw source image into a usable UI asset" convention as
        // EnsurePromptSprite above - alphaIsTransparency=true this time since the frame art is
        // already a real transparent PNG (confirmed on disk: alphaSource=FromInput), unlike the
        // text image's own JPG-derived treatment.
        private static Sprite EnsurePromptFrameSprite()
        {
            AssetDatabase.ImportAsset(PromptFramePath, ImportAssetOptions.ForceSynchronousImport);

            var importer = AssetImporter.GetAtPath(PromptFramePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("TimeTrialPromptFrame image not found at " + PromptFramePath);
                return null;
            }

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(PromptFramePath);
        }
    }
}
