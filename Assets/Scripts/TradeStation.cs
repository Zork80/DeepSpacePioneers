public class TradeStation : MapObject
{
    public TradeStation()
    {
        Name = "Brussels";

        EXAMPLE();
    }

    // BuildingJob buildingJob;

    public override void SetTri( Tri newTri )
    {
        if(Tri == newTri)
        {
            return;
        }

        if(Tri != null)
        {
            Tri.RemoveTradeStation(this);
        }

        base.SetTri( newTri );

        Tri.AddTradeStation( this );
    }

    public override void Destroy()
    {
        if(Tri != null)
        {
            Tri.RemoveTradeStation(this);
        }

        base.Destroy();
    }

    public void DoTurn()
    {
        // if(buildingJob != null)
        // {
        //     float workLeft = buildingJob.DoWork( productionPerTurn );
        //     if(workLeft <= 0)
        //     {
        //         // Job is complete
        //         buildingJob = null;
        //         // TODO: Save overflow
        //     }
        // }
    }

    // public BuildingBlueprint[] GetPossibleBuildings()
    // {
    //     // TODO: Apply tech/uniqueness filters
    //     return BuildingDatabase.GetListOfBuilding();
    // }

    void EXAMPLE()
    {
        // buildingJob = new BuildingJob(
        //     null, 
        //     "Dwarf Warrior",
        //     100,
        //     0,
        //     () => {
        //         this.Tri.TriMap.SpawnUnitAt(
        //             new Unit(),
        //             this.Tri.TriMap.UnitDwarfPrefab,
        //             this.Tri.Q,
        //             this.Tri.R
        //         );
        //     },
        //     null
        // );
    }

}
