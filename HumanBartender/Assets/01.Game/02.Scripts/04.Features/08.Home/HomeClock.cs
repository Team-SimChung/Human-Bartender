using System;
using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>PC의 현재 시각을 표시하고 매초 구분 기호를 0.4초 켠 뒤 0.6초 숨긴다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class HomeClock : MonoBehaviour
{
    [SerializeField] private TMP_Text timeText;
    private long displayedSecond = -1;
    private bool displayedSeparatorLit;

    private void Reset()
    {
        timeText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (timeText == null) timeText = GetComponent<TMP_Text>();
        timeText.richText = true;
        displayedSecond = -1;
        Refresh(DateTime.Now);
    }

    private void Update()
    {
        // 실제 시계를 사용하므로 일시정지(Time.timeScale == 0) 중에도 갱신한다.
        Refresh(DateTime.Now);
    }

    private void Refresh(DateTime now)
    {
        if (timeText == null) return;
        long second = now.Ticks / TimeSpan.TicksPerSecond;
        bool separatorLit = now.Millisecond < 400;
        if (second == displayedSecond && separatorLit == displayedSeparatorLit) return;
        displayedSecond = second;
        displayedSeparatorLit = separatorLit;

        // ':'을 투명하게 숨기되 폭은 유지해 숫자가 좌우로 흔들리지 않도록 한다.
        string separator = separatorLit ? ":" : "<color=#00000000>:</color>";
        timeText.text = now.ToString("HH", CultureInfo.InvariantCulture)
            + separator + now.ToString("mm", CultureInfo.InvariantCulture);
    }
}
