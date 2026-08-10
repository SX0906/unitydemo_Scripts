using UnityEngine;

/// <summary>
/// 锁定目标标记：环绕敌人身体的半透明红色圆环，弧长显示剩余血量。
/// 运行时创建，无需场景或预制体配置。
/// </summary>
public class LockOnTargetMarker : MonoBehaviour
{
    private Transform target;
    private EnemyVitals vitals;
    private LineRenderer lineRenderer;
    private LineRenderer fullRingRenderer;

    public float ringHeight = 0.9f;
    public float ringRadius = 0.24f;
    public float lineWidth = 0.08f;
    public float cameraOffset = 0.5f;
    public int arcSegments = 64;
    public Color ringColor = new Color(1f, 0.15f, 0.15f, 0.55f);

    public void Show(Transform newTarget)
    {
        target = newTarget;
        vitals = newTarget != null
            ? newTarget.GetComponentInParent<EnemyVitals>()
            : null;

        if (lineRenderer == null)
            CreateLineRenderer();

        if (lineRenderer != null)
            lineRenderer.enabled = target != null;
        if (fullRingRenderer != null)
            fullRingRenderer.enabled = target != null;
    }

    public void Hide()
    {
        target = null;
        vitals = null;
        if (lineRenderer != null)
            lineRenderer.enabled = false;
        if (fullRingRenderer != null)
            fullRingRenderer.enabled = false;
    }

    private void CreateLineRenderer()
    {
        var ringGo = new GameObject("LockOnRing");
        ringGo.transform.SetParent(transform, false);

        fullRingRenderer = CreateRingRenderer(
            ringGo.transform,
            "FullRing",
            new Color(ringColor.r, ringColor.g, ringColor.b, 0.12f),
            arcSegments + 1);

        lineRenderer = CreateRingRenderer(
            ringGo.transform,
            "HpRing",
            ringColor,
            arcSegments + 1);
    }

    private LineRenderer CreateRingRenderer(Transform parent, string name, Color color, int pointCount)
    {
        var ringGo = new GameObject(name);
        ringGo.transform.SetParent(parent, false);

        var lr = ringGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.positionCount = pointCount;
        lr.widthMultiplier = lineWidth;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = 1000;

        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            var material = new Material(spriteShader);
            material.color = Color.white;
            lr.material = material;
        }

        lr.enabled = false;
        return lr;
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            Hide();
            return;
        }

        if (vitals == null)
            vitals = target.GetComponentInParent<EnemyVitals>();

        if (vitals != null && vitals.IsDead)
        {
            Hide();
            return;
        }

        Vector3 markerPos = target.position + Vector3.up * ringHeight;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 toCamera = (cam.transform.position - markerPos).normalized;
            markerPos += toCamera * cameraOffset;
            transform.rotation = cam.transform.rotation;
        }

        transform.position = markerPos;

        float healthPercent = vitals != null ? vitals.HealthPercent : 1f;
        int visibleSegments = Mathf.Max(1, Mathf.RoundToInt(arcSegments * healthPercent));

        SetRingPositions(fullRingRenderer, arcSegments, 1f);
        SetRingPositions(lineRenderer, visibleSegments, healthPercent);

        if (fullRingRenderer != null)
            fullRingRenderer.enabled = true;
        if (lineRenderer != null)
            lineRenderer.enabled = true;
    }

    private void SetRingPositions(LineRenderer lr, int segments, float fraction)
    {
        if (lr == null) return;

        int pointCount = Mathf.Max(2, segments + 1);
        lr.positionCount = pointCount;

        for (int i = 0; i < pointCount; i++)
        {
            float t = (float)i / (pointCount - 1);
            float angle = t * 360f * fraction * Mathf.Deg2Rad;
            lr.SetPosition(
                i,
                new Vector3(
                    Mathf.Sin(angle) * ringRadius,
                    Mathf.Cos(angle) * ringRadius,
                    0f));
        }
    }
}
