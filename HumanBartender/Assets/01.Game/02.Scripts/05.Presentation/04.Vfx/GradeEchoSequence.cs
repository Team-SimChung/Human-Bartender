// SEWAGE 등급 연출: 글자 잔상(복제)들이 영역 곳곳에 정신없이 생겼다 사라진다 (사이버사이코 느낌).
// Interval마다 잔상마다 보일지, 어디에(Area 안), 얼마나 크게, 무슨 색으로, 글자마다 얼마나 어긋나게 나올지를
// 시간으로 정해지는 난수로 고른다. 같은 시간이면 항상 같은 모양이라 미리보기와 게임이 같다.
// 위치·크기·색은 TMP 메시 정점에만 적용하고 보이기는 CanvasRenderer 알파로 한다 (둘 다 저장되지 않는 값).
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class GradeEchoSequence : MonoBehaviour
{
    [SerializeField, Tooltip("시간을 읽을 부모 파티클. 비우면 이 오브젝트의 파티클")] private ParticleSystem timer;
    [SerializeField, Tooltip("잔상 글자들 (본 글자와 같은 자리에 두고, 움직임은 이 스크립트가 준다)")] private TMP_Text[] echoTexts = new TMP_Text[0];
    [SerializeField, Tooltip("잔상이 나오기 시작하는 시간 (초)")] private float startTime = 0.15f;
    [SerializeField, Tooltip("잔상 배치가 바뀌는 간격 (초). 작을수록 정신없다")] private float interval = 0.06f;
    [SerializeField, Tooltip("시간(초)에 따른 동시에 보이는 잔상 수")] private AnimationCurve countOverTime = new AnimationCurve(
        new Keyframe(0f, 4f), new Keyframe(0.9f, 3f), new Keyframe(1.2f, 2f), new Keyframe(3.4f, 2f));
    [SerializeField, Tooltip("잔상이 흩어지는 범위 (캔버스 단위 = 도트, 가로·세로 반폭)")] private Vector2 area = new Vector2(90f, 16f);
    [SerializeField, Tooltip("잔상 크기 범위 (최소, 최대)")] private Vector2 scaleRange = new Vector2(0.8f, 1.15f);
    [SerializeField, Tooltip("가로로 늘어나는 정도 범위 (최소, 최대)")] private Vector2 stretchRange = new Vector2(0.9f, 1.4f);
    [SerializeField, Tooltip("글자마다 따로 어긋나는 최대 거리 (도트)")] private float letterJitter = 4f;
    [SerializeField, Tooltip("잔상 색들 (매번 이 중에서 고름)")] private Color32[] tones =
    {
        new Color32(252, 156, 156, 255), new Color32(214, 110, 122, 255), new Color32(160, 70, 88, 255), new Color32(120, 48, 66, 255),
    };
    [SerializeField, Tooltip("에디터에서 파티클 미리보기와 같이 움직이기")] private bool previewInEditMode = true;

    private ParticleSystem Timer => timer != null ? timer : (timer = GetComponent<ParticleSystem>());
    private int _lastBucket = int.MinValue;

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
        }
#endif
        _lastBucket = int.MinValue;
        Evaluate();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
        RestoreAll();
    }

    private void LateUpdate()
    {
        if (Application.isPlaying) Evaluate();
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (this == null)
        {
            EditorApplication.update -= EditorTick;
            return;
        }
        if (isActiveAndEnabled) Evaluate();
    }
#endif

    private void Evaluate()
    {
        var t = Timer;
        bool running = t != null && (t.isPlaying || t.isPaused);
        if (!Application.isPlaying && !previewInEditMode) running = false;
        float time = running ? t.time : -1f;
        int bucket = time < startTime ? -1 : Mathf.FloorToInt((time - startTime) / Mathf.Max(interval, 0.01f));
        if (bucket == _lastBucket) return;
        _lastBucket = bucket;

        int visible = bucket < 0 ? 0 : Mathf.Max(0, Mathf.RoundToInt(countOverTime.Evaluate(time)));
        for (int i = 0; i < echoTexts.Length; i++)
        {
            var text = echoTexts[i];
            if (text == null) continue;
            // 이번 간격에 뽑힌 순서: 잔상마다 난수 순위를 매기고 앞에서 visible개만 보인다.
            int rank = 0;
            float mine = Hash(bucket, i, 0);
            for (int j = 0; j < echoTexts.Length; j++)
                if (j != i && Hash(bucket, j, 0) < mine) rank++;
            bool show = rank < visible;
            text.canvasRenderer.SetAlpha(show ? 1f : 0f);
            if (show) Scatter(text, bucket, i);
        }
    }

    private void Scatter(TMP_Text text, int bucket, int index)
    {
        text.ForceMeshUpdate();
        var info = text.textInfo;
        Vector2 offset = new Vector2((Hash(bucket, index, 1) * 2f - 1f) * area.x, (Hash(bucket, index, 2) * 2f - 1f) * area.y);
        float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, Hash(bucket, index, 3));
        float stretch = Mathf.Lerp(stretchRange.x, stretchRange.y, Hash(bucket, index, 4));
        Color32 tone = tones.Length > 0 ? tones[Mathf.Min(tones.Length - 1, (int)(Hash(bucket, index, 5) * tones.Length))] : (Color32)Color.white;
        for (int c = 0; c < info.characterCount; c++)
        {
            var ch = info.characterInfo[c];
            if (!ch.isVisible) continue;
            int m = ch.materialReferenceIndex;
            int v = ch.vertexIndex;
            var verts = info.meshInfo[m].vertices;
            var cols = info.meshInfo[m].colors32;
            Vector2 jitter = new Vector2((Hash(bucket * 31 + c, index, 6) * 2f - 1f) * letterJitter,
                                        (Hash(bucket * 31 + c, index, 7) * 2f - 1f) * letterJitter * 0.5f);
            for (int k = 0; k < 4; k++)
            {
                Vector3 p = verts[v + k];
                p.x = p.x * scale * stretch + offset.x + jitter.x;
                p.y = p.y * scale + offset.y + jitter.y;
                verts[v + k] = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), p.z);   // 도트 격자에 맞춘다
                cols[v + k] = new Color32(tone.r, tone.g, tone.b, cols[v + k].a);
            }
        }
        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    private void RestoreAll()
    {
        _lastBucket = int.MinValue;
        for (int i = 0; i < echoTexts.Length; i++)
        {
            var text = echoTexts[i];
            if (text == null) continue;
            text.ForceMeshUpdate();
            text.canvasRenderer.SetAlpha(1f);
        }
    }

    private static float Hash(int bucket, int index, int salt)
    {
        unchecked
        {
            uint h = (uint)bucket * 374761393u + (uint)index * 668265263u + (uint)salt * 2246822519u + 3266489917u;
            h = (h ^ (h >> 15)) * 2246822519u;
            h = (h ^ (h >> 13)) * 3266489917u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
