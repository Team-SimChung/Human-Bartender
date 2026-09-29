using UnityEngine;
using TMPro;

/// <summary>
/// 아주 단순한 엔딩 크레딧.
/// - TitleText: 게임 내 플래그(엔딩 종류)에 따라 내용이 바뀜
/// - CreditText: 고정된 크레딧 내용
/// - 둘 다 scrollRoot 안에 들어있고, scrollRoot가 천천히 움직임
/// </summary>
public class EndingCredits : MonoBehaviour
{
    // 게임 내 엔딩 플래그 (필요에 맞게 추가/수정하세요)
    public enum EndingType
    {
        Normal,
        Happy,
        True,
        Bad
    }

    [Header("SO")]
    [SerializeField] private PlayerDataSO data;

    [Header("텍스트 참조")]
    [Tooltip("타이틀+크레딧을 모두 담고 있는 부모. 이 오브젝트가 움직입니다.")]
    [SerializeField] private RectTransform scrollRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text creditText;

    [Header("스크롤 설정")]
    [Tooltip("초당 이동 픽셀 수 (작을수록 천천히)")]
    [SerializeField] private float scrollSpeed = 40f;

    [Tooltip("체크: 아래로 / 해제: 위로(일반적인 영화 크레딧 방식)")]
    [SerializeField] private bool scrollDown = true;


    bool isStart = false;

    private void Start()
    {
        bool isBad =  data.CheckFlag("samho_death_route");
        titleText.text = isBad ? "DEMO ENDING 01\n삼호의 죽음" : "DEMO ENDING 02\n첫 번째 친구의 생존";

        isStart = false;

        Invoke("OnStart", 7.5f);
    }

    public void OnStart()
    {
        isStart = true;
    }

    private void Update()
    {
        if (!isStart) return;

        float direction = scrollDown ? -1f : 1f;
        scrollRoot.anchoredPosition += new Vector2(0f, direction * scrollSpeed * Time.deltaTime);

        if (scrollRoot.anchoredPosition.y > 1900f)
            SceneTransitionManager.Instance.LoadScene("Main");
    }

    

    private string GetTitleText(EndingType ending)
    {
        return ending switch
        {
            EndingType.Happy => "해피 엔딩",
            EndingType.True  => "트루 엔딩",
            EndingType.Bad   => "배드 엔딩",
            _                => "엔딩",
        };
    }
}
