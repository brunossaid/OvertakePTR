using UnityEngine;

public class ChunksManager : MonoBehaviour
{
    public Transform player;           // una sola referencia al player
    public float length = 30f;         // largo de cada chunk en z
    public int totalTilesOverride = 0; // 0 = usa cantidad de hijos con chunktile

    void Awake()
    {
        var tiles = GetComponentsInChildren<ChunkTile>(includeInactive: true);
        int total = (totalTilesOverride > 0) ? totalTilesOverride : tiles.Length;

        foreach (var t in tiles)
        {
            t.player = player;
            t.length = length;
            t.totalTiles = total;
        }
    }
}
