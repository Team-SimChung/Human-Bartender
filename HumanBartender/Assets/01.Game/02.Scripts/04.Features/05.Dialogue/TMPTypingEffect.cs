using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using TMPro;
using UnityEngine;

public static class TMPTypingEffect
{
    /// <summary>
    /// TMP 텍스트에 한 글자씩 나타나는 타이핑 효과를 적용한다.
    /// </summary>
    /// <param name="label">대상 TMP 텍스트</param>
    /// <param name="content">표시할 전체 문자열</param>
    /// <param name="delay">글자 간 딜레이 (초)</param>
    /// <param name="token">취소 토큰 (선택)</param>
    public static async UniTask Type(
        TMP_Text label,
        string content,
        float delay = 0.05f,
        CancellationToken token = default)
    {
        if (label == null || string.IsNullOrEmpty(content)) return;

        label.text = content;
        label.maxVisibleCharacters = 0;
        label.ForceMeshUpdate();

        int total = label.textInfo.characterCount;

        try
        {
            for (int i = 0; i <= total; i++)
            {
                label.maxVisibleCharacters = i;
                if (i < total)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: token);
            }
        }
        catch (System.OperationCanceledException)
        {
            // 취소되면 즉시 전체 표시
            label.maxVisibleCharacters = label.textInfo.characterCount;
        }
    }
}