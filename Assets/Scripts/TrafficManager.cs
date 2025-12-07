using System.Collections.Generic;
using UnityEngine;

public class TrafficManager : MonoBehaviour
{
    [Header("refs")]
    public Transform player;

    [Header("lanes & direction")]
    public float[] laneX = new float[] { -1.625f, 1.625f };
    // true = +z (a favor del player), false = -z (viene de frente)
    public bool[] laneForward = new bool[] { false, true };

    [Header("route height")]
    public float roadY = 0.2f;

    [Header("spawn / pooling")]
    [Tooltip("personajes especiales (no repetibles en pantalla).")]
    public GameObject[] characterPrefabs;

    [Tooltip("npcs comunes")]
    public GameObject[] npcPrefabs;

    [Tooltip("vehiculos grandes (no repetibles en pantalla).")]
    public GameObject[] truckPrefabs;

    [Min(0)] public int npcPoolSize = 30;

    [Header("spawn probabilities")]
    [Range(0f, 1f)] public float npcSpawnChance = 0.75f;   // p(npc)
    [Range(0f, 1f)] public float truckSpawnChance = 0.10f; // p(truck)
    // p(character) = 1 - npcSpawnChance - truckSpawnChance (clamp interno)

    [Header("density / separation")]
    public float spawnAhead = 120f;       // hasta donde rellenar por delante del player
    public float despawnBehind = 40f;     // cuanto toleramos por detras
    public float extraAheadDespawn = 20f; // margen extra para despawn por adelante
    public float spawnEveryMeters = 12f;  // densidad base (fallback)

    [Tooltip("distancia minima entre autos estandar por carril.")]
    public float minGapZ = 25f;

    [Tooltip("distancia minima cuando el vehiculo elegido es camion.")]
    public float minGapZTruck = 40f;

    [Header("dynamic density")]
    public bool useDynamicDensity = true;

    [Tooltip("metros recorridos para llegar a la maxima densidad")]
    public float distanceForMaxDensity = 3000f;

    [Tooltip("distancia entre spawns al inicio (poca densidad)")]
    public float maxSpawnEveryMeters = 18f;

    [Tooltip("distancia entre spawns a maxima densidad (muchos autos)")]
    public float minSpawnEveryMeters = 8f;

    [Header("velocities (m/s)")]
    public float minSpeed = 10f;
    public float maxSpeed = 28f;

    // pools / listas
    private readonly List<GameObject> characterAvailable = new(); // personajes libres (no activos)
    private readonly List<GameObject> truckAvailable = new();     // camiones libres (no activos)
    private Queue<GameObject> npcPool;

    // runtime
    private readonly List<TrafficCar> activeCars = new();
    private float nextSpawnZ;
    private float[] lastLaneZ;
    private float initialPlayerZ;

    void Awake()
    {
        // sanity lanes
        if (laneForward == null || laneForward.Length != laneX.Length)
        {
            laneForward = new bool[laneX.Length];
            for (int i = 0; i < laneForward.Length; i++)
                laneForward[i] = (i == laneForward.Length - 1);
        }

        // personajes: 1 instancia por prefab (garantiza unicidad en pantalla)
        if (characterPrefabs != null)
        {
            for (int i = 0; i < characterPrefabs.Length; i++)
            {
                var prefab = characterPrefabs[i];
                if (!prefab) continue;

                var go = Instantiate(prefab);
                go.SetActive(false);
                EnsureTag(go, VehicleType.Character, i);
                characterAvailable.Add(go);
            }
        }

        // trucks: 1 instancia por prefab (unicidad)
        if (truckPrefabs != null)
        {
            for (int i = 0; i < truckPrefabs.Length; i++)
            {
                var prefab = truckPrefabs[i];
                if (!prefab) continue;

                var go = Instantiate(prefab);
                go.SetActive(false);
                EnsureTag(go, VehicleType.Truck, i);
                truckAvailable.Add(go);
            }
        }

        // npcs: pool con varias copias
        npcPool = new Queue<GameObject>(npcPoolSize);
        for (int i = 0; i < npcPoolSize; i++)
        {
            if (npcPrefabs == null || npcPrefabs.Length == 0) break;
            var prefab = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
            if (!prefab) continue;

            var go = Instantiate(prefab);
            go.SetActive(false);
            EnsureTag(go, VehicleType.NPC, -1);
            npcPool.Enqueue(go);
        }

        lastLaneZ = new float[laneX.Length];
        for (int i = 0; i < lastLaneZ.Length; i++) lastLaneZ[i] = Mathf.NegativeInfinity;

        float startZ = player ? player.position.z : 0f;
        initialPlayerZ = startZ;
        nextSpawnZ = startZ;
    }

    void Update()
    {
        if (!player) return;

        // distancia recorrida aprox en z
        float dist = Mathf.Abs(player.position.z - initialPlayerZ);

        // factor 0..1 segun distanceForMaxDensity
        float t = Mathf.Clamp01(dist / Mathf.Max(1f, distanceForMaxDensity));

        // paso actual entre spawns
        float currentStep = spawnEveryMeters;
        if (useDynamicDensity)
        {
            // al inicio: cerca de maxSpawnEveryMeters (poca densidad)
            // al final: cerca de minSpawnEveryMeters (mucha densidad)
            currentStep = Mathf.Lerp(maxSpawnEveryMeters, minSpawnEveryMeters, t);
        }

        // avanzar los spawn por delante del player
        float targetSpawnFrontZ = player.position.z + spawnAhead;
        while (nextSpawnZ < targetSpawnFrontZ)
        {
            TrySpawnAt(nextSpawnZ);
            nextSpawnZ += currentStep;
        }

        // despawn
        for (int i = activeCars.Count - 1; i >= 0; i--)
        {
            var c = activeCars[i];
            if (!c || !c.gameObject.activeInHierarchy) { activeCars.RemoveAt(i); continue; }

            float z = c.transform.position.z;

            if (z < player.position.z - despawnBehind ||
                z > player.position.z + spawnAhead + extraAheadDespawn)
            {
                Despawn(c);
                activeCars.RemoveAt(i);
            }
        }
    }

    void TrySpawnAt(float z)
    {
        if (laneX.Length == 0) return;

        int lane = Random.Range(0, laneX.Length);

        // probabilidades (clamp por si suman >1)
        float pNpc = Mathf.Clamp01(npcSpawnChance);
        float pTruck = Mathf.Clamp01(truckSpawnChance);
        float pChar = Mathf.Clamp01(1f - pNpc - pTruck);
        float r = Random.value;

        VehicleType wantType =
            (r < pNpc) ? VehicleType.NPC :
            (r < pNpc + pTruck) ? VehicleType.Truck :
            VehicleType.Character;

        // elegir prefab/obj segun tipo y disponibilidad
        GameObject go = null;
        if (wantType == VehicleType.NPC)
        {
            // respetar separacion minima estandar
            if (!CanSpawnOnLane(z, lane, minGapZ)) return;

            if (npcPool.Count > 0) go = npcPool.Dequeue();
            else if (truckAvailable.Count > 0 && CanSpawnOnLane(z, lane, minGapZTruck)) go = TakeRandomTruck();
            else if (characterAvailable.Count > 0 && CanSpawnOnLane(z, lane, minGapZ)) go = TakeRandomCharacter();
        }
        else if (wantType == VehicleType.Truck)
        {
            if (!CanSpawnOnLane(z, lane, minGapZTruck)) return;

            if (truckAvailable.Count > 0) go = TakeRandomTruck();
            else if (npcPool.Count > 0 && CanSpawnOnLane(z, lane, minGapZ)) go = npcPool.Dequeue();
            else if (characterAvailable.Count > 0 && CanSpawnOnLane(z, lane, minGapZ)) go = TakeRandomCharacter();
        }
        else // character
        {
            if (!CanSpawnOnLane(z, lane, minGapZ)) return;

            if (characterAvailable.Count > 0) go = TakeRandomCharacter();
            else if (truckAvailable.Count > 0 && CanSpawnOnLane(z, lane, minGapZTruck)) go = TakeRandomTruck();
            else if (npcPool.Count > 0 && CanSpawnOnLane(z, lane, minGapZ)) go = npcPool.Dequeue();
        }

        if (!go) return;

        // si el primer carril no cumple gap, probar el otro
        if (!RespectsGapFor(go, z, lane))
        {
            int other = (lane + 1) % laneX.Length;
            if (!RespectsGapFor(go, z, other)) return;
            lane = other;
        }

        float dirZ = laneForward[lane] ? +1f : -1f;

        // altura apoyado en la ruta (usa collider real de cada prefab)
        float y = ComputeGroundedY(go);

        go.transform.SetPositionAndRotation(
            new Vector3(laneX[lane], y, z),
            Quaternion.LookRotation(Vector3.forward * dirZ)
        );

        // asegurar/llenar trafficcar
        var tc = go.GetComponent<TrafficCar>() ?? go.AddComponent<TrafficCar>();
        tc.dirZ = dirZ;
        tc.targetSpeed = Random.Range(minSpeed, maxSpeed);
        tc.laneX = laneX;
        tc.currentLane = lane;

        // evita frenazo inmediato post-spawn
        tc.ArmSpawnGrace(0.5f);

        var rb = go.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.linearVelocity = new Vector3(0f, 0f, 0f);
            rb.angularVelocity = Vector3.zero;
        }

        go.SetActive(true);

        // actualizar ultimo z segun tipo
        lastLaneZ[lane] = z;

#if UNITY_EDITOR
        var tag = go.GetComponent<TrafficSpawnTag>();
        Debug.DrawLine(go.transform.position, go.transform.position + Vector3.up * 0.7f,
                       tag != null && tag.type == VehicleType.Truck ? Color.red : Color.green, 1.5f);
#endif
        activeCars.Add(tc);
    }

    float ComputeGroundedY(GameObject go)
    {
        float y = roadY + 0.01f;

        var box = go.GetComponent<BoxCollider>();
        if (box)
        {
            float scaleY = box.transform.lossyScale.y;
            float half = (box.size.y * scaleY) * 0.5f;
            float centerOff = box.center.y * scaleY;
            return roadY + centerOff + half + 0.01f;
        }
        var capsule = go.GetComponent<CapsuleCollider>();
        if (capsule)
        {
            float scaleY = capsule.transform.lossyScale.y;
            float half = (capsule.height * scaleY) * 0.5f;
            float centerOff = capsule.center.y * scaleY;
            return roadY + centerOff + half + 0.01f;
        }
        return y;
    }

    bool CanSpawnOnLane(float z, int lane, float requiredGap)
    {
        // respeta separacion respecto del ultimo spawn en ese carril
        if (z - lastLaneZ[lane] < requiredGap)
        {
            int other = (lane + 1) % laneX.Length;
            if (z - lastLaneZ[other] < requiredGap) return false;
        }
        return true;
    }

    bool RespectsGapFor(GameObject go, float z, int lane)
    {
        var tag = go.GetComponent<TrafficSpawnTag>();
        float need = (tag != null && tag.type == VehicleType.Truck) ? minGapZTruck : minGapZ;
        if (z - lastLaneZ[lane] >= need) return true;

        int other = (lane + 1) % laneX.Length;
        if (z - lastLaneZ[other] >= need) return true;

        return false;
    }

    void Despawn(TrafficCar tc)
    {
        var go = tc.gameObject;
        var tag = go.GetComponent<TrafficSpawnTag>();
        go.SetActive(false);

        if (tag != null)
        {
            switch (tag.type)
            {
                case VehicleType.NPC:
                    npcPool.Enqueue(go);
                    break;
                case VehicleType.Character:
                    if (!characterAvailable.Contains(go)) characterAvailable.Add(go);
                    break;
                case VehicleType.Truck:
                    if (!truckAvailable.Contains(go)) truckAvailable.Add(go);
                    break;
            }
        }
        else
        {
            // fallback
            npcPool.Enqueue(go);
        }
    }

    // helpers
    void EnsureTag(GameObject go, VehicleType type, int uniqueIndex)
    {
        var tag = go.GetComponent<TrafficSpawnTag>();
        if (!tag) tag = go.AddComponent<TrafficSpawnTag>();
        tag.type = type;
        tag.uniqueIndex = uniqueIndex; // -1 para npc / 0..n para no repetibles
    }

    GameObject TakeRandomCharacter()
    {
        if (characterAvailable.Count == 0) return null;
        int idx = Random.Range(0, characterAvailable.Count);
        var go = characterAvailable[idx];
        characterAvailable.RemoveAt(idx);
        return go;
    }

    GameObject TakeRandomTruck()
    {
        if (truckAvailable.Count == 0) return null;
        int idx = Random.Range(0, truckAvailable.Count);
        var go = truckAvailable[idx];
        truckAvailable.RemoveAt(idx);
        return go;
    }

    void OnDrawGizmosSelected()
    {
        if (!player) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(-5, roadY, player.position.z + spawnAhead),
                        new Vector3(5, roadY, player.position.z + spawnAhead));
    }
}

public enum VehicleType { NPC, Character, Truck }

[DisallowMultipleComponent]
public class TrafficSpawnTag : MonoBehaviour
{
    public VehicleType type = VehicleType.NPC;
    // para personajes y camiones usamos indice para que no se repitan
    public int uniqueIndex = -1;
}
