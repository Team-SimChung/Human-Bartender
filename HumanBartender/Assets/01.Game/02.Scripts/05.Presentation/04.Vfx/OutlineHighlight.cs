using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
/// <summary>
/// SpriteRenderer의 MaterialPropertyBlock을 이용해 아웃라인 하이라이트를 부드럽게 페이드하는 컴포넌트.
/// SetHighlight(true/false)로 켜고 끄며, fadeSpeed에 따라 _OutlineIntensity 값이 보간됨.
/// </summary>
public class OutlineHighlight : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _renderer;

    [Header("페이드")]
    [Tooltip("값이 클수록 빠르게 켜지고 꺼짐")]
    [SerializeField] private float fadeSpeed = 8f;

    private static readonly int IntensityID = Shader.PropertyToID("_OutlineIntensity");

    private MaterialPropertyBlock _block;

    private float _currentIntensity;
    private float _targetIntensity;

    private void OnEnable() => ResetHighlight();

    private void OnDisable() => ResetHighlight();

    private void ResetHighlight()
    {
        _currentIntensity = _targetIntensity = 0f;
        // 필드 초기값만으로는 머터리얼의 강도를 덮어쓰지 못하므로 렌더러에도 0을 반영함.
        ApplyIntensity();
    }

    private void Update()
    {
        if (Mathf.Approximately(_currentIntensity, _targetIntensity)) return;

        _currentIntensity = Mathf.MoveTowards(_currentIntensity, _targetIntensity, fadeSpeed * Time.deltaTime);
        ApplyIntensity();
    }

    private void ApplyIntensity()
    {
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        if (_renderer == null) return;

        _block ??= new MaterialPropertyBlock();
        // 렌더러의 다른 속성을 보존하며 공유 머터리얼은 변경하지 않음.
        _renderer.GetPropertyBlock(_block);
        _block.SetFloat(IntensityID, _currentIntensity);
        _renderer.SetPropertyBlock(_block);
    }

    public void SetHighlight(bool isOn)
    {
        _targetIntensity = isOn ? 1f : 0f;
    }
}
