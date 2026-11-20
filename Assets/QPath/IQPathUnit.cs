using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace QPath
{
    
    public interface IQPathUnit {

        float CostToEnterTri( IQPathTile sourceTile, IQPathTile destinationTile );

    }
}

