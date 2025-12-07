using UnityEngine;

public class ChunkTile : MonoBehaviour
{
    [HideInInspector] public Transform player;
    [HideInInspector] public int totalTiles = 8;
    public float length = 30f;
    public float visibleMargin = -8f;

    DecorationSpawner[] spawners;
    PuddleSpawner[] puddleSpawners;

    void Awake()
    {
        spawners = GetComponentsInChildren<DecorationSpawner>(includeInactive: true);
        if (spawners != null)
            for (int i = 0; i < spawners.Length; i++)
                spawners[i].chunkLength = length;

        // charcos
        puddleSpawners = GetComponentsInChildren<PuddleSpawner>(includeInactive: true);
        if (puddleSpawners != null)
            for (int i = 0; i < puddleSpawners.Length; i++)
                puddleSpawners[i].chunkLength = length;
    }

    void LateUpdate()
    {
        if (!player) return;

        float delta = player.position.z - transform.position.z;
        float threshold = length + visibleMargin;

        if (delta >= threshold)
        {
            int steps = Mathf.Max(1, Mathf.FloorToInt(delta / length));
            transform.position += new Vector3(0f, 0f, length * totalTiles * steps);

            // decoraciones
            if (spawners != null)
                for (int i = 0; i < spawners.Length; i++)
                    spawners[i].Respawn();

            // charcos
            if (puddleSpawners != null)
                for (int i = 0; i < puddleSpawners.Length; i++)
                    puddleSpawners[i].Respawn();
        }
    }
}
