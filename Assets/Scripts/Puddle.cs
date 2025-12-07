using UnityEngine;

public enum PuddleType
{
    Water,
    Oil
}

public class Puddle : MonoBehaviour
{
    [Header("puddle type")]
    public PuddleType type = PuddleType.Water;

    [Header("conditions")]
    public float minSpeed = 80f;        // km/h minimos para activar
    public float effectDuration = 5f;   // duracion del efecto 

    [Header("fx (optional)")]
    public ParticleSystem splashParticles;
    public AudioSource splashAudio;

    Collider col;

    void Awake()
    {
        col = GetComponent<Collider>();
        if (col) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerController>();
        if (!player) return;

        // chequeo de velocidad
        float kmh = player.GetSpeedKmh();
        if (kmh < minSpeed) return;

        // avisar al player
        player.OnPuddleEnter(this);

        // fx opcionales
        if (splashParticles) splashParticles.Play();
        if (splashAudio) splashAudio.Play();

        // desactivar collider para que no se repita
        if (col) col.enabled = false;
    }
}
