using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class TriMapSFnew : TriMap
{
    [Tooltip("Legacy board seed field. Controlled centrally by DebugSettings (UseFixedBoardSeed / FixedBoardSeed).")]
    public int BoardSeed = -1;

    public override void GenerateMap()
    {
        if (BoardSeed >= 0 && DebugSettings != null && DebugSettings.FixedBoardSeed == 20261004 && BoardSeed != 20261004)
        {
            DebugSettings.FixedBoardSeed = BoardSeed;
        }

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
        tri = GetTriAt(0, NumRows -1);
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

        var placeHolderSpaces = new List<Tuple<int, int, SystemTypes>>();
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(28, 2, SystemTypes.Alpha));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(28, 5, SystemTypes.Beta));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(28, 8, SystemTypes.Gamma));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(28, 11, SystemTypes.Delta));        

        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(20, 3, SystemTypes.OneStar));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(21, 6, SystemTypes.OneStar));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(20, 10, SystemTypes.OneStar));

        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(16, 4, SystemTypes.OneStar));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(17, 7, SystemTypes.OneStar));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(15, 10, SystemTypes.OneStar));

        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(12, 3, SystemTypes.OneStar));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(11, 7, SystemTypes.TwoStars));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(10, 10, SystemTypes.OneStar));

        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(7, 3, SystemTypes.TwoStars));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(7, 6, SystemTypes.TwoStars));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(6, 9, SystemTypes.TwoStars));

        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(2, 3, SystemTypes.TwoStars));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(2, 7, SystemTypes.TwoStars));
        placeHolderSpaces.Add(new Tuple<int, int, SystemTypes>(2, 10, SystemTypes.TwoStars));        


        int effectiveSeed = CurrentBoardSeed;
        BoardSeed = effectiveSeed;
        var random = new System.Random(effectiveSeed);

        // Strategic Sector Setup:
        // 1-Stern-Zone (vor dem Sternennebel, 8 Aussparungen):
        // 5 Planeten-Systeme, 2 Außenposten, 1 Leerraum (1 Leerraum kommt zurück in die Schachtel -> 5 + 2 + 1 = 8)
        List<SystemKind> sector1Kinds = new List<SystemKind>
        {
            SystemKind.Planets,
            SystemKind.Planets,
            SystemKind.Planets,
            SystemKind.Planets,
            SystemKind.Planets,
            SystemKind.Trade,
            SystemKind.Trade,
            SystemKind.Empty
        };
        sector1Kinds.Shuffle(random);

        // 2-Sterne-Zone (hinter dem Sternennebel, 7 Aussparungen):
        // 3 Planeten-Systeme, 2 Außenposten, 2 Leerräume (3 + 2 + 2 = 7)
        List<SystemKind> sector2Kinds = new List<SystemKind>
        {
            SystemKind.Planets,
            SystemKind.Planets,
            SystemKind.Planets,
            SystemKind.Trade,
            SystemKind.Trade,
            SystemKind.Empty,
            SystemKind.Empty
        };
        sector2Kinds.Shuffle(random);

        int sector1Index = 0;
        int sector2Index = 0;
        int planet1Index = 0;
        int planet2Index = 0;

        List<AlienCivilization> availableAlienCivs = new List<AlienCivilization>
        {
            AlienCivilization.Trader,
            AlienCivilization.Scientist,
            AlienCivilization.Diplomat,
            AlienCivilization.Cultivator
        };
        availableAlienCivs.Shuffle(random);
        int tradeOutpostIndex = 0;

        List<Tuple<ResourceType, ResourceType, ResourceType>> starSystem1Planets = new List<Tuple<ResourceType, ResourceType, ResourceType>>{
            new Tuple<ResourceType, ResourceType, ResourceType>(ResourceType.Carbon, ResourceType.Fuel, ResourceType.Food),
            new Tuple<ResourceType, ResourceType, ResourceType>(ResourceType.Carbon, ResourceType.TradeGoods, ResourceType.Ore),
            new Tuple<ResourceType, ResourceType, ResourceType>(ResourceType.Fuel, ResourceType.TradeGoods, ResourceType.Ore),
            new Tuple<ResourceType, ResourceType, ResourceType>(ResourceType.Fuel, ResourceType.Ore, ResourceType.Food),
            new Tuple<ResourceType, ResourceType, ResourceType>(ResourceType.Food, ResourceType.TradeGoods, ResourceType.Carbon)
        };
        starSystem1Planets.Shuffle(random);

        List<Tuple<ResourceType, ResourceType, ResourceType>> starSystem2Planets = new List<Tuple<ResourceType, ResourceType, ResourceType>>{
            new Tuple<ResourceType, ResourceType, ResourceType>(ResourceType.Carbon, ResourceType.TradeGoods, ResourceType.Fuel),
            new Tuple<ResourceType, ResourceType, ResourceType>(ResourceType.TradeGoods, ResourceType.Ore, ResourceType.Food),
            new Tuple<ResourceType, ResourceType, ResourceType>(ResourceType.Ore, ResourceType.TradeGoods, ResourceType.Carbon)
        };
        starSystem2Planets.Shuffle(random);

        List<Tuple<PlanetSpecialType, int>> sector1Hazards = new List<Tuple<PlanetSpecialType, int>>
        {
            new Tuple<PlanetSpecialType, int>(PlanetSpecialType.IcePlanet, 2),
            new Tuple<PlanetSpecialType, int>(PlanetSpecialType.MarauderBase, 4),
            new Tuple<PlanetSpecialType, int>(PlanetSpecialType.MarauderBase, 5),
            new Tuple<PlanetSpecialType, int>(PlanetSpecialType.Normal, 0),
            new Tuple<PlanetSpecialType, int>(PlanetSpecialType.Normal, 0)
        };
        sector1Hazards.Shuffle(random);

        List<Tuple<PlanetSpecialType, int>> sector2Hazards = new List<Tuple<PlanetSpecialType, int>>
        {
            new Tuple<PlanetSpecialType, int>(PlanetSpecialType.IcePlanet, 3),
            new Tuple<PlanetSpecialType, int>(PlanetSpecialType.MarauderBase, 6),
            new Tuple<PlanetSpecialType, int>(PlanetSpecialType.Normal, 0)
        };
        sector2Hazards.Shuffle(random);

        foreach(Tuple<int, int, SystemTypes> placeHolderSpace in placeHolderSpaces)
        {
            tri = GetTriAt(placeHolderSpace.Item1, placeHolderSpace.Item2);
            tri.IsPassable = false;
            tri.SystemType = placeHolderSpace.Item3;
            switch (tri.SystemType)
            {
                case SystemTypes.Alpha:
                {
                    tri.SystemKind = SystemKind.Planets;
                    SpawnStarSystemAt(tri, ResourceType.Fuel, ResourceType.Carbon, ResourceType.Food, random);
                    break;
                }
                case SystemTypes.Beta:
                {
                    tri.SystemKind = SystemKind.Planets;
                    SpawnStarSystemAt(tri, ResourceType.Fuel, ResourceType.Ore, ResourceType.TradeGoods, random);
                    break;
                }
                case SystemTypes.Gamma:
                {
                    tri.SystemKind = SystemKind.Planets;
                    SpawnStarSystemAt(tri, ResourceType.Carbon, ResourceType.Food, ResourceType.Ore, random);
                    break;
                }
                case SystemTypes.Delta:
                {
                    tri.SystemKind = SystemKind.Planets;
                    SpawnStarSystemAt(tri, ResourceType.TradeGoods, ResourceType.Fuel, ResourceType.Ore, random);
                    break;
                }
                default:
                {                
                    SystemKind kind;
                    if(tri.SystemType == SystemTypes.OneStar)
                    {
                        kind = sector1Kinds[sector1Index++];
                    }
                    else
                    {
                        kind = sector2Kinds[sector2Index++];
                    }

                    tri.SystemKind = kind;

                    if(tri.SystemKind == SystemKind.Planets)
                    {
                        Tuple<ResourceType, ResourceType, ResourceType> planets;
                        Tuple<PlanetSpecialType, int> hazard;
                        if(tri.SystemType == SystemTypes.OneStar) 
                        {
                            planets = starSystem1Planets[planet1Index];
                            hazard = sector1Hazards[planet1Index++];
                        }
                        else
                        {
                            planets = starSystem2Planets[planet2Index];
                            hazard = sector2Hazards[planet2Index++];
                        }

                        if (hazard.Item1 != PlanetSpecialType.Normal)
                        {
                            int hazardSlot = random.Next(0, 3);
                            tri.SetPlanetSpecialType(hazardSlot, hazard.Item1, hazard.Item2);
                        }

                        SpawnStarSystemAt(tri, planets.Item1, planets.Item2, planets.Item3, random);
                    }
                    if(tri.SystemKind == SystemKind.Trade)
                    {
                        tri.IsTradePoint = true;
                        tri.IsPassable = true;
                        if (tradeOutpostIndex < availableAlienCivs.Count)
                        {
                            tri.AlienCivilization = availableAlienCivs[tradeOutpostIndex++];
                        }
                    }
                    if(tri.SystemKind == SystemKind.Empty)
                    {
                        tri.IsPassable = true;
                    }

                    break;
                }
            }
        }

        foreach(Tri triTmp in tris)
        {
            if(triTmp.GetAllNeighbours().FirstOrDefault(x=>x.SystemKind == SystemKind.Planets) != null)
            {
                triTmp.IsColonyPoint = true; 
            }
        }
        var test = GetTriAt(0, 11).GetNeighbours();

        CreateTradeOutpostVisuals();
        UpdateTriVisuals();
        CaptureBoardModel(GameBoardVariant.Revised);

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

    protected override void Update() {
        base.Update();
    }
}
