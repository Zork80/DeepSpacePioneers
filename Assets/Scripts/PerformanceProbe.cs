using System;
using System.Diagnostics;
using UnityEngine;

public class PerformanceProbe : MonoBehaviour
{
    private float _lastLogTime = 0f;
    private int _frameCount = 0;
    private float _frameTimeAccum = 0f;
    private float _minDeltaTime = float.MaxValue;
    private float _maxDeltaTime = 0f;

    private bool _benchmarkCamera = false;
    private float _camBenchmarkTime = 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInit()
    {
        if (FindFirstObjectByType<PerformanceProbe>() == null)
        {
            var go = new GameObject("PerformanceProbe");
            go.AddComponent<PerformanceProbe>();
            DontDestroyOnLoad(go);
        }
    }

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Physics.simulationMode = SimulationMode.Script;

        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].ToLowerInvariant() == "-benchmark-camera")
            {
                _benchmarkCamera = true;
                break;
            }
        }

        StartCoroutine(FramePacingRoutine());
    }

    private System.Collections.IEnumerator FramePacingRoutine()
    {
        var wait = new WaitForEndOfFrame();
        var sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        while (true)
        {
            yield return wait;
            long elapsed = sw.ElapsedMilliseconds;
            // Target 15.5ms per frame (approx 60 FPS). Yield CPU to OS kernel instead of burning core in Unity spinwait
            int sleepMs = (int)(15 - elapsed);
            if (sleepMs > 1)
            {
                System.Threading.Thread.Sleep(sleepMs);
            }
            sw.Restart();
        }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _frameCount++;
        _frameTimeAccum += dt;
        if (dt < _minDeltaTime) _minDeltaTime = dt;
        if (dt > _maxDeltaTime) _maxDeltaTime = dt;

        if (_benchmarkCamera && Camera.main != null)
        {
            _camBenchmarkTime += dt;
            float t = _camBenchmarkTime * 0.8f;
            float x = 12f + Mathf.Sin(t) * 10f;
            float z = 10f + Mathf.Cos(t * 0.7f) * 8f;
            float y = 14f + Mathf.Sin(t * 0.5f) * 5f;
            Camera.main.transform.position = new Vector3(x, y, z);
            float pitch = Mathf.Lerp(45f, 75f, (y - 9f) / 10f);
            Camera.main.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        if (Time.unscaledTime - _lastLogTime >= 1.0f)
        {
            float fps = _frameCount / Mathf.Max(0.001f, _frameTimeAccum);
            float avgMs = (_frameTimeAccum / Mathf.Max(1, _frameCount)) * 1000f;
            float minMs = (_minDeltaTime < float.MaxValue ? _minDeltaTime : 0.016f) * 1000f;
            float maxMs = _maxDeltaTime * 1000f;
            long memMb = GC.GetTotalMemory(false) / (1024 * 1024);
            int gc0 = GC.CollectionCount(0);
            int gc1 = GC.CollectionCount(1);
            int gc2 = GC.CollectionCount(2);

            string tag = _benchmarkCamera ? "[PERF-CAM]" : "[PERF]";
            UnityEngine.Debug.Log($"{tag} FPS: {fps:F1} | AvgMs: {avgMs:F1}ms | MinMs: {minMs:F1}ms | MaxMs: {maxMs:F1}ms | GC Mem: {memMb}MB | GC Counts: ({gc0},{gc1},{gc2})");

            _frameCount = 0;
            _frameTimeAccum = 0f;
            _minDeltaTime = float.MaxValue;
            _maxDeltaTime = 0f;
            _lastLogTime = Time.unscaledTime;
        }
    }
}
