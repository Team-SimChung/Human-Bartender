using DG.Tweening;
using UnityEngine;
using System;

/// <summary>
/// 쉐이킹 미니게임의 타격 노드. DSP 타임 기반으로 BPM에 맞춰 경로 상의 점들 사이를 왕복 이동한다.
/// 시퀀스 인덱스가 변경될 때 OnChangeSeqIndex 이벤트를 발생시켜 노드 스폰 타이밍을 알린다.
/// </summary>
public class ShakingStrikeNode : MonoBehaviour
{
    [SerializeField] BoolEvent OnChangeSeqIndex;

    [SerializeField] Vector3 fromPos;
    [SerializeField] Vector3 toPos;

    [SerializeField] Ease ease = Ease.InOutQuad;
    [SerializeField] float curBpm = 60;

    double startDspTime;
    float beatDuration;

    int[] targetSequence = { 0, 1, 2, 3, 2, 1 };
    Vector3[] targetPostions;

    bool isStart = false;

    double elapsed;
    double beats;

    int fromSeq;
    int toSeq;

    int seqIndex;
    float t;

    float easedT;

    int lastSeqIndex = -1;

    public void Handle()
    {
        if (!isStart) return;

        elapsed = AudioSettings.dspTime - startDspTime;
        if (elapsed < 0)
        {
            transform.position = targetPostions[targetSequence[0]];
            return;
        }

        beats = elapsed / beatDuration;

        seqIndex = (int)beats;
        t = (float)(beats - Math.Floor(beats));

        fromSeq = seqIndex % targetSequence.Length;
        toSeq = (seqIndex + 1) % targetSequence.Length;

        fromPos = targetPostions[targetSequence[fromSeq]];
        toPos = targetPostions[targetSequence[toSeq]];

        easedT = DOVirtual.EasedValue(0f, 1f, t, ease);
        transform.position = Vector3.LerpUnclamped(fromPos, toPos, easedT);

        if (seqIndex != lastSeqIndex)
        {
            lastSeqIndex = seqIndex;
            //이벤트 호출
            Logger.Log("Change LastSeqIndex");

            if (targetSequence[fromSeq] == 3)
                OnChangeSeqIndex?.Raise(false);
            else if (targetSequence[fromSeq] == 0)
                OnChangeSeqIndex?.Raise(true);
        }
    }

    public void InitToStart(Vector3[] line, float bpm)
    {
        lastSeqIndex = -1;
        curBpm = bpm;
        targetPostions = line;

        beatDuration = 60f / bpm;
        startDspTime = AudioSettings.dspTime + 0.1f;

        isStart = true;
    }
}
