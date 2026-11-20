using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriMapSF : TriMap
{
    public override void GenerateMap()
    {
        base.GenerateMap();

        // Tri tri;
        // tri = GetTriAt(NumColumns - 1, 0);
        // tri.IsPassable = false;
        // tri = GetTriAt(NumColumns - 1, 1);
        // tri.IsPassable = false;

        Tri tri;
        for (int c = 0; c < NumColumns; c++)
        {
            tri = GetTriAt(c, 0);
            if (tri != null)
            {
                tri.IsPassable = false;
            }
        }

        for (int r = 0; r < NumRows; r++)
        {
            tri = GetTriAt(NumColumns - 1, r);
            if (tri != null)
            {
                tri.IsPassable = false;
            }
        }

        for (int c = 0; c < 25; c++)
        {
            tri = GetTriAt(c, 1);
            if (tri != null)
            {
                tri.IsPassable = false;
            }
            tri = GetTriAt(c, NumRows - 1);
            if (tri != null)
            {
                tri.IsPassable = false;
            }
        }

        tri = GetTriAt(NumColumns - 1, 1);
        if (tri != null)
        {
            tri.IsPassable = false;
        }
        tri = GetTriAt(NumColumns - 2, 1);
        if (tri != null)
        {
            tri.IsPassable = false;
        }
        tri = GetTriAt(NumColumns - 1, NumRows -1);
        if (tri != null)
        {
            tri.IsPassable = false;
        }
        tri = GetTriAt(NumColumns - 2, NumRows -1);
        if (tri != null)
        {
            tri.IsPassable = false;
        }

        tri = GetTriAt(0, 1);
        if (tri != null)
        {
            tri.IsPassable = false;
        }
        tri = GetTriAt(0, NumRows -1);
        if (tri != null)
        {
            tri.IsPassable = false;            
        }

        // Space for outpost trade stations
        ConfigureTradeOutpost(GetTriAt(3, 2), AlienCivilization.Trader);
        ConfigureTradeOutpost(GetTriAt(15, 2), AlienCivilization.Scientist);
        ConfigureTradeOutpost(GetTriAt(7, NumRows - 2), AlienCivilization.Diplomat);
        ConfigureTradeOutpost(GetTriAt(17, NumRows - 2), AlienCivilization.Cultivator);

        CreateTradeOutpostVisuals();
        UpdateTriVisuals();

        Tri.Distance(this.tris[5,5], this.tris[6,6]);

        var test = GetTriAt(0, 11).GetNeighbours();
        foreach(var t in test)
        {

        }       

        CaptureBoardModel(GameBoardVariant.Legacy);

        // Draw static grid lines ONCE upon board generation
        if (tris != null)
        {
            foreach (var t in tris)
            {
                if (t != null)
                {
                    t.DrawLines();
                }
            }
        }
    }

    private static void ConfigureTradeOutpost(Tri tri, AlienCivilization civilization = AlienCivilization.None)
    {
        if (tri == null)
        {
            return;
        }

        tri.SystemKind = SystemKind.Trade;
        tri.IsTradePoint = true;
        tri.IsPassable = true;
        tri.AlienCivilization = civilization;
    }

    protected override void Update() {
        base.Update();
    }
}
