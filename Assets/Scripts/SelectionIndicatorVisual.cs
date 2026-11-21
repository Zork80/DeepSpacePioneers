using UnityEngine;

/// <summary>
/// Replaces the legacy green smoke ParticleSystem on SelectionIndicator with a crisp,
/// animated sci-fi holographic reticle (pulsing inner ring + rotating outer targeting arcs).
/// </summary>
public class SelectionIndicatorVisual : MonoBehaviour
{
    private const int InnerSegments = 48;
    private const int ArcSegments = 12;
    private const int ArcCount = 4;

    private LineRenderer _innerRing;
    private Transform _outerRoot;
    private readonly LineRenderer[] _outerArcs = new LineRenderer[ArcCount];
    private readonly LineRenderer[] _ticks = new LineRenderer[ArcCount];
    private Material _lineMaterial;
    private bool _initialized;

    private void Awake()
    {
        InitializeVisuals();
    }

    private void OnEnable()
    {
        InitializeVisuals();
        DisableLegacyParticles();
    }

    private void DisableLegacyParticles()
    {
        ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem ps in systems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null) psr.enabled = false;
            ps.gameObject.SetActive(false);
        }
    }

    private void InitializeVisuals()
    {
        if (_initialized) return;
        _initialized = true;

        DisableLegacyParticles();

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        _lineMaterial = new Material(shader);

        // 1. Inner pulsing ring
        GameObject innerGo = new GameObject("InnerReticleRing");
        innerGo.transform.SetParent(transform, false);
        innerGo.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        _innerRing = CreateLineRenderer(innerGo, 0.035f, true, new Color(0.20f, 0.92f, 1.0f, 0.90f));
        UpdateCircle(_innerRing, 0.48f, InnerSegments);

        // 2. Outer rotating targeting arcs + inward ticks
        GameObject outerGo = new GameObject("OuterReticleArcs");
        outerGo.transform.SetParent(transform, false);
        outerGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        _outerRoot = outerGo.transform;

        Color arcColor = new Color(1.0f, 0.84f, 0.25f, 0.95f);
        for (int i = 0; i < ArcCount; i++)
        {
            float baseAngle = i * 90f;

            GameObject arcObj = new GameObject($"Arc_{i}");
            arcObj.transform.SetParent(_outerRoot, false);
            _outerArcs[i] = CreateLineRenderer(arcObj, 0.045f, false, arcColor);
            UpdateArc(_outerArcs[i], 0.62f, baseAngle - 26f, baseAngle + 26f, ArcSegments);

            GameObject tickObj = new GameObject($"Tick_{i}");
            tickObj.transform.SetParent(_outerRoot, false);
            _ticks[i] = CreateLineRenderer(tickObj, 0.04f, false, arcColor);
            float rad = baseAngle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            _ticks[i].positionCount = 2;
            _ticks[i].SetPosition(0, dir * 0.68f);
            _ticks[i].SetPosition(1, dir * 0.56f);
        }
    }

    private void Update()
    {
        if (!_initialized) return;

        float t = Time.time;

        // Smooth rotation of outer targeting brackets
        if (_outerRoot != null)
        {
            _outerRoot.localRotation = Quaternion.Euler(0f, t * 42f, 0f);
        }

        // Subtle pulse of inner ring radius and brightness
        if (_innerRing != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4.2f);
            float radius = Mathf.Lerp(0.45f, 0.52f, pulse);
            Color c = new Color(0.20f, 0.92f, 1.0f, Mathf.Lerp(0.60f, 0.98f, pulse));
            _innerRing.startColor = c;
            _innerRing.endColor = c;
            UpdateCircle(_innerRing, radius, InnerSegments);
        }
    }

    private LineRenderer CreateLineRenderer(GameObject target, float width, bool loop, Color color)
    {
        LineRenderer lr = target.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.material = _lineMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.sortingOrder = 10;
        return lr;
    }

    private static void UpdateCircle(LineRenderer lr, float radius, int segments)
    {
        lr.positionCount = segments;
        float step = (Mathf.PI * 2f) / segments;
        for (int i = 0; i < segments; i++)
        {
            float a = i * step;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
    }

    private static void UpdateArc(LineRenderer lr, float radius, float startDeg, float endDeg, int segments)
    {
        lr.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float deg = Mathf.Lerp(startDeg, endDeg, (float)i / segments);
            float rad = deg * Mathf.Deg2Rad;
            lr.SetPosition(i, new Vector3(Mathf.Cos(rad) * radius, 0f, Mathf.Sin(rad) * radius));
        }
    }
}
