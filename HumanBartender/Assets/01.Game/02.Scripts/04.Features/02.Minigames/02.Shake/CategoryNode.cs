using UnityEngine;

/// <summary>
/// 쉐이킹/스터링 미니게임의 타격 목표 노드. PooledObject를 상속받아 풀에서 관리된다.
/// 외곽선/중심 SpriteRenderer 두 개로 카테고리 색상을 표시한다.
/// </summary>
public class CategoryNode : PooledObject
{
    [SerializeField] SpriteRenderer outLineSprite;
    [SerializeField] SpriteRenderer centerSprite;


    public Color curColor;
    public float spawnTime = 0;
    public float lifeTime = 0;

    public string Category;

    public void SetNodeColor(Color color)
    {
        curColor = color;
        outLineSprite.color = curColor;
        centerSprite.color = curColor;
    }
}