public class MapObject
{
    public MapObject()
    {
    }

    public string Name;
    public int HitPoints = 100;
    public bool CanBeAttacked = true;
    public int FactionID = 0;

    public bool IsDestroyed { get; private set; }

    public Tri Tri  { get; protected set; }

    public delegate void ObjectMovedDelegate ( Tri oldTri, Tri newTri );
    public event ObjectMovedDelegate OnObjectMoved;

    public delegate void ObjectDestroyedDelegate ( MapObject mo );
    public event ObjectDestroyedDelegate OnObjectDestroyed;

    /// <summary>
    /// This object is being removed from the map/game
    /// </summary>
    public virtual void Destroy()
    {
        IsDestroyed = true;

        if(OnObjectDestroyed != null)
        {
            OnObjectDestroyed( this );
        }
        OnObjectMoved = null;
        OnObjectDestroyed = null;
    }

    public virtual void SetTri( Tri newTri )
    {
        Tri oldTri = Tri;

        Tri = newTri;

        if(OnObjectMoved != null)
        {
            OnObjectMoved(oldTri, newTri);
        }
    }

}


