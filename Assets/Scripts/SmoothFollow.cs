using UnityEngine;

public class SmoothFollow : MonoBehaviour
{
    [Header("target")]
    public Transform target;

    [Header("position offset")]
    public Vector3 offset = new Vector3(0, 5.5f, -15f);

    [Header("follow settings")]
    public float lerp = 8f;          //  normal
    public float nitroLerp = 12f;    // cuando hay nitro

    [Header("camera fov")]
    public Camera cam;
    public float baseFov = 60f;      // FOV normal
    public float nitroFov = 75f;     // FOV con nitro
    public float fovLerpSpeed = 5f;  // que tan rapido cambia

    [Header("nitro shake (leve)")]
    public float shakeAmount = 0.15f;   // intensidad
    public float shakeSpeed = 15f;      // velocidad de oscilacion

    // cache
    PlayerController pc;

    void Start()
    {
        if (target)
            pc = target.GetComponent<PlayerController>();

        if (!cam)
            cam = GetComponentInChildren<Camera>();

        if (cam)
            cam.fieldOfView = baseFov;
    }

    void LateUpdate()
    {
        if (!target) return;

        bool nitro = pc && pc.IsNitroActive;

        // elegir lerp segun nitro
        float followLerp = nitro ? nitroLerp : lerp;

        // posicion
        Vector3 desired = target.position + offset;

        // aplicar un leve shake SOLO con nitro
        if (nitro)
        {
            float shakeX = Mathf.Sin(Time.time * shakeSpeed) * shakeAmount;
            float shakeY = Mathf.Cos(Time.time * shakeSpeed * 1.2f) * (shakeAmount * 0.5f);

            desired += new Vector3(shakeX, shakeY, 0);
        }

        // smooth follow
        float t = 1f - Mathf.Exp(-followLerp * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);

        // mirar suavemente al auto
        transform.LookAt(target.position + target.forward * 6f);

        // FOV
        if (cam)
        {
            float targetFov = nitro ? nitroFov : baseFov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * fovLerpSpeed);
        }
    }
}
