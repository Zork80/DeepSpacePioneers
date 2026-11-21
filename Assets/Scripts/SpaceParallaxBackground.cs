using UnityEngine;

/// <summary>
/// Creates and animates multi-plane parallax layers for the space background:
/// - Layer 0 (this SpriteRenderer): Deep-space galaxies & starfield (furthest, follows camera most)
/// - Layer 1 (NebulaLayer): Translucent Catan-style galactic nebulae (mid-depth)
/// - Layer 2 (ForegroundStarsLayer): Crisp near-board stars (closest, subtle parallax)
/// </summary>
public class SpaceParallaxBackground : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureAttached()
    {
        GameObject bg = GameObject.Find("Background");
        if (bg != null && bg.GetComponent<SpaceParallaxBackground>() == null)
        {
            bg.AddComponent<SpaceParallaxBackground>();
        }
    }

    [Header("Depths (World Y below board at Y=0)")]
    [SerializeField] private float deepSpaceY = -7.0f;
    [SerializeField] private float nebulaY = -3.5f;
    [SerializeField] private float foregroundStarsY = -1.5f;

    [Header("Parallax Follow Factors (0 = locked to world, 1 = locked to camera)")]
    [SerializeField] private float deepSpaceFollow = 0.42f;
    [SerializeField] private float nebulaFollow = 0.22f;
    [SerializeField] private float foregroundStarsFollow = 0.08f;

    private Camera _cam;
    private Vector3 _camOrigin;

    private Vector3 _deepBasePos;
    private Transform _nebulaTf;
    private Vector3 _nebulaBasePos;
    private Transform _fgTf;
    private Vector3 _fgBasePos;

    private void Start()
    {
        _cam = Camera.main;
        if (_cam != null)
        {
            _camOrigin = _cam.transform.position;
        }

        // Layer 0: Deep space background (this object)
        Vector3 p0 = transform.position;
        transform.position = new Vector3(p0.x, deepSpaceY, p0.z);
        _deepBasePos = transform.position;

        SpriteRenderer baseSr = GetComponent<SpriteRenderer>();
        if (baseSr != null)
        {
            baseSr.sortingOrder = 0;
        }

        // Layer 1: Galactic nebulae (mid-depth, centered over the board)
        Sprite nebulaSprite = Resources.Load<Sprite>("SpaceBackground/space_nebula");
        if (nebulaSprite != null)
        {
            GameObject nebGo = new GameObject("SpaceNebulaParallaxLayer");
            nebGo.transform.rotation = transform.rotation;
            // Scale nebula layer to cover ~56 x 31.5 world units over the board
            nebGo.transform.localScale = new Vector3(2.75f, 2.75f, 1f);
            nebGo.transform.position = new Vector3(13.8f, nebulaY, 10.5f);

            SpriteRenderer nebSr = nebGo.AddComponent<SpriteRenderer>();
            nebSr.sprite = nebulaSprite;
            nebSr.color = new Color(1f, 1f, 1f, 0.78f);
            nebSr.sortingOrder = 1;

            _nebulaTf = nebGo.transform;
            _nebulaBasePos = _nebulaTf.position;
        }

        // Layer 2: Crisp foreground stars (closest to the board)
        Sprite fgSprite = Resources.Load<Sprite>("SpaceBackground/space_stars_fg");
        if (fgSprite != null)
        {
            GameObject fgGo = new GameObject("SpaceForegroundStarsLayer");
            fgGo.transform.rotation = transform.rotation;
            fgGo.transform.localScale = new Vector3(3.2f, 3.2f, 1f);
            fgGo.transform.position = new Vector3(15f, foregroundStarsY, 14f);

            SpriteRenderer fgSr = fgGo.AddComponent<SpriteRenderer>();
            fgSr.sprite = fgSprite;
            fgSr.color = new Color(1f, 1f, 1f, 0.85f);
            fgSr.sortingOrder = 2;

            _fgTf = fgGo.transform;
            _fgBasePos = _fgTf.position;
        }
    }

    private void LateUpdate()
    {
        if (_cam == null)
        {
            _cam = Camera.main;
            if (_cam == null) return;
            _camOrigin = _cam.transform.position;
        }

        Vector3 camDelta = _cam.transform.position - _camOrigin;
        Vector3 offsetXZ = new Vector3(camDelta.x, 0f, camDelta.z);

        transform.position = _deepBasePos + offsetXZ * deepSpaceFollow;

        if (_nebulaTf != null)
        {
            _nebulaTf.position = _nebulaBasePos + offsetXZ * nebulaFollow;
        }

        if (_fgTf != null)
        {
            _fgTf.position = _fgBasePos + offsetXZ * foregroundStarsFollow;
        }
    }
}
