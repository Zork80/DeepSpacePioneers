using UnityEngine;

public class CameraMotion : MonoBehaviour
{
    public static readonly Vector3 DefaultPosition = new Vector3(17.6f, 20f, 3.5f);
    public static readonly Vector3 DefaultRotation = new Vector3(75f, 0f, 0f);

    Vector3 oldPosition;
    TriComponent[] tris;
    TriMap map;

    // Start is called before the first frame update
    void Start()
    {
        map = FindFirstObjectByType<TriMap>();
        // If the scene camera was saved at older off-center positions, reset to the centered overview
        if (Mathf.Approximately(transform.position.x, 21f) ||
            Mathf.Approximately(transform.position.x, 14.2f) ||
            Mathf.Approximately(transform.position.x, 10.8f))
        {
            ResetToOverview();
        }
        else
        {
            oldPosition = transform.position;
        }
    }

    public void ResetToOverview()
    {
        transform.position = DefaultPosition;
        transform.rotation = Quaternion.Euler(DefaultRotation);
        oldPosition = transform.position;
        CheckIfCameraMoved();
    }

    // Update is called once per frame
    void Update()
    {
        var camPos = transform.position;
        float newX = camPos.x;
        float newZ = camPos.z;

        // Allow panning across the full extent of the galaxy board
        if (camPos.x < -5f)
        {
            newX = -5f;
        }
        else if (camPos.x > 35f)
        {
            newX = 35f;
        }

        if (camPos.z < -5f)
        {
            newZ = -5f;
        }
        else if (camPos.z > 30f)
        {
            newZ = 30f;
        }

        if (newX != camPos.x || newZ != camPos.z)
        {
            transform.position = new Vector3(newX, camPos.y, newZ);
        }

        CheckIfCameraMoved();
    }

    /// <summary>
    /// Checks whether the specified tile is currently visible in the camera viewport.
    /// Accounts for sidebar UI padding on the left (default 16%) and edge margins.
    /// </summary>
    public bool IsTriVisible(Tri tri, float marginXLeft = 0.16f, float marginXRight = 0.08f, float marginY = 0.08f)
    {
        if (tri == null) return false;
        Camera cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) return false;

        Vector3 targetCenter = tri.CenterPosition();
        Vector3 vp = cam.WorldToViewportPoint(targetCenter);
        if (vp.z <= 0f) return false;

        return (vp.x >= marginXLeft && vp.x <= (1f - marginXRight) &&
                vp.y >= marginY && vp.y <= (1f - marginY));
    }

    public void PanToTri(Tri tri, bool onlyIfOffscreen = true)
    {
        if (tri == null) return;
        if (onlyIfOffscreen && IsTriVisible(tri))
        {
            return;
        }

        Vector3 targetCenter = tri.CenterPosition();
        float currentY = transform.position.y;
        float angleX = transform.rotation.eulerAngles.x;
        float rad = angleX * Mathf.Deg2Rad;
        float deltaZ = Mathf.Tan(rad) > 0.01f ? currentY / Mathf.Tan(rad) : 5.36f;

        transform.position = new Vector3(targetCenter.x, currentY, targetCenter.z - deltaZ);
        CheckIfCameraMoved();
    }

    void CheckIfCameraMoved()
    {
        if (oldPosition != transform.position)
        {
            oldPosition = transform.position;

            if (map == null)
            {
                map = TriMap.Instance;
            }

            if (map == null || (!map.AllowWrapEastWest && !map.AllowWrapNorthSouth))
            {
                return;
            }

            if (tris == null)
            {
                tris = FindObjectsByType<TriComponent>(FindObjectsSortMode.None);
            }

            foreach (TriComponent tri in tris)
            {
                tri.UpdatePosition();
            }
        }
    }
}
