public class Colony : MapObject
{
    public bool IsSpaceport { get; private set; }

    public Colony()
    {
        Name = "Brussels";

        EXAMPLE();
    }

    public void UpgradeToSpaceport()
    {
        if (IsSpaceport)
        {
            throw new System.InvalidOperationException("This colony is already a spaceport.");
        }

        IsSpaceport = true;
    }

    // BuildingJob buildingJob;

    public override void SetTri( Tri newTri )
    {
        if(Tri != null)
        {
            // Will a city ever LEAVE a tri and enter a new one?
            Tri.RemoveColony(this);
        }

        base.SetTri( newTri );

        Tri.AddColony( this );
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
