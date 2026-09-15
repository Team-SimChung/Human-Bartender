using UnityEngine;

public class OutsideSpotManager : MonoBehaviour
{
    [SerializeField] private NewSpotDataSO spotDataSO;

    private void Awake()
    {
        RegisterAllChildSpots();
    }

    /// <summary>
    /// 하위에 있는 모든 SpotPoint를 찾아 SpotDataSO에 위치/회전 정보를 등록합니다.
    /// </summary>
    public void RegisterAllChildSpots()
    {
        if (spotDataSO == null)
        {
            Debug.LogError("[OutsideSpotManager] spotDataSO가 할당되지 않았습니다.");
            return;
        }

        // 자식 게임오브젝트들에 포함된 모든 SpotPoint 컴포넌트 수집 (비활성화된 객체 포함)
        SpotPoint[] spotPoints = GetComponentsInChildren<SpotPoint>(true);

        int registeredCount = 0;

        foreach (var spotPoint in spotPoints)
        {
            if (spotPoint == null || string.IsNullOrEmpty(spotPoint.SpotID))
            {
                Debug.LogWarning($"[OutsideSpotManager] SpotID가 비어있는 SpotPoint가 있습니다: {spotPoint?.gameObject.name}");
                continue;
            }

            // SO의 RegisterSpotTransform 메서드를 통해 위치 및 회전값 전달
            bool isSuccess = spotDataSO.RegisterSpotTransform(
                spotPoint.SpotID,
                spotPoint.transform.position,
                spotPoint.transform.rotation
            );

            if (isSuccess)
            {
                registeredCount++;
            }
        }

        Debug.Log($"[OutsideSpotManager] 총 {registeredCount}개 / {spotPoints.Length}개의 SpotPoint 등록 완료.");
    }
}