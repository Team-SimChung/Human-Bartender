using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 취침 뒤 검은 화면에 명언·저자를 보이고 Day 숫자를 다음 날로 굴리는 전역 전환 화면.
/// 웹 bar-playtest/src/features/campaign/day-transition.js의 문구·시간표를 그대로 옮겼다. 명언은 대사 CSV가 아닌 화면 설정이다.
/// 일차 변경·씬 로드는 GameProgressionService가 소유하고, 이 화면은 "준비 시점"까지 기다린 뒤 공개만 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DayTransitionView : MonoBehaviour, IDayTransitionPresenter
{
    [Serializable]
    public struct Quote
    {
        [Tooltip("전환 전 일차.")] public int from;
        [Tooltip("전환 후 일차.")] public int to;
        [Tooltip("명언 본문. 줄바꿈은 그대로 표시합니다.")] [TextArea(2, 4)] public string text;
        [Tooltip("저자 줄.")] public string author;
    }

    /// <summary>한 번의 전환에서 각 장면이 시작되는 시각(초). 웹 schedule()과 같은 계산이다.</summary>
    public readonly struct Schedule
    {
        public readonly bool HasQuote;
        public readonly float QuoteAt, AuthorAt, QuoteOutAt, DayAt, RollAt, PrepareAt, TitleOutAt, RevealAt;
        public Schedule(bool hasQuote, float quoteAt, float authorAt, float quoteOutAt, float dayAt, float rollAt, float prepareAt,
            float titleOutAt, float revealAt)
        {
            HasQuote = hasQuote; QuoteAt = quoteAt; AuthorAt = authorAt; QuoteOutAt = quoteOutAt; DayAt = dayAt; RollAt = rollAt;
            PrepareAt = prepareAt; TitleOutAt = titleOutAt; RevealAt = revealAt;
        }
    }

    [Header("화면")]
    [Tooltip("켜고 끄는 자식 루트. 이 컴포넌트는 항상 켜져 있는 Canvas에 둡니다.")]
    [SerializeField] GameObject root;
    [Tooltip("검은 막. Raycast를 켜 두어 전환 중 아래 화면 입력을 막습니다.")]
    [SerializeField] Image shade;
    [SerializeField] TMP_Text quoteText;
    [SerializeField] TMP_Text authorText;
    [Tooltip("\"Day\"와 숫자를 묶은 그룹의 투명도.")]
    [SerializeField] CanvasGroup dayTitle;
    [Tooltip("굴러 올라가며 사라지는 이전 일차 숫자.")]
    [SerializeField] TMP_Text oldDigit;
    [Tooltip("아래에서 올라오는 다음 일차 숫자.")]
    [SerializeField] TMP_Text newDigit;

    [Header("명언 (웹 day-transition.js quotes)")]
    [SerializeField] List<Quote> quotes = new()
    {
        new Quote { from = 0, to = 1, text = "\"살아갈 이유가 있는 사람은 거의 어떤 삶의 방식도 견뎌낼 수 있다.\"", author = "- 프리드리히 니체, 『우상의 황혼』 -" },
        new Quote { from = 1, to = 2, text = "\"수동적인 감정은 우리가 그것에 대해 명석하고 판명한 관념을 형성하는 순간,\n수동적인 감정이기를 그친다.\"", author = "- 바뤼흐 스피노자, 『에티카』 -" },
        new Quote { from = 2, to = 3, text = "\"모든 인간은 예나 지금이나 노예와 자유인으로 나뉜다.\n하루의 3분의 2를 자기 자신을 위해 갖지 못하는 사람은 노예이기 때문이다.\"", author = "- 프리드리히 니체, 『인간적인 너무나 인간적인』 -" },
    };

    [Header("시간 (초, 웹 timing)")]
    [Tooltip("이전 장면 위로 검게 덮이는 시간.")] [Min(0)] [SerializeField] float shadeIn = .65f;
    [Tooltip("완전히 검은 뒤 명언이 나오기 전 대기.")] [Min(0)] [SerializeField] float blankHold = 1.5f;
    [Tooltip("명언이 올라오며 나타나는 시간.")] [Min(0)] [SerializeField] float quoteIn = .5f;
    [Tooltip("명언 뒤 저자가 나오기까지의 간격.")] [Min(0)] [SerializeField] float authorDelay = .3f;
    [Tooltip("저자가 올라오며 나타나는 시간.")] [Min(0)] [SerializeField] float authorIn = .7f;
    [Tooltip("명언·저자를 모두 보여 두는 시간.")] [Min(0)] [SerializeField] float hold = 4f;
    [Tooltip("명언·저자가 사라지는 시간.")] [Min(0)] [SerializeField] float quoteOut = .85f;
    [Tooltip("명언·저자가 완전히 사라진 뒤 Day가 나타나기까지 비워 두는 시간. 0이면 웹처럼 명언이 사라지는 동안 Day가 나타납니다.")]
    [Min(0)] [SerializeField] float dayGap = .6f;
    [Tooltip("Day 글자가 나타나는 시간.")] [Min(0)] [SerializeField] float dayIn = .3f;
    [Tooltip("명언이 없는 일차 조합에서 Day가 나오는 시각.")] [Min(0)] [SerializeField] float dayOnlyAt = .7f;
    [Tooltip("Day 등장 뒤 숫자가 구르기 시작하기까지.")] [Min(0)] [SerializeField] float rollDelay = .85f;
    [Tooltip("숫자가 구르는 시간.")] [Min(0)] [SerializeField] float rollDuration = .65f;
    [Tooltip("Day 등장 뒤 다음 날 준비(일차 변경·씬 로드)를 시작하는 시각.")] [Min(0)] [SerializeField] float prepareDelay = 1.55f;
    [Tooltip("Day 등장 뒤 Day 글자가 사라지기 시작하는 시각.")] [Min(0)] [SerializeField] float titleOutDelay = 2.3f;
    [Tooltip("Day 글자가 사라지는 시간.")] [Min(0)] [SerializeField] float titleOut = .3f;
    [Tooltip("Day 등장 뒤 다음 장면 공개를 시작하는 가장 이른 시각. 준비가 늦으면 더 기다립니다.")] [Min(0)] [SerializeField] float revealDelay = 2.7f;
    [Tooltip("검은 막이 걷히는 시간.")] [Min(0)] [SerializeField] float revealDuration = .9f;

    [Header("움직임 (px, 웹 ×0.75)")]
    [Tooltip("명언이 올라오는 거리. 웹 12px.")] [SerializeField] float quoteRise = 9f;
    [Tooltip("저자가 올라오는 거리. 웹 10px.")] [SerializeField] float authorRise = 8f;
    [Tooltip("숫자가 구르는 거리(숫자 칸 높이 대비). 웹 110%.")] [SerializeField] float rollTravel = 1.1f;

    Schedule schedule;
    float clock;
    float reveal;
    Tween running;
    Vector2 quoteHome, authorHome, oldHome, newHome;
    bool homesKnown;

    public bool IsShowing => root != null && root.activeSelf;
    public Schedule Current => schedule;
    public float RevealEnd => schedule.RevealAt + revealDuration;

    void Awake() => HideImmediate();

    public Quote? FindQuote(int from, int to)
    {
        foreach (var quote in quotes)
            if (quote.from == from && quote.to == to) return quote;
        return null;
    }

    public Schedule Plan(int from, int to)
    {
        float quoteAt = shadeIn + blankHold;
        float authorAt = quoteAt + authorDelay;
        bool hasQuote = FindQuote(from, to).HasValue;
        float quoteOutAt = authorAt + authorIn + hold;
        // 명언이 다 사라진 뒤 잠깐 비워 두고 Day를 띄운다. 간격이 0이면 웹과 같이 겹친다.
        float dayAt = hasQuote ? (dayGap > 0f ? quoteOutAt + quoteOut + dayGap : quoteOutAt) : dayOnlyAt;
        return new Schedule(hasQuote, quoteAt, authorAt, quoteOutAt, dayAt, dayAt + rollDelay, dayAt + prepareDelay,
            dayAt + titleOutDelay, dayAt + revealDelay);
    }

    /// <summary>암전부터 다음 날 준비 시점까지 재생한다. 끝나면 화면은 검게 덮인 채 숫자 굴림이 이어진다.</summary>
    public async UniTask PlayToPrepareAsync(int from, int to, CancellationToken token)
    {
        ValidateReferences();
        Begin(from, to);
        try
        {
            await RunClock(0f, schedule.PrepareAt, token);
            // 준비가 진행되는 동안에도 숫자 굴림·Day 사라짐은 계속된다(웹과 같음).
            running = Clock(schedule.PrepareAt, schedule.RevealAt);
        }
        catch
        {
            HideImmediate();
            throw;
        }
    }

    /// <summary>준비가 끝난 뒤 남은 연출을 마치고 검은 막을 걷는다.</summary>
    public async UniTask FinishAsync(CancellationToken token)
    {
        try
        {
            if (running != null && running.IsActive()) await running.ToUniTask(TweenCancelBehaviour.Kill, token);
            else await RunClock(clock, schedule.RevealAt, token);
            running = DOTween.To(() => reveal, value => { reveal = value; Paint(); }, 1f, revealDuration)
                .SetEase(Ease.Linear).SetUpdate(true);
            await running.ToUniTask(TweenCancelBehaviour.Kill, token);
        }
        finally { HideImmediate(); }
    }

    public void HideImmediate()
    {
        running?.Kill();
        running = null;
        clock = 0f;
        reveal = 0f;
        if (root != null) root.SetActive(false);
    }

    /// <summary>시각 t의 화면을 그린다. 에디터 미리보기·테스트에서도 쓴다.</summary>
    public void PreviewAt(int from, int to, float time, float revealProgress = 0f)
    {
        ValidateReferences();
        Begin(from, to);
        clock = time;
        reveal = revealProgress;
        Paint();
    }

    void Begin(int from, int to)
    {
        running?.Kill();
        running = null;
        CaptureHomes();
        schedule = Plan(from, to);
        var quote = FindQuote(from, to);
        quoteText.text = quote?.text ?? "";
        authorText.text = quote?.author ?? "";
        quoteText.gameObject.SetActive(quote.HasValue);
        authorText.gameObject.SetActive(quote.HasValue);
        oldDigit.text = from.ToString();
        newDigit.text = to.ToString();
        clock = 0f;
        reveal = 0f;
        root.SetActive(true);
        Paint();
    }

    void CaptureHomes()
    {
        if (homesKnown) return;
        quoteHome = quoteText.rectTransform.anchoredPosition;
        authorHome = authorText.rectTransform.anchoredPosition;
        oldHome = oldDigit.rectTransform.anchoredPosition;
        newHome = newDigit.rectTransform.anchoredPosition;
        homesKnown = true;
    }

    UniTask RunClock(float from, float to, CancellationToken token) =>
        Clock(from, to).ToUniTask(TweenCancelBehaviour.Kill, token);

    Tween Clock(float from, float to)
    {
        clock = from;
        Paint();
        return DOTween.To(() => clock, value => { clock = value; Paint(); }, to, Mathf.Max(0f, to - from))
            .SetEase(Ease.Linear).SetUpdate(true);
    }

    static float Smooth(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    static float Ratio(float t, float at, float duration) => duration <= 0f ? (t >= at ? 1f : 0f) : (t - at) / duration;

    void Paint()
    {
        float t = clock;
        float revealed = Smooth(reveal);
        SetAlpha(shade, Smooth(Ratio(t, 0f, shadeIn)) * (1f - revealed));

        if (schedule.HasQuote)
        {
            float fade = 1f - Smooth(Ratio(t, schedule.QuoteOutAt, quoteOut));
            float quoteEnter = Smooth(Ratio(t, schedule.QuoteAt, quoteIn));
            float authorEnter = Smooth(Ratio(t, schedule.AuthorAt, authorIn));
            SetAlpha(quoteText, quoteEnter * fade);
            SetAlpha(authorText, authorEnter * fade);
            quoteText.rectTransform.anchoredPosition = quoteHome + Vector2.down * ((1f - quoteEnter) * quoteRise);
            authorText.rectTransform.anchoredPosition = authorHome + Vector2.down * ((1f - authorEnter) * authorRise);
        }

        dayTitle.alpha = Smooth(Ratio(t, schedule.DayAt, dayIn)) * (1f - Smooth(Ratio(t, schedule.TitleOutAt, titleOut)));
        float roll = Smooth(Ratio(t, schedule.RollAt, rollDuration));
        float travel = oldDigit.rectTransform.rect.height * rollTravel;
        oldDigit.rectTransform.anchoredPosition = oldHome + Vector2.up * (roll * travel);
        newDigit.rectTransform.anchoredPosition = newHome + Vector2.down * ((1f - roll) * travel);
        SetAlpha(oldDigit, 1f - roll);
        SetAlpha(newDigit, roll);
    }

    static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    public void ValidateReferences()
    {
        if (root == null || root == gameObject || !root.transform.IsChildOf(transform) || shade == null || quoteText == null ||
            authorText == null || dayTitle == null || oldDigit == null || newDigit == null)
            throw new InvalidOperationException("[DayTransition] DayTransitionView의 UI 참조를 연결해 주세요. Setup Day Transition 메뉴로 만들 수 있습니다.");
    }

    void OnDestroy() => running?.Kill();
}
