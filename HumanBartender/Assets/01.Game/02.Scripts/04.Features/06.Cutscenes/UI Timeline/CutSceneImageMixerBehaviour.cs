using System.Threading;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

/// <summary>Image 트랙 믹서. 활성 클립의 이미지 데이터를 읽어 CutSceneTimelineManager의 이미지 풀을 통해 표시한다.</summary>
public class CutSceneImageMixerBehaviour : PlayableBehaviour
{
    readonly TimelineTransitionScope transitions = new();
    // Mixer가 Track에서 받아오는 바인딩
    internal CutSceneTimelineManager manager;

    // ══════════════════════════════════════════════════════════════════
    //  매 프레임 — 클립 활성/비활성 감지 후 Enter/Exit 트리거
    // ══════════════════════════════════════════════════════════════════

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        manager = playerData as CutSceneTimelineManager;
        if (manager == null) return;

        int inputCount = playable.GetInputCount();

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            var inputPlayable = (ScriptPlayable<CutSceneImageBehaviour>)playable.GetInput(i);
            var behaviour = inputPlayable.GetBehaviour();

            if (weight > 0f && !behaviour.isActive)
            {
                // ── 클립 시작 → Enter ─────────────────────────────────
                behaviour.isActive = true;
                behaviour.exitDone = false;

                Image img = manager.GetPooledImage();
                if (img == null) continue;

                Sprite sprite = LoadSprite(behaviour);
                if (sprite != null) img.sprite = sprite;
                img.SetNativeSize();

                string activeKey = GetActiveKey(behaviour);

                manager.SetImagePosition(img, behaviour.anchor, behaviour.offsetX, behaviour.offsetY);
                img.transform.SetSiblingIndex(behaviour.sortOrder);

                behaviour.assignedImage = img;
                manager.RegisterActive(activeKey, img);

                transitions.RunAsync(img, manager, token => PlayEnter(img, behaviour, token)).Forget();
            }
            else if (weight <= 0f && behaviour.isActive)
            {
                // ── 클립 종료 → Exit ──────────────────────────────────
                behaviour.isActive = false;

                if (behaviour.assignedImage != null && !behaviour.exitDone)
                {
                    behaviour.exitDone = true;
                    var exiting = behaviour.assignedImage;
                    transitions.RunAsync(exiting, manager, token => PlayExit(exiting, behaviour, token)).Forget();
                }
            }
        }
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        transitions.CancelAll();
        if (manager != null)
            manager.ResetImages();
    }

    // ══════════════════════════════════════════════════════════════════
    //  Enter 애니메이션
    // ══════════════════════════════════════════════════════════════════

    async UniTask PlayEnter(Image img, CutSceneImageBehaviour b, CancellationToken token)
    {
        RectTransform rect = img.GetComponent<RectTransform>();
        float duration = b.enterDuration;
        float dist     = b.enterSlideDistance > 0 ? b.enterSlideDistance : 1f;
        Ease  ease     = b.enterEase == Ease.Unset ? Ease.OutCubic : b.enterEase;
        RectTransform canvasRect = manager.CanvasRect;

        switch (b.enterType)
        {
            case EEneterPreset.Cut:
                img.color = Color.white;
                img.gameObject.SetActive(true);
                break;

            case EEneterPreset.FadeIn:
                img.color = new Color(1, 1, 1, 0);
                img.gameObject.SetActive(true);
                await img.DOFade(1f, duration)
                         .SetEase(b.enterEase == Ease.Unset ? Ease.Linear : b.enterEase)
                         .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;

            case EEneterPreset.SlideLeft:
            {
                Vector2 dest = rect.anchoredPosition;
                rect.anchoredPosition = dest + new Vector2(canvasRect.rect.width * dist, 0);
                img.gameObject.SetActive(true);
                await rect.DOAnchorPos(dest, duration).SetEase(ease).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;
            }
            case EEneterPreset.SlideRight:
            {
                Vector2 dest = rect.anchoredPosition;
                rect.anchoredPosition = dest - new Vector2(canvasRect.rect.width * dist, 0);
                img.gameObject.SetActive(true);
                await rect.DOAnchorPos(dest, duration).SetEase(ease).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;
            }
            case EEneterPreset.SlideUp:
            {
                Vector2 dest = rect.anchoredPosition;
                rect.anchoredPosition = dest - new Vector2(0, canvasRect.rect.height * dist);
                img.gameObject.SetActive(true);
                await rect.DOAnchorPos(dest, duration).SetEase(ease).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;
            }
            case EEneterPreset.SlideDown:
            {
                Vector2 dest = rect.anchoredPosition;
                rect.anchoredPosition = dest + new Vector2(0, canvasRect.rect.height * dist);
                img.gameObject.SetActive(true);
                await rect.DOAnchorPos(dest, duration).SetEase(ease).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;
            }
            case EEneterPreset.ScaleUp:
            {
                Ease scaleEase = b.enterEase == Ease.Unset ? Ease.OutBack : b.enterEase;
                rect.localScale = Vector3.one * 0.5f;
                img.color = new Color(1, 1, 1, 0);
                img.gameObject.SetActive(true);
                await UniTask.WhenAll(
                    rect.DOScale(1f, duration).SetEase(scaleEase).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token),
                    img.DOFade(1f, duration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token)
                );
                break;
            }
            default:
                img.color = Color.white;
                img.gameObject.SetActive(true);
                break;
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  Exit 애니메이션
    // ══════════════════════════════════════════════════════════════════

    async UniTask PlayExit(Image img, CutSceneImageBehaviour b, CancellationToken token)
    {
        RectTransform rect = img.GetComponent<RectTransform>();
        float duration = b.exitDuration;
        float dist     = b.exitSlideDistance > 0 ? b.exitSlideDistance : 1f;
        Ease  ease     = b.exitEase == Ease.Unset ? Ease.InCubic : b.exitEase;
        RectTransform canvasRect = manager.CanvasRect;

        switch (b.exitType)
        {
            case EExitPreset.Cut:
                img.color = new Color(1, 1, 1, 0);
                break;

            case EExitPreset.FadeOut:
                await img.DOFade(0f, duration)
                         .SetEase(b.exitEase == Ease.Unset ? Ease.Linear : b.exitEase)
                         .ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;

            case EExitPreset.SlideLeft:
            {
                Vector2 target = rect.anchoredPosition - new Vector2(canvasRect.rect.width * dist, 0);
                await rect.DOAnchorPos(target, duration).SetEase(ease).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;
            }
            case EExitPreset.SlideRight:
            {
                Vector2 target = rect.anchoredPosition + new Vector2(canvasRect.rect.width * dist, 0);
                await rect.DOAnchorPos(target, duration).SetEase(ease).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;
            }
            case EExitPreset.SlideUp:
            {
                Vector2 target = rect.anchoredPosition + new Vector2(0, canvasRect.rect.height * dist);
                await rect.DOAnchorPos(target, duration).SetEase(ease).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;
            }
            case EExitPreset.SlideDown:
            {
                Vector2 target = rect.anchoredPosition - new Vector2(0, canvasRect.rect.height * dist);
                await rect.DOAnchorPos(target, duration).SetEase(ease).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;
            }
            case EExitPreset.ScaleDown:
            {
                Ease scaleEase = b.exitEase == Ease.Unset ? Ease.InBack : b.exitEase;
                await UniTask.WhenAll(
                    rect.DOScale(0f, duration).SetEase(scaleEase).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token),
                    img.DOFade(0f, duration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token)
                );
                break;
            }
            default:
                await img.DOFade(0f, duration).ToUniTask(TweenCancelBehaviour.KillAndCancelAwait, token);
                break;
        }

        manager.ReturnToPool(GetActiveKey(b), img);
        b.assignedImage = null;
    }

    // ══════════════════════════════════════════════════════════════════
    //  스프라이트 로드 헬퍼
    // ══════════════════════════════════════════════════════════════════

    Sprite LoadSprite(CutSceneImageBehaviour b)
    {
        if (!string.IsNullOrEmpty(b.sheetPath))
        {
            Sprite[] allSprites = Resources.LoadAll<Sprite>($"Cutscenes/{b.sheetPath}");
            if (allSprites == null || allSprites.Length == 0)
            {
                Debug.LogWarning($"[ImageTrack] 시트를 찾을 수 없음: {b.sheetPath}");
                return null;
            }

            System.Array.Sort(allSprites, (a, c) =>
            {
                int numA = int.Parse(a.name.Substring(a.name.LastIndexOf('_') + 1));
                int numC = int.Parse(c.name.Substring(c.name.LastIndexOf('_') + 1));
                return numA.CompareTo(numC);
            });

            int idx = Mathf.Clamp(b.frameIndex, 0, allSprites.Length - 1);
            return allSprites[idx];
        }

        if (!string.IsNullOrEmpty(b.imagePath))
            return Resources.Load<Sprite>($"Cutscenes/{b.imagePath}");

        return null;
    }

    string GetActiveKey(CutSceneImageBehaviour b)
    {
        return b.imagePath;
    }
}
