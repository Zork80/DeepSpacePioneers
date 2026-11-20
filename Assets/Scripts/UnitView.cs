using UnityEngine;

public class UnitView : MonoBehaviour
{
    public TriMap TriMap { get; set; }

    [SerializeField]
    private float moveSpeed = 8.5f; // Constant snappy speed in world units / second
    public float MoveSpeed
    {
        get => moveSpeed;
        set => moveSpeed = value;
    }

    [SerializeField]
    private float turnSpeed = 2160f; // Instantaneous, agile rotation towards flight direction
    public float TurnSpeed
    {
        get => turnSpeed;
        set => turnSpeed = value;
    }

    private readonly System.Collections.Generic.Queue<Vector3> _waypointQueue = new System.Collections.Generic.Queue<Vector3>();
    private Vector3 targetPosition;
    private Vector3 _lastEnqueuedPos;
    private bool isMoving = false;

    public void InitializePosition(Vector3 pos)
    {
        transform.position = pos;
        targetPosition = pos;
        _lastEnqueuedPos = pos;
        _waypointQueue.Clear();
        isMoving = false;
        TriMap?.UnregisterUnitViewMoving(this);
    }

    void Start()
    {
        if (TriMap == null)
        {
            TriMap = TriMap.Instance;
        }
        if (!isMoving)
        {
            targetPosition = transform.position;
            _lastEnqueuedPos = transform.position;
        }
    }

    void OnDisable()
    {
        if (isMoving)
        {
            _waypointQueue.Clear();
            isMoving = false;
            TriMap?.UnregisterUnitViewMoving(this);
        }
    }

    void OnDestroy()
    {
        if (isMoving)
        {
            _waypointQueue.Clear();
            isMoving = false;
            TriMap?.UnregisterUnitViewMoving(this);
        }
    }

    public void TeleportTo(Vector3 pos)
    {
        _waypointQueue.Clear();
        transform.position = pos;
        targetPosition = pos;
        _lastEnqueuedPos = pos;
        isMoving = false;
        if (TriMap == null)
        {
            TriMap = TriMap.Instance;
        }
        TriMap?.UnregisterUnitViewMoving(this);
    }

    public void OnUnitMoved(Tri oldTri, Tri newTri)
    {
        if (this == null || gameObject == null)
        {
            return;
        }

        if (TriMap == null)
        {
            TriMap = TriMap.Instance;
        }
        if (TriMap == null) return;

        Vector3 nextPos = TriMap.GetShipHoverPosition(newTri);

        if (transform.position == nextPos && !isMoving && _waypointQueue.Count == 0)
        {
            targetPosition = nextPos;
            _lastEnqueuedPos = nextPos;
            return;
        }

        // If distance from last waypoint is large (initial spawn or map boundary wrap), teleport immediately
        Vector3 refPos = isMoving ? _lastEnqueuedPos : transform.position;
        if (oldTri == null || Vector3.Distance(refPos, nextPos) > 8.0f)
        {
            _waypointQueue.Clear();
            transform.position = nextPos;
            targetPosition = nextPos;
            _lastEnqueuedPos = nextPos;
            isMoving = false;
            TriMap?.UnregisterUnitViewMoving(this);
            return;
        }

        if (!isMoving)
        {
            targetPosition = nextPos;
            _lastEnqueuedPos = nextPos;
            isMoving = true;
            TriMap?.RegisterUnitViewMoving(this);
        }
        else
        {
            _waypointQueue.Enqueue(nextPos);
            _lastEnqueuedPos = nextPos;
        }
    }

    void Update()
    {
        if (TriMap == null || !isMoving)
        {
            return;
        }

        float remainingStep = moveSpeed * Time.deltaTime;

        while (remainingStep > 0f && isMoving)
        {
            // Smoothly rotate the ship towards its current target waypoint
            Vector3 moveDir = targetPosition - transform.position;
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            float dist = Vector3.Distance(transform.position, targetPosition);
            if (dist <= remainingStep)
            {
                transform.position = targetPosition;
                remainingStep -= dist;

                if (_waypointQueue.Count > 0)
                {
                    targetPosition = _waypointQueue.Dequeue();
                }
                else
                {
                    isMoving = false;
                    TriMap?.UnregisterUnitViewMoving(this);
                    break;
                }
            }
            else
            {
                transform.position = Vector3.MoveTowards(transform.position, targetPosition, remainingStep);
                remainingStep = 0f;
            }
        }
    }
}
