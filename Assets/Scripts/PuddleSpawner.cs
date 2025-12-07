using UnityEngine;

public class PuddleSpawner : MonoBehaviour
{
    public GameObject waterPrefab;
    public GameObject oilPrefab;

    [Range(0f,1f)]
    public float waterProbability = 0.25f;

    [Range(0f,1f)]
    public float oilProbability = 0.06f;

    public float chunkLength = 30f;

    GameObject current;

    public void Respawn()
    {
        // borrar el charco anterior
        if (current) Destroy(current);

        // decidir si spawn
        float r = Random.value;

        GameObject prefab = null;

        if (r < oilProbability)
            prefab = oilPrefab;
        else if (r < waterProbability + oilProbability)
            prefab = waterPrefab;

        if (!prefab) return;

        // carriles -1.625 o +1.625
        float x = (Random.value < 0.5f) ? -1.625f : 1.625f;

        // posición Z dentro del tile
        float z = Random.Range(2f, chunkLength - 5f);

        Vector3 pos = transform.position + new Vector3(x, 0f, z);

        current = Instantiate(prefab, pos, Quaternion.identity, transform);
    }
}
