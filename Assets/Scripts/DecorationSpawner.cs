using System;
using System.Collections.Generic;
using UnityEngine;

public class DecorationSpawner : MonoBehaviour
{
    [Serializable]
    public class Prop
    {
        public GameObject prefab;
        [Range(0f, 100f)] public float percent = 25f; // peso relativo
    }

    [Header("Chunk dimensions")]
    public float chunkLength = 30f; // = ChunkTile.length
    public float chunkWidth = 12f;  // ancho total util (izq+der)

    [Header("Route")]
    public float roadCenterOffsetX = 0f; // si la ruta no esta centrada
    public float roadHalfWidth = 2.2f;
    public float safeMargin = 0.6f;

    [Header("Global density")]
    [Range(0f, 100f)] public float globalSpawnPercent = 65f;
    public int attempts = 40;
    public int maxCount = 16;
    public float minSeparation = 1.0f;

    [Header("Rotation/Height")]
    public bool randomYRotation = true;  // solo Y para todos
    public float globalYOffset = 0f;     // ajuste global de altura

    [Header("Prefabs")]
    public List<Prop> props = new List<Prop>();

    [Header("Seed")]
    public int baseSeed = 1234;
    public bool deterministicByWorldZ = true;

    readonly List<Transform> spawned = new();
    Transform chunkTr; 

    void Awake()
    {
        var tile = GetComponentInParent<ChunkTile>();
        chunkTr = tile ? tile.transform : transform; 
    }

    public void Respawn()
    {
        Clear();
        Spawn();
    }

    void OnEnable() => Respawn();

    void Clear()
    {
        for (int i = 0; i < spawned.Count; i++)
            if (spawned[i]) Destroy(spawned[i].gameObject);
        spawned.Clear();
    }

    void Spawn()
    {
        if (props == null || props.Count == 0) return;

        // semilla del CHUNK
        if (deterministicByWorldZ)
        {
            int key = Mathf.RoundToInt(chunkTr.position.z / chunkLength);
            UnityEngine.Random.InitState(baseSeed ^ key);
        }

        // suma de pesos
        float totalPercent = 0f;
        foreach (var p in props) totalPercent += Mathf.Max(0f, p.percent);
        if (totalPercent <= 0f) return;

        int placed = 0, guard = 0;

        while (guard++ < attempts && placed < maxCount)
        {
            if (UnityEngine.Random.value * 100f > globalSpawnPercent)
                continue;

            // elegir prop por ruleta
            float r = UnityEngine.Random.value * totalPercent;
            Prop choice = props[0];
            foreach (var p in props)
            {
                r -= Mathf.Max(0f, p.percent);
                if (r <= 0f) { choice = p; break; }
            }

            // posición dentro del CHUNK, evitando la ruta
            float localZ = UnityEngine.Random.Range(0f, chunkLength);

            float worldRoadCenterX = chunkTr.position.x + roadCenterOffsetX;
            float minX = roadHalfWidth + safeMargin;          // desde centro de ruta hacia afuera
            float maxX = Mathf.Max(minX, chunkWidth * 0.5f);  // borde del decor
            if (maxX <= minX) continue;

            float side = (UnityEngine.Random.value < 0.5f) ? -1f : 1f;
            float offsetX = side * UnityEngine.Random.Range(minX, maxX);

            Vector3 wp = new Vector3(
                worldRoadCenterX + offsetX,
                chunkTr.position.y + globalYOffset,
                chunkTr.position.z + localZ
            );

            // separacion minima
            bool ok = true;
            for (int i = 0; i < spawned.Count; i++)
                if ((spawned[i].position - wp).sqrMagnitude < (minSeparation * minSeparation))
                { ok = false; break; }
            if (!ok) continue;

            var go = Instantiate(choice.prefab, wp, Quaternion.identity);
            if (randomYRotation)
            {
                float yaw = UnityEngine.Random.Range(0f, 360f);
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            go.transform.SetParent(transform, true); // mantiene world transform

            spawned.Add(go.transform);
            placed++;
        }
    }

    // gizmos
    void OnDrawGizmos()
    {
        var tile = GetComponentInParent<ChunkTile>();
        Transform ct = tile ? tile.transform : transform;

        float halfChunkW = chunkWidth * 0.5f;
        float minX = roadHalfWidth + safeMargin;
        float zMid = ct.position.z + chunkLength * 0.5f;
        float y = ct.position.y;

        // chunk
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            new Vector3(ct.position.x + roadCenterOffsetX, y, zMid),
            new Vector3(chunkWidth, 0.01f, chunkLength)
        );

        // ruta 
        float roadW = Mathf.Min(chunkWidth, 2f * roadHalfWidth);
        Gizmos.color = new Color(1f, 0f, 0f, 0.20f);
        Gizmos.DrawCube(
            new Vector3(ct.position.x + roadCenterOffsetX, y, zMid),
            new Vector3(roadW, 0.01f, chunkLength)
        );

        // zonas validas
        float sideW = Mathf.Max(0f, halfChunkW - minX);
        if (sideW > 0f)
        {
            float leftCX = ct.position.x + roadCenterOffsetX - (minX + sideW * 0.5f);
            float rightCX = ct.position.x + roadCenterOffsetX + (minX + sideW * 0.5f);

            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawCube(new Vector3(leftCX, y, zMid), new Vector3(sideW, 0.01f, chunkLength));
            Gizmos.DrawCube(new Vector3(rightCX, y, zMid), new Vector3(sideW, 0.01f, chunkLength));
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(new Vector3(leftCX, y, zMid), new Vector3(sideW, 0.01f, chunkLength));
            Gizmos.DrawWireCube(new Vector3(rightCX, y, zMid), new Vector3(sideW, 0.01f, chunkLength));
        }
    }
}
