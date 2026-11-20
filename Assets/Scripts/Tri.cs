using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using QPath;

///
/// The Tri class defines the grid postion, world postion, size,
/// _neighbours, etc.. of a Tri tile, it dos not interact
/// with Unity directly in any way.
///
public class Tri : IQPathTile
{
    public Tri(TriMap triMap, int q, int r)
    {
        TriMap = triMap;
        Q = q;
        R = r;
        _s = -(q + r);    
    }
    
    public readonly TriMap TriMap;

    // Q + R + S = 0
    // S = -(Q + R)
    public readonly int Q; // Column
    public readonly int R; // Row
    private readonly int _s;

    private const float WidthMultiplier = 0.8660254f;
    private const float HeightMultiplier = 1.5f;

    float _radius = 1f;

    public bool IsUpsideDown{ get; set; }

    HashSet<Unit> units;
    public Unit[] Units
    { 
        get
        {
            if(units == null)
                units = new HashSet<Unit>();
            return units.ToArray();
        }
    }

    public Colony Colony { get; protected set; }

    private readonly HashSet<Tri> adjacentPlanetSystems = new HashSet<Tri>();
    public Tri[] AdjacentPlanetSystems
    {
        get { return adjacentPlanetSystems.ToArray(); }
    }

    public bool IsAdjacentToPlanetSystem(Tri planetSystem)
    {
        return adjacentPlanetSystems.Contains(planetSystem);
    }

    public void RegisterAdjacentPlanetSystem(Tri planetSystem)
    {
        if (planetSystem == null)
        {
            throw new ArgumentNullException("planetSystem");
        }
        if (planetSystem.SystemKind != SystemKind.Planets)
        {
            throw new ArgumentException(
                "Only planet systems can be registered as adjacent.", "planetSystem");
        }

        adjacentPlanetSystems.Add(planetSystem);
    }

    public Tri[] GetAllNeighbours()
    {
        var neighbours = new HashSet<Tri>(GetNeighbours().Cast<Tri>());

        for (int q = Mathf.Max(0, Q - 1); q <= Mathf.Min(TriMap.NumColumns - 1, Q + 1); q++)
        {
            for (int r = Mathf.Max(0, R - 1); r <= Mathf.Min(TriMap.NumRows - 1, R + 1); r++)
            {
                Tri candidate = TriMap.GetTriAt(q, r);
                if (candidate != this && candidate.GetNeighbours().Contains(this))
                {
                    neighbours.Add(candidate);
                }
            }
        }

        neighbours.Remove(this);
        return neighbours.ToArray();
    }

    public Tri[] GetPlanetSystemAdjacentIntersections()
    {
        if (SystemKind != SystemKind.Planets)
        {
            return new Tri[0];
        }

        var discoveryIntersections = new List<Tri>();

        int[][] offsets;
        if (!IsUpsideDown)
        {
            // Upright planetary system: single planet at bottom (R - 1), two planets at top (R + 1)
            offsets = new int[][]
            {
                new int[] { -1, -1 }, // Planet 3 (bottom) outer left
                new int[] {  0, -1 }, // Planet 3 (bottom) outer bottom
                new int[] {  1, -1 }, // Planet 3 (bottom) outer right
                new int[] { -2,  0 }, // Planet 1 (top-left) outer left
                new int[] {  2,  0 }, // Planet 2 (top-right) outer right
                new int[] { -2,  1 }, // Planet 1 (top-left) outer top-left
                new int[] { -1,  1 }, // Planet 1 (top-left) outer top-right
                new int[] {  1,  1 }, // Planet 2 (top-right) outer top-left
                new int[] {  2,  1 }  // Planet 2 (top-right) outer top-right
            };
        }
        else
        {
            // Inverted planetary system: single planet at top (R + 1), two planets at bottom (R - 1)
            offsets = new int[][]
            {
                new int[] { -1,  1 }, // Planet 3 (top) outer left
                new int[] {  0,  1 }, // Planet 3 (top) outer top
                new int[] {  1,  1 }, // Planet 3 (top) outer right
                new int[] { -2,  0 }, // Planet 1 (bottom-left) outer left
                new int[] {  2,  0 }, // Planet 2 (bottom-right) outer right
                new int[] { -2, -1 }, // Planet 1 (bottom-left) outer bottom-left
                new int[] { -1, -1 }, // Planet 1 (bottom-left) outer bottom-right
                new int[] {  1, -1 }, // Planet 2 (bottom-right) outer bottom-left
                new int[] {  2, -1 }  // Planet 2 (bottom-right) outer bottom-right
            };
        }

        foreach (var offset in offsets)
        {
            int targetQ = Q + offset[0];
            int targetR = R + offset[1];

            if (!TriMap.AllowWrapEastWest && (targetQ < 0 || targetQ >= TriMap.NumColumns))
            {
                continue;
            }

            if (!TriMap.AllowWrapNorthSouth && (targetR < 0 || targetR >= TriMap.NumRows))
            {
                continue;
            }

            Tri candidate = TriMap.GetTriAt(targetQ, targetR);
            if (candidate != null && !discoveryIntersections.Contains(candidate))
            {
                discoveryIntersections.Add(candidate);
            }
        }

        return discoveryIntersections.ToArray();
    }

    public override string ToString()
    {
        return Q + ", " + R;
    }

    public TradeStation[] TradeStations = new TradeStation[5];

    /// Returns the world space position of the tri
    public Vector3 Position()
    {
        float vert = TriHeight() * HeightMultiplier;
        float horiz = TriWidth();

        return new Vector3(
            Q * horiz,
            0,
            R * vert
        );
    }

    //float triPointOffset = 0.33f;
    float triPointOffset = 0.25f;

    public Vector3 PositionPoint()
    {
        var posPoint = Position();

        if(IsUpsideDown)
            return new Vector3(
                posPoint.x,
                posPoint.y,
                posPoint.z + TriHeight() * (triPointOffset)
            );
        else
            return new Vector3(
                posPoint.x,
                posPoint.y,
                posPoint.z + TriHeight() * (1 - triPointOffset)
            );
    }

    /// <summary>
    /// Returns the central world space position of the triangle tile / navigation junction point.
    /// </summary>
    public Vector3 CenterPosition()
    {
        return PositionPoint() + new Vector3(0f, 0f, -0.5f);
    }

    public float TriHeight()
    {
        return _radius;
    }

    public float TriWidth()
    {
        return WidthMultiplier * TriHeight();
    }

    public float TriVerticalSpacing()
    {
        return TriHeight() * HeightMultiplier;
    }

    public float TriHorizontalSpacing()
    {
        return TriWidth();
    }

    bool _isPassable = true;
    public bool IsPassable 
    {
        get { return _isPassable; }
        set 
        {
            if (_isPassable != value)
            {
                _isPassable = value;
                var lineRenderers = TriMap.GetTriGO(this).transform.GetComponentsInChildren<LineRenderer>();
                foreach(var lineRenderer in lineRenderers)
                {
                    lineRenderer.enabled = _isPassable;
                }
            }
        }
    }

    SystemTypes _systemType;
    public SystemTypes SystemType
    {
        get { return _systemType; }
        set 
        {
             _systemType = value; 
            SetSystemText();
        }
    }

    SystemKind _systemKind;
    public SystemKind SystemKind
    {
        get { return _systemKind; }
        set 
        {
             _systemKind = value; 
             SetSystemText();
        }
    }

    AlienCivilization _alienCivilization = AlienCivilization.None;
    public AlienCivilization AlienCivilization
    {
        get { return _alienCivilization; }
        set { _alienCivilization = value; }
    }

    bool _isColonyPoint;
    public bool IsColonyPoint
    {
        get { return _isColonyPoint; }
        set { _isColonyPoint = value; }
    }
    public bool IsColonySite
    {
        get { return _isColonyPoint; }
        set { _isColonyPoint = value; }
    }

    bool _isTradePoint;
    public bool IsTradePoint
    {
        get { return _isTradePoint; }
        set { _isTradePoint = value; }
    }
    public bool IsDockingPoint
    {
        get { return _isTradePoint; }
        set { _isTradePoint = value; }
    }

    private ResourceType[] _planetResources = new ResourceType[0];
    public ResourceType[] PlanetResources
    {
        get { return (ResourceType[])_planetResources.Clone(); }
    }

    private int[] _planetProductionNumbers = new int[0];
    public int[] PlanetProductionNumbers
    {
        get { return (int[])_planetProductionNumbers.Clone(); }
    }
    public bool ArePlanetProductionNumbersRevealed { get; private set; }

    private PlanetSpecialType[] _planetSpecialTypes = new[] { PlanetSpecialType.Normal, PlanetSpecialType.Normal, PlanetSpecialType.Normal };
    public PlanetSpecialType[] PlanetSpecialTypes => (PlanetSpecialType[])_planetSpecialTypes.Clone();

    private int[] _planetSpecialRequirements = new int[3];
    public int[] PlanetSpecialRequirements => (int[])_planetSpecialRequirements.Clone();

    private bool[] _planetSpecialResolved = new bool[3];
    public bool[] PlanetSpecialResolved => (bool[])_planetSpecialResolved.Clone();

    public void SetPlanetResources(ResourceType planet1, ResourceType planet2, ResourceType planet3)
    {
        _planetResources = new[] { planet1, planet2, planet3 };
    }

    public void SetPlanetProductionNumbers(int planet1, int planet2, int planet3)
    {
        int[] numbers = { planet1, planet2, planet3 };
        for (int i = 0; i < numbers.Length; i++)
        {
            if (numbers[i] < 2 || numbers[i] > 12 || numbers[i] == 7)
            {
                throw new ArgumentOutOfRangeException(
                    "numbers",
                    "Planet production numbers must be between 2 and 12, excluding 7.");
            }
        }

        _planetProductionNumbers = numbers;
    }

    public void SetPlanetSpecialType(int planetIndex, PlanetSpecialType type, int requirement)
    {
        if (planetIndex < 0 || planetIndex >= 3) return;
        _planetSpecialTypes[planetIndex] = type;
        _planetSpecialRequirements[planetIndex] = requirement;
        _planetSpecialResolved[planetIndex] = (type == PlanetSpecialType.Normal);
    }

    public PlanetSpecialType GetPlanetSpecialType(int planetIndex)
    {
        if (planetIndex < 0 || planetIndex >= _planetSpecialTypes.Length) return PlanetSpecialType.Normal;
        return _planetSpecialTypes[planetIndex];
    }

    public int GetPlanetSpecialRequirement(int planetIndex)
    {
        if (planetIndex < 0 || planetIndex >= _planetSpecialRequirements.Length) return 0;
        return _planetSpecialRequirements[planetIndex];
    }

    public bool IsPlanetSpecialResolved(int planetIndex)
    {
        if (planetIndex < 0 || planetIndex >= _planetSpecialResolved.Length) return true;
        return _planetSpecialResolved[planetIndex];
    }

    public bool IsIcePlanet(int planetIndex)
    {
        return GetPlanetSpecialType(planetIndex) == PlanetSpecialType.IcePlanet;
    }

    public bool IsMarauderBase(int planetIndex)
    {
        return GetPlanetSpecialType(planetIndex) == PlanetSpecialType.MarauderBase;
    }

    public bool HasUnresolvedHazard(int planetIndex)
    {
        if (planetIndex < 0 || planetIndex >= _planetSpecialTypes.Length) return false;
        return _planetSpecialTypes[planetIndex] != PlanetSpecialType.Normal && !_planetSpecialResolved[planetIndex];
    }

    public void ResolveSpecialPlanet(int planetIndex, int newProductionNumber)
    {
        if (planetIndex < 0 || planetIndex >= 3) return;
        if (newProductionNumber < 2 || newProductionNumber > 12 || newProductionNumber == 7)
        {
            throw new ArgumentOutOfRangeException("newProductionNumber", "Production number must be between 2 and 12, excluding 7.");
        }
        _planetSpecialResolved[planetIndex] = true;
        if (_planetProductionNumbers.Length >= 3)
        {
            _planetProductionNumbers[planetIndex] = newProductionNumber;
        }
    }

    public int[] GetAdjacentPlanetIndicesForIntersection(Tri intersection)
    {
        if (intersection == null || SystemKind != SystemKind.Planets) return new int[0];
        int dQ = intersection.Q - this.Q;
        int dR = intersection.R - this.R;

        if (TriMap != null)
        {
            if (TriMap.AllowWrapEastWest)
            {
                if (dQ > TriMap.NumColumns / 2) dQ -= TriMap.NumColumns;
                else if (dQ < -TriMap.NumColumns / 2) dQ += TriMap.NumColumns;
            }
            if (TriMap.AllowWrapNorthSouth)
            {
                if (dR > TriMap.NumRows / 2) dR -= TriMap.NumRows;
                else if (dR < -TriMap.NumRows / 2) dR += TriMap.NumRows;
            }
        }

        if (!IsUpsideDown)
        {
            // Upright planetary system:
            // Discovery intersections (each adjacent to 1 planet):
            // Planet 0 (top-left): offsets (-2, 0), (-2, 1), (-1, 1)
            if ((dQ == -2 && dR == 0) || (dQ == -2 && dR == 1) || (dQ == -1 && dR == 1)) return new[] { 0 };
            // Planet 1 (top-right): offsets (2, 0), (1, 1), (2, 1)
            if ((dQ == 2 && dR == 0) || (dQ == 1 && dR == 1) || (dQ == 2 && dR == 1)) return new[] { 1 };
            // Planet 2 (bottom): offsets (-1, -1), (0, -1), (1, -1)
            if ((dQ == -1 && dR == -1) || (dQ == 0 && dR == -1) || (dQ == 1 && dR == -1)) return new[] { 2 };

            // Colony sites (each adjacent to 2 planets):
            // Top colony site: between Planet 0 (top-left) and Planet 1 (top-right)
            if (dQ == 0 && dR == 1) return new[] { 0, 1 };
            // Left colony site: between Planet 0 (top-left) and Planet 2 (bottom)
            if (dQ == -1 && dR == 0) return new[] { 0, 2 };
            // Right colony site: between Planet 1 (top-right) and Planet 2 (bottom)
            if (dQ == 1 && dR == 0) return new[] { 1, 2 };
        }
        else
        {
            // Inverted planetary system:
            // Discovery intersections (each adjacent to 1 planet):
            // Planet 0 (bottom-left): offsets (-2, 0), (-2, -1), (-1, -1)
            if ((dQ == -2 && dR == 0) || (dQ == -2 && dR == -1) || (dQ == -1 && dR == -1)) return new[] { 0 };
            // Planet 1 (bottom-right): offsets (2, 0), (1, -1), (2, -1)
            if ((dQ == 2 && dR == 0) || (dQ == 1 && dR == -1) || (dQ == 2 && dR == -1)) return new[] { 1 };
            // Planet 2 (top): offsets (-1, 1), (0, 1), (1, 1)
            if ((dQ == -1 && dR == 1) || (dQ == 0 && dR == 1) || (dQ == 1 && dR == 1)) return new[] { 2 };

            // Colony sites (each adjacent to 2 planets):
            // Bottom colony site: between Planet 0 (bottom-left) and Planet 1 (bottom-right)
            if (dQ == 0 && dR == -1) return new[] { 0, 1 };
            // Left colony site: between Planet 0 (bottom-left) and Planet 2 (top)
            if (dQ == -1 && dR == 0) return new[] { 0, 2 };
            // Right colony site: between Planet 1 (bottom-right) and Planet 2 (top)
            if (dQ == 1 && dR == 0) return new[] { 1, 2 };
        }

        return new int[0];
    }

    public int GetAdjacentPlanetIndexForIntersection(Tri intersection)
    {
        int[] indices = GetAdjacentPlanetIndicesForIntersection(intersection);
        if (indices.Length == 0) return -1;
        foreach (int idx in indices)
        {
            if (HasUnresolvedHazard(idx))
            {
                return idx;
            }
        }
        return indices[0];
    }

    public bool IsIntersectionBlockedByHazard(Tri intersection)
    {
        int[] indices = GetAdjacentPlanetIndicesForIntersection(intersection);
        foreach (int idx in indices)
        {
            if (HasUnresolvedHazard(idx))
            {
                return true;
            }
        }
        return false;
    }

    public void RevealPlanetProductionNumbers()
    {
        if (_planetProductionNumbers.Length == 0)
        {
            return;
        }

        ArePlanetProductionNumbersRevealed = true;
    }

    public void SetSystemText()
    {
        var systemLabel = GetLabelByName("SystemLabel");
        if (systemLabel == null) return;

        bool showDebug = (TriMap != null && TriMap.DebugSettings != null && TriMap.DebugSettings.ShowSystemDebugLabels)
            || (GameDebugSettings.Active != null && GameDebugSettings.Active.ShowSystemDebugLabels);

        systemLabel.gameObject.SetActive(showDebug);
        if (!showDebug)
        {
            systemLabel.text = string.Empty;
            return;
        }

        switch(SystemType)
        {
            case SystemTypes.Alpha:
                systemLabel.text = "ɑ";
                break;
            case SystemTypes.Beta:
                systemLabel.text = "β";
                break;
            case SystemTypes.Gamma:
                systemLabel.text = "ɣ";
                break;
            case SystemTypes.Delta:
                systemLabel.text = "δ";
                break;
            case SystemTypes.OneStar:
                systemLabel.text = "*";
                break;
            case SystemTypes.TwoStars:
                systemLabel.text = "* *";
                break;
            default:
                systemLabel.text = string.Empty;
                break;
        }

        switch(SystemKind)
        {
            case SystemKind.Empty:
            case SystemKind.Planets:
            case SystemKind.Trade:
                break;
        }
    }

    public TextMesh GetLabelByName(string name)
    {
        var triGO = TriMap.GetTriGO(this);
        TextMesh[] ts = triGO.transform.GetComponentsInChildren<TextMesh>();
        foreach (TextMesh t in ts) if (t.name == name) return t;
        return null;
    }

    public Vector3 PositionFromCamera()
    {
        return TriMap.GetTriPosition(this);
    }

    public Vector3 PositionFromCamera(Vector3 cameraPosition, float numRows, float numColumns)
    {
        float mapHeight = numRows * TriVerticalSpacing();
        float mapWidth = numColumns * TriHorizontalSpacing();

        var position = Position();


        if(TriMap.AllowWrapEastWest)
        {
            float howManyWidthsFromCamera = (position.x - cameraPosition.x) / mapWidth;

            if(howManyWidthsFromCamera > 0)
                howManyWidthsFromCamera += 0.5f;
            else
                howManyWidthsFromCamera -= 0.5f;

            int howManyWidthToFix = (int)howManyWidthsFromCamera;

            position.x -= howManyWidthToFix * mapWidth;
        }

        if(TriMap.AllowWrapNorthSouth)
        {
            float howManyHeightsFromCamera = (position.z - cameraPosition.z) / mapHeight;

            if(howManyHeightsFromCamera > 0)
                howManyHeightsFromCamera += 0.5f;
            else   
                howManyHeightsFromCamera -= 0.5f;

            int howManyHeightsToFix = (int) howManyHeightsFromCamera;

            position.z = howManyHeightsToFix * mapHeight;
        }

        return position;
    }


    public static float Distance(Tri a, Tri b)
    {
        // WARNING: Probably Wrong for wrapping
        int dQ = Mathf.Abs(a.Q - b.Q);
        if(a.TriMap.AllowWrapEastWest)
        {
            if(dQ > a.TriMap.NumColumns / 2)
                dQ = a.TriMap.NumColumns - dQ;
        }

        int dR = Mathf.Abs(a.R - b.R);
        if(a.TriMap.AllowWrapNorthSouth)
        {
            if(dR > a.TriMap.NumRows / 2)
                dR = a.TriMap.NumRows - dR;
        }

        return 
            Mathf.Max( 
                dQ,
                dR,
                Mathf.Abs(a._s - b._s)
            );

        // return 
        //     Mathf.Max(
        //         Mathf.Abs(a.Q - b.Q),
        //         Mathf.Abs(a.R - b.R),
        //         Mathf.Abs(a.S - b.S)
        //     );
    }


    public void AddUnit( Unit unit )
    {
        if(units == null)
        {
            units = new HashSet<Unit>();
        }

        units.Add(unit);
    }

    public void RemoveUnit( Unit unit )
    {
        if(units != null)
        {
            units.Remove(unit);
        }
    }

    public void AddColony( Colony colony )
    {
        if(Colony != null)
        {
            throw new UnityException("Trying to add a colony to a tri that already has one!");
        }
        if(GetNeighbours().Where(x=>((Tri)x).SystemKind != SystemKind.Planets).Count() == 0)
        {
            throw new UnityException("Trying to add a colony to a tri is not beneath a system!");
        }

        Colony = colony;
    }

    public void AddTradeStation( TradeStation tradeStation )
    {
        if(tradeStation == null)
        {
            throw new UnityException("Cannot add a null trade station!");
        }
        if(SystemKind != SystemKind.Trade)
        {
            throw new UnityException("Trying to add a trade station to a tri that is not a trade outpost!");
        }
        if(Array.IndexOf(TradeStations, tradeStation) >= 0)
        {
            return;
        }

        int i = Array.IndexOf(TradeStations, null);
        if(i < 0)
        {
            throw new UnityException("Trying to add a trade station to a tri where all trade ports are full!");
        }

        TradeStations[i] = tradeStation;
    }

    public int GetTradeStationSlot(TradeStation tradeStation)
    {
        return Array.IndexOf(TradeStations, tradeStation);
    }

    public void RemoveTradeStation(TradeStation tradeStation)
    {
        int slot = GetTradeStationSlot(tradeStation);
        if(slot >= 0)
        {
            TradeStations[slot] = null;
        }
    }

    public void RemoveColony( Colony colony )
    {
        if(Colony == null)
        {
            Debug.LogError("Trying to remove a colony where there isn't one!");
            return;
        }

        if(Colony != colony)
        {
            Debug.LogError("Trying to remove a colony that isn't ours!");
            return;
        }

        Colony = null;
    }

    /// <summary>
    /// Returns the most common movement cost for this tile, for a typical melee unit
    /// </summary>
    public int BaseMovementCost( bool isHillWalker, bool isForestWalker, bool isFlyer )
    {
        if(!IsPassable)
            return -99;

        int moveCost = 1;

        return moveCost;
    }

    public static float CostEstimate(IQPathTile aa, IQPathTile bb)
    {
        return Distance((Tri)aa, (Tri) bb);
    }


    Tri[] _neighbours;

    #region IQPathTile implementation
    public IQPathTile[] GetNeighbours()
    {
        if(_neighbours != null)
            return _neighbours;
        
        List<Tri> neighboursTmp = new List<Tri>();

        if(Q - 1 >= 0)
        {
            neighboursTmp.Add( TriMap.GetTriAt( Q -  1,  R +  0 ) );
        }
        if(Q + 1 < TriMap.NumColumns)
        {
            neighboursTmp.Add( TriMap.GetTriAt( Q +  1,  R +  0 ) );
        }
        if(IsUpsideDown)
        {
            if(R - 1 >= 0)
                neighboursTmp.Add( TriMap.GetTriAt( Q +  0,  R - 1 ) );
        }
        else if(R + 1 < TriMap.NumRows)
                neighboursTmp.Add( TriMap.GetTriAt( Q +  0,  R + 1 ) );


        List<Tri> neighboursTmp2 = new List<Tri>();

        foreach(Tri t in neighboursTmp)
        {
            if(t != null)
            {
                neighboursTmp2.Add(t);
            }
        }

        _neighbours = neighboursTmp2.ToArray();

        return _neighbours;
    }

    public float AggregateCostToEnter(float costSoFar, IQPathTile sourceTile, IQPathUnit theUnit)
    {
        // TODO: We are ignoring source tile right now, this will have to change when
        // we have rivers.
        return ((Unit)theUnit).AggregateTurnsToEnterTri(this, costSoFar);
    }
    #endregion

    public void DrawLines()
    {
        if(IsPassable)
        {
            LineRenderer[] lineRenderers = TriMap.GetTriGO(this).transform.GetComponentsInChildren<LineRenderer>();
            foreach(var lineRenderer in lineRenderers)
            {
                lineRenderer.enabled = false;
            }

            int i = 0;
            foreach(var neighbour in GetNeighbours())
            {
                if(((Tri)neighbour).IsPassable)
                {
                    Tri[] triPath = new Tri[2];
                    triPath[0] = this;
                    triPath[1] = (Tri)neighbour;
                    lineRenderers[i].enabled = true;
                    DrawPath(triPath, lineRenderers[i]);
                }
                i++;
            }
        }
    }

    void DrawPath(Tri[] triPath, LineRenderer lineRenderer)
    {
        if(triPath == null || triPath.Length == 0)
        {
            lineRenderer.enabled = false;
            return;
        }
        lineRenderer.enabled = true;

        Vector3[] ps = new Vector3[ triPath.Length ];
        for (int i = 0; i < triPath.Length; i++)
        {
            var tri = triPath[i];
            ps[i] = tri.CenterPosition() + (Vector3.up * 0.02f);
        }

        lineRenderer.positionCount = ps.Length;
        lineRenderer.SetPositions(ps);
    }
}
