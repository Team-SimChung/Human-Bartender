using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// "빌드(Build)" 제조 방식용 미니게임 컨트롤러. 다른 미니게임(쉐이커/스터)과 달리 별도의 판정 로직 없이
/// 서빙/재시도 버튼 입력만 이벤트로 전달하는 단순 구현체.
/// </summary>
public class BuildManager : MonoBehaviour, IMiniGameController
{
    [Header("Craft Event")]
    [SerializeField] VoidEvent craftServe;
    [SerializeField] VoidEvent craftRetry;

    [SerializeField] Canvas buttonCanvas;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>제조 완료 후 다음 진행 버튼(서빙/재시도) 캔버스를 표시한다.</summary>
    public void OnNextButton()
    {
        buttonCanvas.gameObject.SetActive(true);
    }
    /// <summary>서빙 버튼 클릭 시 craftServe 이벤트를 발생시킨다. 받던 쪽(구형 제조)은 걷어냈다.</summary>
    public void Serve()
    {
        craftServe?.Raise(new Void());
    }

    /// <summary>재시도 버튼 클릭 시 craftRetry 이벤트를 발생시킨다.</summary>
    public void Retry()
    {
        craftRetry?.Raise(new Void());
    }

    /// <summary>미니게임 시작 시 호출. Build는 별도 초기화가 필요 없어 즉시 완료 처리한다.</summary>
    public void InitGame(UniTaskCompletionSource tcs)
    {
        if (tcs != null)
            tcs.TrySetResult();
    }

    /// <summary>제조 완료 콜백 (현재 미구현).</summary>
    public void CompleteMade()
    {

    }
}
