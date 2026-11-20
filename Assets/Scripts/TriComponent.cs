using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TriComponent : MonoBehaviour
{
    public Tri Tri;
    public TriMap TriMap;

    public void UpdatePosition()
    {
        transform.position = Tri.PositionFromCamera(
            Camera.main.transform.position, 
            TriMap.NumRows,
            TriMap.NumColumns
            );
    }
}
