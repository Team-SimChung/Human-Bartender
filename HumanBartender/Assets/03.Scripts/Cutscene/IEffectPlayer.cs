using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>화면 이펙트(페이드/플래시/쉐이크 등)를 비동기로 재생하는 인터페이스.</summary>
public interface IEffectPlayer
{
    UniTask PlayEffectAsync(EEffectType type, float duration = 1f, float Intensity = 0f, CancellationToken token = default);
}
