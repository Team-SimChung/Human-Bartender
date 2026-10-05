using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>캐릭터 애니메이션 리소스(AnimationClip AssetReference)를 보유하는 ScriptableObject.</summary>
[CreateAssetMenu(fileName = "CharacterResourceData", menuName = "Scriptable Objects/CharacterResourceData")]
public class CharacterResourceData : ScriptableObject
{
    public AssetReferenceT<AnimationClip> clip;
}
