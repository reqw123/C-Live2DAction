using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Live2DAction.Combat;

namespace Live2DAction.EditorTools
{
    // 2026-08-23, explicit user request ("想要為瞄準時射擊配上音效") - the project has zero audio
    // assets/AudioSource usage anywhere (checked first). User declined the fal.ai AI-generation
    // option specifically to avoid the paid API ("不要付費"), so this is a procedurally
    // synthesized gunshot instead - a sharp noise "crack" (fast exponential decay) layered with a
    // low-frequency "thump" (slower decay), written out as a real 16-bit PCM WAV file and
    // imported as a proper AudioClip asset. Same "not hand-authored, baked once, comment why"
    // precedent as ExecutionRing.png/HudRoundedRect.png - just audio samples instead of pixels.
    internal static class GunshotSfxSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/GreyboxTest.unity";
        private const string ClipPath = "Assets/_Project/Audio/GunshotSfx.wav";

        private const int SampleRate = 44100;
        private const float Duration = 0.22f;

        [MenuItem("Tools/Live2DAction/Add Gunshot SFX To Ranged Weapon")]
        public static void Apply()
        {
            EnsureGunshotClip();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in " + ScenePath);
                return;
            }

            RangedWeapon rangedWeapon = player.GetComponent<RangedWeapon>();
            if (rangedWeapon == null)
            {
                // 2026-08-31: the shooting system was retired (right mouse is the katana guard
                // now, PlayerGuardSetup). The AK47 asset + RangedWeapon.cs are kept on disk; run
                // "Add Ranged Weapon To Player" first if you're bringing it back, then this.
                Debug.LogWarning("GunshotSfxSetup: Player has no RangedWeapon (shooting system retired) - " +
                                 "nothing to wire. Skipped.");
                return;
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
            if (clip == null)
            {
                Debug.LogError("Gunshot clip not found at " + ClipPath + " after generation.");
                return;
            }

            AudioSource audioSource = player.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = player.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D - the player hearing their own gunshot doesn't need 3D attenuation

            var so = new SerializedObject(rangedWeapon);
            so.FindProperty("audioSource").objectReferenceValue = audioSource;
            so.FindProperty("fireSound").objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Wired procedural gunshot SFX to Player's RangedWeapon.");
        }

        private static void EnsureGunshotClip()
        {
            string fullPath = Path.Combine(Application.dataPath, "..", ClipPath);

            int sampleCount = Mathf.RoundToInt(SampleRate * Duration);
            var samples = new float[sampleCount];

            // Fixed seed - reproducible output (re-running this tool always bakes the exact same
            // clip) rather than a different random gunshot every time the setup is re-applied.
            var rng = new System.Random(12345);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Sharp "crack" - the initial percussive transient, decays almost immediately.
                float crackEnvelope = Mathf.Exp(-t / 0.015f);
                float crack = noise * crackEnvelope;

                // Low-frequency "thump" - the gun's body/report, decays slower than the crack so
                // it reads as a short tail rather than an instant click.
                float thumpEnvelope = Mathf.Exp(-t / 0.08f);
                float thump = Mathf.Sin(2f * Mathf.PI * 90f * t) * thumpEnvelope;

                // Low-passed-ish noise body (just noise under the same slower envelope, no real
                // filtering needed at this duration) - adds grit/texture under the thump so it
                // doesn't read as a pure tone.
                float bodyNoise = noise * thumpEnvelope * 0.35f;

                samples[i] = crack * 0.85f + thump * 0.6f + bodyNoise;
            }

            float maxAbs = 0f;
            foreach (float s in samples)
            {
                maxAbs = Mathf.Max(maxAbs, Mathf.Abs(s));
            }
            if (maxAbs > 1f)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] /= maxAbs;
                }
            }

            byte[] wavBytes = EncodeWav(samples, SampleRate);

            string directory = Path.GetDirectoryName(fullPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllBytes(fullPath, wavBytes);

            AssetDatabase.ImportAsset(ClipPath);
        }

        // Standard 16-bit PCM mono WAV (RIFF/WAVE) - hand-rolled rather than pulling in a
        // dependency, same reasoning InvulnerabilityRippleSetup/ExecutionIndicatorSetup gave for
        // baking their own sprite pixels directly rather than reaching for an external tool.
        private static byte[] EncodeWav(float[] samples, int sampleRate)
        {
            const int bitsPerSample = 16;
            const int channels = 1;
            int byteRate = sampleRate * channels * bitsPerSample / 8;
            int blockAlign = channels * bitsPerSample / 8;
            int dataSize = samples.Length * blockAlign;

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)bitsPerSample);

            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            foreach (float sample in samples)
            {
                short pcm = (short)Mathf.Clamp(sample * short.MaxValue, short.MinValue, short.MaxValue);
                writer.Write(pcm);
            }

            return stream.ToArray();
        }
    }
}
