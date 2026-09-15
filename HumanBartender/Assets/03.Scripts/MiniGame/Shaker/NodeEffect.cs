using Cysharp.Threading.Tasks;
using DG.Tweening;
using System;
using UnityEngine;

/// <summary>
/// 노드 타격 시 표시되는 이펙트 오브젝트. 풀에서 꺼내진 후 duration 초 후 자동으로 반납된다.
/// </summary>
public class NodeEffect : PooledObject
{
    [SerializeField] float duration = 1f;
    [SerializeField] SpriteRenderer sprite;
    [SerializeField] Vector3 effectfromSize;
    [SerializeField] Vector3 effectToSize;
    [SerializeField] Ease ease;

    void Start()
    {
        
    }
     
    public void SetNodeColor(Color color)
    {
        sprite.color = color;
    }
    public void PlayEffect()
    {
        //sprite.transform.localScale = effectfromSize;
        //ActiveEffect().Forget();

        ReturnEffect().Forget();
    }

    public async UniTask ReturnEffect()
    {
        bool canceled = await UniTask.Delay(
            TimeSpan.FromSeconds(duration),
            cancellationToken: this.GetCancellationTokenOnDestroy()
        ).SuppressCancellationThrow();

        if (canceled || this == null) return;

        parentPool.Return(gameObject);
    }

    public async UniTaskVoid ActiveEffect()
    {
        float duraion = 0.5f;
        float t = 0f;

        Vector3 fromSize = sprite.transform.localScale;
        Vector3 toSize = effectToSize;

        while (t < duraion)
        {
            t += Time.deltaTime;
            float k = DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(t / duraion), ease);

            sprite.transform.localScale = Vector3.Lerp(fromSize, toSize, k);
            
            await UniTask.Yield();
        }

        parentPool.Return(this.gameObject);
        return;
    }
}
