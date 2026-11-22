using TMPro;
using UnityEngine;

/// <summary>기록 한 건을 표시한다. 진행 표시는 텍스트 또는 이미지 오브젝트로 교체할 수 있다.</summary>
public sealed class DialogueHistoryEntryView : MonoBehaviour
{
    [SerializeField] TMP_Text speakerName;
    [SerializeField] TMP_Text dialogueText;
    [SerializeField] GameObject currentIndicator;

    public void Show(DialogueHistoryStore.Entry entry)
    {
        // 기록은 이미 해석된 글자다. 문자로 출력된 '<...>'를 다시 서식으로 해석하지 않는다.
        speakerName.richText = false;
        dialogueText.richText = false;
        speakerName.text = entry.Speaker;
        dialogueText.text = entry.Text;
        currentIndicator.SetActive(entry.IsCurrent);
    }
}
