using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using QPath;
using System.Linq;

public class Unit : MapObject, IQPathUnit {

    public Unit()
    {
        Name = "Ship";
    }

    public int Strenth = 8;
    public int Movement = 2;
    public int MovementRemaining = 2;
    public bool CanBuildCities = false;
    public bool SkipThisUnit = false;
    public StarshipType ShipType { get; set; }

    /// <summary>
    /// List of tris to walk through (from pathfinder).
    /// NOTE: First item is always the tri we are standing in.
    /// </summary>
    List<Tri> triPath;
    public System.Collections.Generic.IReadOnlyList<Tri> TriPath => triPath;

    // TODO: This should probably be moved to some kind of central option/config file
    private bool MovementRulesLikeCiv6 => true;

    public void DUMMY_PATHING_FUNCTION()
    {

        /*QPath.CostEstimateDelegate ced = (IQPathTile a, IQPathTile b) => (
            return Tri.Distance(a, b);
        );*/

        Tri[] pathTris = QPath.QPath.FindPath<Tri>( 
            Tri.TriMap, 
            this,
            Tri, 
            Tri.TriMap.GetTriAt( Tri.Q + 6, Tri.R ), 
            Tri.CostEstimate 
        );
            
        Debug.Log("Got pathfinding path of length: " + pathTris.Length);

        SetTriPath(pathTris);
    }

    public void ClearTriPath()
    {
        SkipThisUnit = false;
        this.triPath = new List<Tri>();
    }

    public void SetTriPath( Tri[] triArray )
    {
        SkipThisUnit = false;
        this.triPath = new List<Tri>( triArray );
    }

    public Tri[] GetTriPath()
    {
        return (this.triPath == null ) ? null : this.triPath.ToArray();
    }

    public int GetTriPathLength()
    {
        return this.triPath.Count;
    }

    public bool UnitWaitingForOrders()
    {
        if(SkipThisUnit)
        {
            return false;
        }

        // Returns true if we have movement left but nothing queued
        if( 
            MovementRemaining > 0 && 
            (triPath==null || triPath.Count==0) 
            // TODO: Maybe we've been told to Fortify/Alert/SkipTurn
        )
        {
            return true;
        }

        return false;
    }

    public void RefreshMovement()
    {
        SkipThisUnit = false;
        MovementRemaining = Movement;
    }

    public void SetMovementAllowance(int movementPoints)
    {
        if (movementPoints < 0)
        {
            throw new System.ArgumentOutOfRangeException("movementPoints");
        }

        Movement = movementPoints;
        MovementRemaining = movementPoints;
        SkipThisUnit = false;
    }

    /// <summary>
    /// Processes one tile worth of movement for the unit
    /// </summary>
    /// <returns>Returns true if this should be called immediately again.</returns>
    public bool DoMove()
    {
        // Do queued move

        if(MovementRemaining <= 0)
            return false;

        if(triPath == null || triPath.Count == 0)
        {
            return false;
        }

        // Grab the first tri from our queue
        Tri triWeAreLeaving = triPath[0];
        Tri newTri = triPath[1];

        int costToEnter = MovementCostToEnterTri( newTri );

        if( costToEnter > MovementRemaining && MovementRemaining < Movement && MovementRulesLikeCiv6 )
        {
            // We can't enter the tri this turn
            return false;
        }

        triPath.RemoveAt(0);

        if( triPath.Count == 1 )
        {
            // The only tri left in the list, is the one we are moving to now,
            // therefore we have no more path to follow, so let's just clear
            // the queue completely to avoid confusion.
            triPath = null;
        }

        // Move to the new Tri
        SetTri( newTri );
        MovementRemaining = Mathf.Max(MovementRemaining-costToEnter, 0);

        return triPath != null && MovementRemaining > 0;
    }

    public int MovementCostToEnterTri( Tri tri )
    {
        // TODO:  Implement different movement traits

        return tri.BaseMovementCost( false, false, false );
    }

    public float AggregateTurnsToEnterTri( Tri tri, float turnsToDate )
    {
        // The issue at hand is that if you are trying to enter a tile
        // with a movement cost greater than your current remaining movement
        // points, this will either result in a cheaper-than expected
        // turn cost (Civ5) or a more-expensive-than expected turn cost (Civ6)

        float baseTurnsToEnterTri = MovementCostToEnterTri(tri) / Movement; // Example: Entering a forest is "1" turn

        if(baseTurnsToEnterTri < 0)
        {
            // Impassible terrain
            //Debug.Log("Impassible terrain at:" + tri.ToString());
            return -99999;
        }

        if(baseTurnsToEnterTri > 1)
        {
            // Even if something costs 3 to enter and we have a max move of 2, 
            // you can always enter it using a full turn of movement.
            baseTurnsToEnterTri = 1;
        }


        float turnsRemaining = MovementRemaining / Movement;    // Example, if we are at 1/2 move, then we have .5 turns left

        float turnsToDateWhole = Mathf.Floor(turnsToDate); // Example: 4.33 becomes 4
        float turnsToDateFraction = turnsToDate - turnsToDateWhole; // Example: 4.33 becomes 0.33

        if( (turnsToDateFraction > 0 && turnsToDateFraction < 0.01f) || turnsToDateFraction > 0.99f )
        {
            Debug.LogError("Looks like we've got floating-point drift: " + turnsToDate);

            if( turnsToDateFraction < 0.01f )
                turnsToDateFraction = 0;

            if( turnsToDateFraction > 0.99f )
            {
                turnsToDateWhole   += 1;
                turnsToDateFraction = 0;
            }
        }

        float turnsUsedAfterThismove = turnsToDateFraction + baseTurnsToEnterTri; // Example 0.33 + 1

        if(turnsUsedAfterThismove > 1)
        {
            // We have hit the situation where we don't actually have enough movement to complete this move.
            // What do we do?

            if( MovementRulesLikeCiv6 )
            {
                // We aren't ALLOWED to enter the tile this move. That means, we have to...

                if(turnsToDateFraction == 0)
                {
                    // We have full movement, but this isn't enough to enter the tile
                    // EXAMPLE: We have a max move of 2 but the tile costs 3 to enter.
                    // We are good to go.
                }
                else
                {
                    // We are NOT on a fresh turn -- therefore we need to 
                    // sit idle for the remainder of this turn.
                    turnsToDateWhole   += 1;
                    turnsToDateFraction = 0;
                }

                // So now we know for a fact that we are starting the move into difficult terrain
                // on a fresh turn.
                turnsUsedAfterThismove = baseTurnsToEnterTri;
            }
            else
            {
                // Civ5-style movement state that we can always enter a tile, even if we don't
                // have enough movement left.
                turnsUsedAfterThismove = 1;
            }
        }

        // turnsUsedAfterThismove is now some value from 0..1. (this includes
        // the factional part of moves from previous turns).


        // Do we return the number of turns THIS move is going to take?
        // I say no, this an an "aggregate" function, so return the total
        // turn cost of turnsToDate + turns for this move.

        return turnsToDateWhole + turnsUsedAfterThismove;

    }

    override public void SetTri( Tri newTri )
    {
        if(Tri != null)
        {
            Tri.RemoveUnit(this);
        }

        base.SetTri( newTri );

        Tri.AddUnit(this);
    }

    override public void Destroy(  )
    {
        base.Destroy(  );

        Tri.RemoveUnit(this);
    }

    /// <summary>
    /// Turn cost to enter a tri (i.e. 0.5 turns if a movement cost is 1 and we have 2 max movement)
    /// </summary>
    public float CostToEnterTri( IQPathTile sourceTile, IQPathTile destinationTile )
    {
        return 1;
    }
}
