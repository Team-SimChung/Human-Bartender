using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
/// <summary>
/// 미니게임 결과 게이지 UI. 성공(녹색)/대기(흰색)/실패(빨강) 비율을 셰이더 머티리얼 프로퍼티로 전달한다.
/// UpdateValues()를 호출하면 _LeftBoundary, _RightBoundary, _Smoothness가 즉시 반영된다.
/// </summary>
public class GradientRatioController : MonoBehaviour
{
    [SerializeField] Image targetImage;
    [SerializeField] Material instancedMaterial;

    [Header("Values (비율 설정)")]
    public float totalValue = 100f;
    public float greenValue = 0f;
    public float whiteValue = 100f;
    public float redValue = 0f;   

    [Header("Settings (경계 뚜렷함)")]
    [Range(0.0001f, 0.5f)]
    [Tooltip("값이 작을수록 경계가 칼처럼 보이고, 클수록 자연스럽게 섞입니다.")]
    public float smoothness = 0.05f;

    void Start()
    {
        instancedMaterial = new Material(targetImage.material);
        targetImage.material = instancedMaterial;

        ApplyGradientSettings();
    }

    public void UpdateValues(float total, float green, float white, float red)
    {
        totalValue = total;
        greenValue = green;
        whiteValue = white;
        redValue = red;

        ApplyGradientSettings();
    }

    private void ApplyGradientSettings()
    {
        if (instancedMaterial == null || totalValue <= 0) return;

        float leftBoundary = greenValue / totalValue;
        float rightBoundary = (greenValue + whiteValue) / totalValue;

        instancedMaterial.SetFloat("_LeftBoundary", leftBoundary);
        instancedMaterial.SetFloat("_RightBoundary", rightBoundary);
        instancedMaterial.SetFloat("_Smoothness", smoothness);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            ApplyGradientSettings();
        }
    }

    void OnDestroy()
    {
        if (instancedMaterial != null)
        {
            Destroy(instancedMaterial);
        }
    }
}