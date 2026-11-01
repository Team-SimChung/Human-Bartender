using UnityEngine;

/// <summary>Keep the authored background at its aspect ratio and cover the active gimmick camera.</summary>
[RequireComponent(typeof(SpriteRenderer))]
[DisallowMultipleComponent]
public sealed class MinigameBackground : MonoBehaviour
{
    SpriteRenderer background;

    void OnEnable() => Refresh();
    void LateUpdate() => Refresh();

    public void Refresh()
    {
        if (background == null) background = GetComponent<SpriteRenderer>();
        Camera camera = CraftGimmickStage.Find(this);
        if (background.sprite == null || camera == null || !camera.orthographic) return;

        Vector2 size = background.sprite.bounds.size;
        if (size.x <= 0 || size.y <= 0) return;
        float height = camera.orthographicSize * 2f;
        float scale = Mathf.Max(height * camera.aspect / size.x, height / size.y);
        float depth = Mathf.Clamp(10f, camera.nearClipPlane + .01f, camera.farClipPlane - .01f);
        transform.rotation = camera.transform.rotation;
        // Reset before measuring so the scale remains correct under the stage's parent scale.
        transform.localScale = Vector3.one;
        Vector3 parentScale = transform.lossyScale;
        transform.localScale = new Vector3(scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
            scale / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), 1f);
        Vector3 center = camera.ViewportToWorldPoint(new Vector3(.5f, .5f, depth));
        transform.position = center - transform.TransformVector(background.sprite.bounds.center);
    }
}
