using UnityEngine;
using UnityEngine.UI;

/// <summary>ImageViewer 프리팹의 콘텐츠 연결. 크기와 배치는 프리팹에서 편집함.</summary>
public sealed class WorldInteractionImageView : MonoBehaviour
{
    [SerializeField] private Image inspectionImage;

    internal Image InspectionImage => inspectionImage;
    public bool IsConfigured => inspectionImage != null;

    public void Show(Sprite sprite)
    {
        inspectionImage.sprite = sprite;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        inspectionImage.sprite = null;
    }
}
