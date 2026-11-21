using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HumanBartender.CutsceneStudio
{
    // 컷씬의 그림과 글자를 같은 신호로 처리한다. 다른 카메라의 화면은 샘플링하지 않는다.
    internal sealed class StudioSignalCapture : IDisposable
    {
        private readonly GameObject root;
        private readonly Camera camera;
        private Material sourceMaterial, instance;
        private static readonly int SignalTime = Shader.PropertyToID("_SignalTime");
        private static readonly int SignalAmount = Shader.PropertyToID("_SignalAmount");
        private static readonly int SignalStyle = Shader.PropertyToID("_SignalStyle");
        private static readonly int DialogueAfterSignal = Shader.PropertyToID("_DialogueAfterSignal");
        private static readonly int DashPositions = Shader.PropertyToID("_DashPositions");
        private static readonly int Band = Shader.PropertyToID("_Band");
        private static readonly int Reception = Shader.PropertyToID("_Reception");
        private static readonly int Tears = Shader.PropertyToID("_Tears");
        private static readonly int Blocks = Shader.PropertyToID("_Blocks");
        private static readonly int BlockColors = Shader.PropertyToID("_BlockColors");
        private static readonly int Speckles = Shader.PropertyToID("_Speckles");
        private static readonly int Grain = Shader.PropertyToID("_Grain");
        private enum Style { Legacy, Dream, Documentary, Disposal, Bar }
        private readonly float[] dreamDashes = new float[110];
        private readonly Vector4[] tears = new Vector4[9], blocks = new Vector4[5], blockColors = new Vector4[5];
        private static readonly Vector4[] blockPalette = { new(1,0,1,0), new(0,1,1,0), new(0,1,.267f,0), new(1,.933f,0,0) };
        private readonly List<int> dirtyPixels = new();
        private Texture2D speckles;
        private int lastTick = int.MinValue;
        private float lastIntensity;
        internal RectTransform Content { get; }
        internal RenderTexture Texture { get; private set; }

        internal StudioSignalCapture(StudioStage stage)
        {
            root = new GameObject("Cutscene signal source") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(root, stage.gameObject.scene);
            root.transform.position = new Vector3(0, -28000, 0);
            camera = root.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = StudioSequence.Height * .5f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 100;
#if UNITY_EDITOR
            if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(root.scene))
                camera.scene = root.scene;
#endif
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            Resize(new Vector2(1280, 720));
            var canvasRoot = new GameObject("Cutscene layers", typeof(RectTransform), typeof(Canvas));
            canvasRoot.transform.SetParent(root.transform, false);
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasRoot.transform.localPosition = Vector3.forward * 10;
            ((RectTransform)canvasRoot.transform).sizeDelta = new Vector2(StudioSequence.Width, StudioSequence.Height);
            canvas.worldCamera = camera;
            camera.aspect = StudioSequence.Width / StudioSequence.Height;
            Content = StudioStage.Rect("Source frame", canvasRoot.transform, new Vector2(StudioSequence.Width, StudioSequence.Height));
        }

        internal bool Resize(Vector2 viewport)
        {
            if (viewport.x < 1 || viewport.y < 1) return false;
            float scale = Mathf.Min(Mathf.Min(viewport.x / 1280f, viewport.y / 720f), 3f);
            int w = Mathf.Max(1, Mathf.RoundToInt(1280 * scale)), h = Mathf.Max(1, Mathf.RoundToInt(720 * scale));
            if (Texture != null && Texture.width == w && Texture.height == h) return false;
            var previous = Texture;
            Texture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32)
            {
                name = "Cutscene signal", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            camera.targetTexture = Texture;
            if (previous != null) { previous.Release(); Release(previous); }
            return true;
        }

        internal static bool DrawDialogueOnTop(Material material) => material != null &&
            material.HasProperty(DialogueAfterSignal) && material.GetFloat(DialogueAfterSignal) > .5f;

        internal Material ResolveMaterial(StudioEntry? effect, StudioEntry? line, StudioSequence sequence,
            double storyTime, float motionTime, float amount, StudioClock clock)
        {
            var source = effect?.Asset.ScreenMaterial;
            if (source == null) return null;
            if (sourceMaterial != source)
            {
                Release(instance);
                sourceMaterial = source;
                instance = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
                lastTick = int.MinValue;
            }
            if (instance.HasProperty(SignalTime)) instance.SetFloat(SignalTime, motionTime);
            if (instance.HasProperty(SignalAmount)) instance.SetFloat(SignalAmount, amount);
            if (!instance.HasProperty(SignalStyle)) return instance;
            var style = (Style)(int)instance.GetFloat(SignalStyle);
            bool reduced = DialoguePresentationSettings.Shared?.ReducedMotion == true;
            if (style == Style.Dream)
                UpdateDream(effect.Value, line, sequence, storyTime, clock, reduced);
            if (style == Style.Disposal)
                UpdateReception(effect.Value, storyTime, motionTime, reduced);
            if (style == Style.Bar && reduced) instance.SetFloat(SignalTime, 0);
            return instance;
        }

        private void UpdateDream(StudioEntry effect, StudioEntry? line, StudioSequence sequence,
            double time, StudioClock clock, bool reduced)
        {
            // 꿈은 대사를 넘길 때 원래 클립 시각으로 되돌아간다. TV의 연속 수신 시계와 다르다.
            double held = effect.ContinuesDuringWait ? clock?.DialogueHoldTime ?? 0 : 0;
            double local = time - effect.Clip.start + held;
            int tick = (int)Math.Floor((time + held) * 24);
            if (tick != lastTick)
            {
                for (int i = 0; i < dreamDashes.Length; i++)
                {
                    double random = Math.Sin(i * 119.7 + tick * 37.9) * 43758.54;
                    dreamDashes[i] = (float)((random - Math.Floor(random)) * 1280);
                }
                instance.SetFloatArray(DashPositions, dreamDashes);
                lastTick = tick;
            }
            double age = line.HasValue ? Math.Min(time + held, line.Value.Clip.end) : time;
            float progress = Mathf.Clamp01((float)((age - effect.Clip.start) / effect.Clip.duration));
            int beat = (int)Math.Floor(local / .66);
            bool burst = local - beat * .66 <= .08 + .22 * progress;
            if (line.HasValue && !line.Value.Asset.WaitForInput)
            {
                var timing = StudioDialogueTiming.For(line.Value.Asset, sequence);
                double elapsed = time - line.Value.Clip.start, last = 0;
                int count = 0, visible = 0;
                for (int i = 0; i < timing.Count; i++)
                {
                    if (!timing.IsMaskedGlyph(i)) continue;
                    count++;
                    if (timing.At(i) <= elapsed) { visible++; last = timing.At(i); }
                }
                if (count > 0)
                {
                    progress = visible / (float)count;
                    beat = Math.Max(0, visible - 1);
                    burst = visible > 0 && elapsed - last <= .06 + .04 * progress;
                }
            }
            instance.SetVector(Band, new Vector4(beat, progress, burst && !reduced ? 1 : 0, 0));
        }

        private static double ReceptionRandom(int tick, int seed)
        {
            double value = Math.Sin((tick * 13.7 + seed * 3.1) * 127.1 + 311.7) * 43758.5453;
            return value - Math.Floor(value);
        }

        private void UpdateReception(StudioEntry effect, double time, float motion, bool reduced)
        {
            float age = (float)(time - effect.Clip.start), left = (float)(effect.Clip.end - time);
            float grain = instance.GetFloat(Grain);
            float intensity = grain * Mathf.Max(1, Mathf.Max(1.28f * Mathf.Clamp01(Mathf.Min((age - .05f) / .25f, (.95f - age) / .45f)),
                1.12f * Mathf.Clamp01((1.2f - left) / .7f)));
            int tick = Mathf.FloorToInt(motion * 20);
            int beat = Mathf.FloorToInt(motion / .66f);
            instance.SetVector(Band, new Vector4(beat, 1, !reduced && motion - beat * .66f < .19f ? 1 : 0, 36));
            if (lastTick == tick && lastIntensity == intensity) return;
            lastTick = tick; lastIntensity = intensity;
            double R(int seed) => ReceptionRandom(tick, seed);
            int dx = Math.Max(1, (int)Math.Floor(.5 + (1 + 4 * intensity) * (R(3) < .85 ? 1 : 1.8)));
            int dy = R(1) < .14 * intensity ? (int)Math.Floor(.5 + (R(2) - .5) * 12 * intensity) : 0;
            instance.SetVector(Reception, new Vector4(dx, dy, tick & 1, intensity));

            int tearCount = Mathf.Min(tears.Length, (int)Math.Floor(.5 + intensity * 6 * (.4 + R(4))));
            for (int i = 0; i < tears.Length; i++)
                tears[i] = i >= tearCount ? Vector4.zero : new Vector4((int)(R(10+i)*270),
                    2+(int)(R(20+i)*16*intensity), (float)Math.Floor(.5 + (R(30+i)-.5)*70*intensity), 0);
            instance.SetVectorArray(Tears, tears);
            int count = Mathf.Min(blocks.Length, (int)Math.Floor(.5 + intensity * 5 * R(5)));
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = i >= count ? Vector4.zero : new Vector4((int)(R(60+i)*480), (int)(R(70+i)*270),
                    24+(float)R(80+i)*150, 2+(float)R(90+i)*12);
                var color = blockPalette[(int)(R(50+i)*4)];
                blockColors[i] = color * (float)(.10 + .30 * intensity * R(40+i));
            }
            instance.SetVectorArray(Blocks, blocks);
            instance.SetVectorArray(BlockColors, blockColors);
            // 480×270 수신 잡음은 두 회상에서만 쓰고, 20Hz가 바뀔 때에만 올린다.
            if (speckles == null)
            {
                speckles = new Texture2D(480, 270, TextureFormat.RGBA32, false, true)
                    { name = "Memory signal speckles", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var initial = speckles.GetRawTextureData<Color32>();
                for (int i = 0; i < initial.Length; i++) initial[i] = default;
            }
            var pixels = speckles.GetRawTextureData<Color32>();
            foreach (int index in dirtyPixels) pixels[index] = default;
            dirtyPixels.Clear();
            for (int i = 0; i < (int)(30 + 240 * intensity); i++)
            {
                byte gray = R(300+i) < .55 ? (byte)255 : (byte)0;
                var dot = new Color32(gray, gray, gray, (byte)Math.Round(255 * (.12 + .30 * intensity) * R(200+i)));
                int x = (int)(R(400+i)*480), y = (int)(R(500+i)*270);
                int end = Math.Min(480, x + (R(600+i) < .2 ? 3 : 1));
                for (; x < end; x++) { int index = y * 480 + x; pixels[index] = dot; dirtyPixels.Add(index); }
            }
            speckles.Apply(false, false);
            instance.SetTexture(Speckles, speckles);
        }

        internal void Render()
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
        }

        public void Dispose()
        {
            if (camera != null) camera.targetTexture = null;
            if (root != null) root.SetActive(false);
            if (Texture != null) Texture.Release();
            Release(root);
            Release(Texture);
            Release(instance);
            Release(speckles);
        }

        private static void Release(UnityEngine.Object item)
        {
            if (item == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(item);
            else UnityEngine.Object.DestroyImmediate(item);
        }
    }
}
