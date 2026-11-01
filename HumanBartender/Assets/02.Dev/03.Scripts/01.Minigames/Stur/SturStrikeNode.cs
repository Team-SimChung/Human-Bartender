using DG.Tweening;
using UnityEngine;
using System;

/// <summary>
/// 스터링 미니게임의 타격 노드. DSP 타임 기반으로 BPM에 맞춰 타원형 경로를 공전한다.
/// Perlin 노이즈 없이 순수 삼각함수(cos/sin)로 위치를 계산하므로 스크러빙에도 일관된 결과를 낸다.
/// </summary>
public class SturStrikeNode : MonoBehaviour
{
    [SerializeField] Vector3 center;

    [SerializeField] Ease ease = Ease.InOutQuad;
    [SerializeField] float curBpm = 60;
    [SerializeField] float curBeatsPerLap = 4f;
    [SerializeField] float radiusX = 2.4f;
    [SerializeField] float radiusY = 2.0f;

    double startDspTime;
    float beatDuration;

    bool isStart = false;

    double elapsed;
    double beats;
    double laps;
    float lapT;
    float easedT;
    float angle;

    Vector3 offset;

    double audioTime;
    double lastDsp;
    float dspDelta;

    public void Handle()
    {
        if (!isStart) return;

        double dsp = AudioSettings.dspTime;
        if (dsp != lastDsp)   
        {
            audioTime = dsp - startDspTime;
            lastDsp = dsp;
            dspDelta = 0f;
        }
        else
        {
            dspDelta += Time.unscaledDeltaTime; 
        }

        elapsed = audioTime + dspDelta;
        if (elapsed < 0) return;

        UpdatePosition();
    }

    public void UpdatePosition()
    {
        beats = elapsed / beatDuration;
        laps = beats / curBeatsPerLap;
        lapT = (float)(laps - Math.Floor(laps));

        angle = lapT * Mathf.PI * 2f;
        offset = new Vector3(
            Mathf.Cos(-angle) * radiusX,
            Mathf.Sin(-angle) * radiusY,
            0f
        );

        transform.position = center + offset;
    }

    public void Init(Vector3 centerPos, float bpm, float beatLap, float radX, float radY)
    {
        center = centerPos;
        curBpm = bpm;
        curBeatsPerLap = beatLap;
        beatDuration = 60f / bpm;
        radiusX = radX;
        radiusY = radY;

        UpdatePosition();
    }

    public void InitToStart()
    {
        startDspTime = AudioSettings.dspTime + 0.1f;
        isStart = true;
    }
}
