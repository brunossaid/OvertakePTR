using UnityEngine;

public class CrashDetector : MonoBehaviour
{
    private AudioSource engineAudio;                 // sonido del motor (loop)
    [SerializeField] private AudioClip crashSound;   // golpe/choque
    [SerializeField] private GameObject crashFxPrefab; 

    private bool hasCrashed = false;

    // referencia al player controller para apagar nitro
    private PlayerController playerController;

    void Start()
    {
        engineAudio = GetComponent<AudioSource>();
        playerController = GetComponent<PlayerController>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (hasCrashed) return;
        if (collision.gameObject.layer != LayerMask.NameToLayer("Traffic")) return;

        hasCrashed = true;

        // punto del contacto físico
        var contact = collision.GetContact(0);
        SpawnCrashFx(contact.point, contact.normal);

        HandleCrash();
    }

    void OnTriggerEnter(Collider other)
    {
        if (hasCrashed) return;
        if (other.gameObject.layer != LayerMask.NameToLayer("Traffic")) return;

        hasCrashed = true;

        Vector3 pos = other.ClosestPoint(transform.position);
        Vector3 normal = -transform.forward; 
        SpawnCrashFx(pos, normal);

        HandleCrash();
    }

    private void SpawnCrashFx(Vector3 position, Vector3 normal)
    {
        if (!crashFxPrefab) return;
        // orientamos el burst “hacia afuera” del impacto
        Quaternion rot = Quaternion.LookRotation(normal);
        Instantiate(crashFxPrefab, position, rot);
    }

    private void HandleCrash()
    {
        // apagar motor
        if (engineAudio) engineAudio.Stop();
        // apagar nitro
        if (playerController) playerController.ResetAllEffects();
        // sonido del choque
        if (crashSound) AudioSource.PlayClipAtPoint(crashSound, transform.position);
        // game over
        GameManager.Instance.GameOver();
    }
}
