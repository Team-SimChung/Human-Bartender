using UnityEngine;

/// <summary>선택지 글자 글리치 연출 값. 선택지(ChoicePanel)와 타이틀 메뉴가 같은 에셋을 공유한다.</summary>
[CreateAssetMenu(fileName = "ChoiceGlitchSettings", menuName = "Scriptable Objects/UI/ChoiceGlitchSettings")]
public sealed class UIChoiceGlitchSettings : ScriptableObject
{
    // 에셋이 연결되지 않았을 때도 같은 값을 쓰도록 기본값은 여기 한 곳에만 둔다.
    public const float DefaultDuration = .3f;
    public const float DefaultFrameRate = 60f;
    public const int DefaultActiveFrames = 15;
    public const int DefaultRestEvery = 6;
    public const float DefaultJitter = .75f;
    public static readonly Color32 DefaultTintA = new Color32(101, 247, 255, 255);
    public static readonly Color32 DefaultTintB = new Color32(230, 166, 214, 255);

    [Header("Timing")]
    [Tooltip("한 주기 길이(초). 홀드 선택지는 이 주기를 반복하고, 일반 선택지는 포커스 때 이만큼만 재생한다.")]
    [SerializeField, Min(.01f)] float duration = DefaultDuration;
    [Tooltip("주기 안의 프레임을 나누는 기준 FPS. 높을수록 지지직 패턴이 빨리 바뀐다.")]
    [SerializeField, Min(1f)] float frameRate = DefaultFrameRate;
    [Tooltip("주기 안에서 글리치가 보이는 마지막 프레임. 이후 프레임은 원본으로 쉰다.")]
    [SerializeField, Min(0)] int activeFrames = DefaultActiveFrames;
    [Tooltip("N 프레임마다 원본 프레임을 한 번 끼워 깜빡임을 만든다. 0이면 끼우지 않는다.")]
    [SerializeField, Min(0)] int restEvery = DefaultRestEvery;

    [Header("Look")]
    [Tooltip("글자가 좌우로 흔들리는 한 칸 크기. 실제 최대 이동량은 이 값의 2배.")]
    [SerializeField, Min(0f)] float jitter = DefaultJitter;
    [Tooltip("글리치 색 1 (시안 계열).")]
    [SerializeField] Color tintA = DefaultTintA;
    [Tooltip("글리치 색 2 (핑크 계열).")]
    [SerializeField] Color tintB = DefaultTintB;

    [Header("Shader")]
    [Tooltip("Custom/TMPGlitch 셰이더 머티리얼. 연결하면 글리치 동안 RGB 분리·가로 찢김·스캔라인이 더해진다. 비우면 글자 흔들기만 한다.")]
    [SerializeField] Material textGlitchMaterial;

    public float Duration => duration;
    public float FrameRate => frameRate;
    public int ActiveFrames => activeFrames;
    public int RestEvery => restEvery;
    public float Jitter => jitter;
    public Color32 TintA => tintA;
    public Color32 TintB => tintB;
    public Material TextGlitchMaterial => textGlitchMaterial;
}
