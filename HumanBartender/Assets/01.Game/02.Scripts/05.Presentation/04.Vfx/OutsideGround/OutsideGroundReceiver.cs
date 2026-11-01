using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>그림자가 놓일 수 있는 바닥 영역. 이동 플랫폼은 그 자식으로 두면 함께 움직인다.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed class OutsideGroundReceiver : MonoBehaviour
{
    [SerializeField] Rect area = new Rect(-1, -0.25f, 2, 0.35f);
    [SerializeField] float surfaceY;
    [SerializeField, Min(0.01f)] float footTolerance = 0.3f;
    [SerializeField] string shadowSortingLayer = "GroundShadow";
    [SerializeField] int shadowSortingOrder;
    static readonly List<OutsideGroundReceiver> Active = new List<OutsideGroundReceiver>();
    public Rect Area => area;
    public string ShadowSortingLayer => shadowSortingLayer;
    public int ShadowSortingOrder => shadowSortingOrder;

    public void Configure(Rect bounds, float localSurfaceY, float tolerance = 0.3f)
    {
        area = bounds;
        surfaceY = localSurfaceY;
        footTolerance = tolerance;
    }
    public void SetSorting(string layer, int order) { shadowSortingLayer = layer; shadowSortingOrder = order; }
    void OnEnable() { if (!Active.Contains(this)) Active.Add(this); }
    void OnDisable() => Active.Remove(this);

    public static OutsideGroundReceiver Find(Vector3 foot, Scene scene)
    {
        OutsideGroundReceiver result = null;
        float closest = float.PositiveInfinity;
        foreach (var receiver in Active)
        {
            if (receiver == null || !receiver.isActiveAndEnabled || receiver.gameObject.scene != scene) continue;
            Vector3 local = receiver.transform.InverseTransformPoint(foot);
            float distance = Mathf.Abs(local.y - receiver.surfaceY);
            if (local.x < receiver.area.xMin || local.x > receiver.area.xMax || distance > receiver.footTolerance) continue;
            if (distance < closest) { closest = distance; result = receiver; }
        }
        return result;
    }

    public void Apply(MaterialPropertyBlock block)
    {
        block.SetMatrix("_WorldToGround", transform.worldToLocalMatrix);
        block.SetVector("_GroundRect", new Vector4(area.xMin, area.yMin, area.xMax, area.yMax));
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 0.8f, 1, 0.7f);
        Gizmos.DrawWireCube(area.center, new Vector3(area.width, area.height, 0));
        Gizmos.DrawLine(new Vector3(area.xMin, surfaceY), new Vector3(area.xMax, surfaceY));
    }
}
