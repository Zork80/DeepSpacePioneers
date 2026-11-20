using UnityEngine;

public class SelectionController : MonoBehaviour {

    private bool _subscribedToMapEvents;

    void Awake()
    {
        if (triMap == null)
        {
            triMap = FindFirstObjectByType<TriMap>();
        }
    }

    void OnEnable()
    {
        if (triMap == null)
        {
            triMap = FindFirstObjectByType<TriMap>();
        }
        SubscribeToMapEvents();
    }

    void OnDisable()
    {
        UnsubscribeFromMapEvents();
    }

	// Use this for initialization
	void Start () {
        mouseController = FindFirstObjectByType<MouseController>();
        if (triMap == null)
        {
            triMap = FindFirstObjectByType<TriMap>();
        }
        SubscribeToMapEvents();
	}

    void OnDestroy()
    {
        UnsubscribeFromMapEvents();
    }

    private void SubscribeToMapEvents()
    {
        if (!_subscribedToMapEvents && triMap != null)
        {
            triMap.OnPlayerChanged += HandlePlayerChanged;
            triMap.OnTurnPhaseChanged += HandleTurnPhaseChanged;
            _subscribedToMapEvents = true;
        }
    }

    private void UnsubscribeFromMapEvents()
    {
        if (_subscribedToMapEvents && triMap != null)
        {
            triMap.OnPlayerChanged -= HandlePlayerChanged;
            triMap.OnTurnPhaseChanged -= HandleTurnPhaseChanged;
            _subscribedToMapEvents = false;
        }
    }

    private void HandlePlayerChanged(Player player)
    {
        ClearSelection();
    }

    private void HandleTurnPhaseChanged(Player player, GameTurnPhase phase)
    {
        if (SelectedUnit != null && (triMap == null || !triMap.CanSelectUnit(SelectedUnit)))
        {
            ClearSelection();
        }
    }

    public void ClearSelection()
    {
        if (mouseController != null)
        {
            mouseController.ClearPath();
        }
        SelectedUnit = null;
        SelectedColony = null;
        if (SelectionIndicator != null)
        {
            SelectionIndicator.SetActive(false);
        }
    }

    public GameObject SelectionIndicator;
    TriMap triMap;

    public static event System.Action<Unit> OnUnitSelectionChanged;

    // Unit selection
    Unit _selectedUnit;
    public Unit SelectedUnit {
        get { return _selectedUnit; }   
        set {
            if(value != null && (triMap == null || !triMap.CanSelectUnit(value)))
            {
                return;
            }

            if (_selectedUnit == value)
            {
                return;
            }

            if (mouseController != null)
            {
                mouseController.ClearPath();
            }

            _selectedUnit = null;
            if(_selectedColony != null)
                SelectedColony = null;

            _selectedUnit = value;
            UpdateSelectionIndicator();
            OnUnitSelectionChanged?.Invoke(_selectedUnit);
        }
    }

    Colony _selectedColony;
    public Colony SelectedColony {
        get { return _selectedColony; }   
        set {
            if(_selectedColony != null)
            {
                // We already have a colony selected, make sure we cancel the old mouse mode
                //mouseController.CancelUpdateFunc();
            }

            _selectedColony = null;
            if(_selectedUnit != null)
                SelectedUnit = null;

            _selectedColony = value;
            if(_selectedColony != null)
            {
                //mouseController.StartColonyView();
            }
        }
    }

    MouseController mouseController;

	
	// Update is called once per frame
	void Update () {

        // De-select things that have been destroyed or can no longer be selected
        if(SelectedUnit != null &&
            (SelectedUnit.IsDestroyed || triMap == null || !triMap.CanSelectUnit(SelectedUnit)))
        {
            ClearSelection();
        }

        if(SelectedColony != null && SelectedColony.IsDestroyed == true)
        {
            // We are pointing to a destroyed unit.
            SelectedColony = null;
        }

        UpdateSelectionIndicator();

	}

    void UpdateSelectionIndicator()
    {
        if(SelectedUnit == null)
        {
            if (SelectionIndicator != null)
            {
                SelectionIndicator.SetActive(false);
            }
            return;
        }

        GameObject uGO = triMap != null ? triMap.GetUnitGO( SelectedUnit ) : null;
        if(uGO == null)
        {
            ClearSelection();
            return;
        }

        if (SelectionIndicator != null)
        {
            SelectionIndicator.SetActive(true);
            SelectionIndicator.transform.position = uGO.transform.position;
        }
    }

    public void SelectNextUnit( bool skipDoneUnits )
    {
        if (triMap == null) return;
        Player player = triMap.CurrentPlayer;
        if (Networking.NetworkManager.Instance != null && Networking.NetworkManager.Instance.IsClientConnected)
        {
            if (Networking.NetworkManager.Instance.LocalPlayerIndex >= 0 &&
                Networking.NetworkManager.Instance.LocalPlayerIndex < triMap.Players.Length)
            {
                player = triMap.Players[Networking.NetworkManager.Instance.LocalPlayerIndex];
            }
        }

        if (player == null || player.Type == Player.PlayerType.AI || player.Units == null || player.Units.Length == 0) return;

        Unit[] units = player.Units;

        int currentIndex = -1;

        if(SelectedUnit != null)
        {
            for (int i = 0; i < units.Length; i++)
            {
                if(SelectedUnit == units[i])
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        for (int i = 0; i < units.Length; i++)
        {
            int tryIndex = (currentIndex + i + 1) % units.Length;

            if ( skipDoneUnits == true && units[tryIndex].UnitWaitingForOrders() == false )
            {
                // Skip this unit
                continue;
            }

            // We only get here if we're on a valid pick
            SelectedUnit = units[ tryIndex ];
            if (SelectedUnit != null && SelectedUnit.Tri != null && player.Type != Player.PlayerType.AI)
            {
                Camera.main?.GetComponent<CameraMotion>()?.PanToTri(SelectedUnit.Tri, onlyIfOffscreen: true);
            }
            return;
        }

        // if we got here, selection did not change
        // If the pre-existing unit is done, and we're suppposed to skip that,
        // then clear the selection.
        if(SelectedUnit != null && SelectedUnit.UnitWaitingForOrders() == false && skipDoneUnits == true)
        {
            SelectedUnit = null;
        }
    }
}
