using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// <summary>대사 한 줄을 화면에 띄우는 데 필요한 것.</summary>
public readonly struct StorySayRequest
{
    /// <summary>말하는 사람. characters.json의 id다.</summary>
    public string ActorId { get; }

    /// <summary>표정 키. 없으면 표정을 바꾸지 않는다.</summary>
    public string Expression { get; }

    /// <summary>현재 언어의 본문.</summary>
    public string Body { get; }

    /// <summary>
    /// 플레이어(루나)의 말인지. 바에서는 1인칭이라 초상 없이 이름과 본문만 띄우는데,
    /// 그 판단은 화면마다 다를 수 있어 여기서는 사실만 전한다.
    /// </summary>
    public bool IsPlayer { get; }

    public StorySayRequest(string actorId, string expression, string body, bool isPlayer)
    {
        ActorId = actorId;
        Expression = expression;
        Body = body;
        IsPlayer = isPlayer;
    }
}

/// <summary>화면에 띄울 선택지 하나.</summary>
public readonly struct StoryChoiceOption
{
    /// <summary>버튼에 적을 현재 언어의 문구.</summary>
    public string Text { get; }

    /// <summary>고를 수 있는지. 거짓이면 감추지 않고 회색으로 둔다(§12.4).</summary>
    public bool IsSelectable { get; }

    /// <summary>고를 수 없을 때 보여 줄 이유. 고를 수 있으면 null이다.</summary>
    public string LockReason { get; }

    public StoryChoiceOption(string text, bool isSelectable, string lockReason)
    {
        Text = text;
        IsSelectable = isSelectable;
        LockReason = lockReason;
    }
}

/// <summary>
/// 대본 실행기가 화면에 무언가를 요청할 때 쓰는 창구.
///
/// 공용 대화 시스템이 IDialoguePresenter로 바 화면과 실외 화면을 갈라 놓은 것과 같은 이유로 둔다.
/// 대본을 걷는 방법은 어디서나 같지만 그리는 방법은 화면마다 다르다 — 바에는 좌석에 앉은 인물과
/// 말풍선이 있고, 길거리에는 좌석이 없다.
///
/// 실행기는 이 인터페이스 너머를 모른다. 그래서 같은 순회 코드로 다른 화면을 돌릴 수 있고,
/// 화면을 바꾸는 일이 실행기를 고치는 일이 되지 않는다.
/// </summary>
public interface IStoryPresenter
{
    /// <summary>대사를 띄우고 본문이 다 나올 때까지 기다린다. 넘기는 입력을 기다리는 것은 실행기가 한다.</summary>
    UniTask ShowSayAsync(StorySayRequest request, CancellationToken token);

    /// <summary>인물을 자리에 세운다. 자리 개념이 없는 화면은 아무것도 하지 않아도 된다.</summary>
    UniTask EnterAsync(string actorId, ESlotType slot, CancellationToken token);

    /// <summary>자리를 비운다.</summary>
    void Exit(ESlotType slot);

    /// <summary>
    /// 지금 앉아 있는 사람들에 맞춰 화면을 잡는다(2부 명세 §10.1.1).
    ///
    /// enter·exit로 인원이 바뀔 때마다 실행기가 부른다. 실행기는 누가 어디 앉았는지만 알고
    /// 그것을 몇 대 몇의 프레임으로 옮기는 일은 화면이 한다 — 좌석이 없는 화면은 아무것도 하지 않는다.
    ///
    /// 카메라가 다 움직인 뒤에 돌아온다. 움직이는 중에 다음 대사가 뜨면 말하는 사람이 화면 밖에 있다.
    /// </summary>
    UniTask ApplyFramingAsync(IReadOnlyList<ESlotType> occupiedSlots, CancellationToken token);

    /// <summary>
    /// 선택지를 띄우고 고를 때까지 기다린다. 고른 것의 자리 번호를 돌려준다.
    ///
    /// 고를 수 없는 항목도 화면에서 지우지 않는다 — 무엇을 놓쳤는지 보이지 않으면 조건이 없는 것과 같다.
    /// </summary>
    UniTask<int> ShowChoicesAsync(IReadOnlyList<StoryChoiceOption> options, CancellationToken token);

    /// <summary>타이핑 중이면 즉시 다 보여 준다.</summary>
    void SkipTyping();

    /// <summary>대본이 끝나 화면을 정리한다.</summary>
    void Clear();
}
