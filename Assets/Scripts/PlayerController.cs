using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("movement")]
    public float accel = 16f;          // m/s^2 (w)
    public float brake = 12f;          // m/s^2 (s)
    public float maxSpeed = 118.8f;    // km/h
    public float laneChangeSpeed = 8f; // m/s lateral
    public float coastDecel = 6f;      // m/s^2 -> desaceleracion natural al soltar w/s
    public float[] laneX = new float[] { -1.625f, 1.625f };

    [Header("visual tilt (roll)")]
    public Transform visual;
    public float maxRollDeg = 7f;
    public float rollSmoothTime = 0.2f;

    [Header("nitro visual")]
    public float nitroPitchDeg = 6f;    // cuanto se levanta la trompa
    public float nitroYawDeg = 1.3f;    // tembleque leve izq/der
    public float nitroYawSpeed = 18f;   // velocidad del tembleque

    // === audio motor ===
    [Header("motor sound")]
    public AudioSource engineAudio;
    public float minPitch = 0.85f;
    public float maxPitch = 2.0f;
    public float minVolume = 0.15f;
    public float maxVolume = 0.5f;

    // === nitro ===
    [Header("nitro")]
    public bool nitroEnabled = true;

    [Tooltip("segundos que dura el nitro si lo usas de 100% a 0 sin soltar")]
    public float nitroMaxUseTime = 2.5f;   // s de uso continuo

    [Tooltip("segundos que tarda en recargarse de 0% a 100% sin usarlo")]
    public float nitroRechargeTime = 5f;   // s de recarga completa

    public float nitroExtraAccel = 10f;    // m/s^2 extra
    public float nitroMaxSpeed = 162f;     // km/h velocidad maxima con nitro

    [Tooltip("audio opcional de nitro")]
    public AudioSource nitroAudio;

    public ParticleSystem nitroParticles;
    public float nitroVolume = 0.6f;       // volumen base del nitro

    [Tooltip("tiempo de espera despues de usar nitro")]
    public float nitroCooldown = 0.7f;     // s de cooldown

    // === charcos ===
    [Header("puddles (water/oil)")]
    public float wetBrakeMultiplier = 0.35f; // frena menos con ruedas mojadas
    public float wetCoastMultiplier = 0.5f;  // desacelera menos al soltar

    [Range(0f, 10f)]
    public float wetYawDeg = 3f;            // max intensidad del movimiento mojado
    public float wetYawSpeed = 4f;          // velocidad del movimiento mojado

    public AudioSource wetAudio;
    public float wetVolume = 0.4f;
    public ParticleSystem wetParticles; 

    // runtime
    const float maxNitro = 100f; // capacidad fija del tanque

    Rigidbody rb;
    int laneIndex = 1;
    float targetX;

    float rollZ;
    float rollVel;
    Quaternion visualBase;

    // nitro visual interno
    float nitroPitch;       // grados eje x
    float nitroPitchVel;    // suavizado

    float nitro;            // 0..maxNitro
    bool nitroHeld;
    bool nitroActive;
    bool nitroActiveLast;
    float nitroCooldownUntil;

    // estado ruedas mojadas
    bool wetTires;
    float wetTimer;

    // factor visual para que no corte seco al terminar el efecto
    float wetVisualFactor;  // 0..1

    public float NitroNormalized => (maxNitro > 0f) ? nitro / maxNitro : 0f;
    public bool IsNitroActive => nitroActive;
    public bool HasWetTires => wetTires;
    public float WetTime => Mathf.Max(0f, wetTimer);

    public float GetSpeedKmh()
    {
        return rb.linearVelocity.magnitude * 3.6f;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (!visual) visual = transform;
        visualBase = visual.localRotation;

        if (engineAudio)
        {
            engineAudio.loop = true;
            engineAudio.playOnAwake = true;
            engineAudio.spatialBlend = 0f;
            engineAudio.dopplerLevel = 0f;

            if (!engineAudio.isPlaying)
                engineAudio.Play();
        }

        // nitro arranca vacio
        nitro = 0f;

        if (nitroAudio)
        {
            nitroAudio.loop = true;
            nitroAudio.playOnAwake = false;
            nitroAudio.spatialBlend = 0f;
            nitroAudio.dopplerLevel = 0f;
            nitroAudio.volume = nitroVolume;
        }

        if (wetAudio)
        {
            wetAudio.loop = true;
            wetAudio.playOnAwake = false;
            wetAudio.spatialBlend = 0f;
            wetAudio.dopplerLevel = 0f;
            wetAudio.volume = 0f;
        }

        if (wetParticles)
        {
            wetParticles.Stop(true);
        }

        SetLaneIndex(laneIndex);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.A))
            ChangeLane(-1);

        if (Input.GetKeyDown(KeyCode.D))
            ChangeLane(+1);

        nitroHeld = Input.GetKey(KeyCode.Space);
    }

    void FixedUpdate()
    {
        // convertir limites desde km/h a m/s
        float maxSpeedMS = maxSpeed / 3.6f;
        float nitroMaxSpeedMS = nitroMaxSpeed / 3.6f;

        float v = rb.linearVelocity.z;
        bool w = Input.GetKey(KeyCode.W);
        bool s = Input.GetKey(KeyCode.S);

        // actualizar efecto ruedas mojadas (logico)
        if (wetTires)
        {
            wetTimer -= Time.fixedDeltaTime;
            if (wetTimer <= 0f)
            {
                wetTimer = 0f;
                wetTires = false;
            }
        }

        // factores actuales de freno y desacel segun superficie
        float brakeCur = brake;
        float coastCur = coastDecel;

        if (wetTires)
        {
            brakeCur *= wetBrakeMultiplier;
            coastCur *= wetCoastMultiplier;
        }

        // tasas segun tiempos configurados
        float nitroRechargeRate =
            (nitroRechargeTime > 0f) ? maxNitro / nitroRechargeTime : maxNitro;

        float nitroConsumeRate =
            (nitroMaxUseTime > 0f) ? maxNitro / nitroMaxUseTime : maxNitro;

        // --- nitro: recarga / consumo / cooldown ---
        nitroActiveLast = nitroActive;
        nitroActive = false;

        if (nitroEnabled && maxNitro > 0f)
        {
            // recarga cuando NO lo estas usando
            if (!nitroHeld || !w)
            {
                nitro += nitroRechargeRate * Time.fixedDeltaTime;
                if (nitro > maxNitro)
                    nitro = maxNitro;
            }

            float nitroMinToUse = 0.25f * maxNitro; // 25% para activar

            bool wantsNitro =
                nitroHeld && w && Time.time >= nitroCooldownUntil;

            bool canUseNitro =
                wantsNitro &&
                (nitroActiveLast || nitro >= nitroMinToUse) &&
                nitro > 0.01f;

            if (canUseNitro)
            {
                nitroActive = true;
                nitro -= nitroConsumeRate * Time.fixedDeltaTime;
                if (nitro < 0f)
                    nitro = 0f;
            }
        }

        // arrancar cooldown al soltar nitro
        if (!nitroActive && nitroActiveLast)
            nitroCooldownUntil = Time.time + nitroCooldown;

        // limite segun nitro o no
        float speedLimit = nitroActive ? nitroMaxSpeedMS : maxSpeedMS;

        // === movimiento adelante/atras ===
        if (w || s)
        {
            float a = 0f;

            if (w) a += accel;
            if (s) a -= brakeCur;

            if (nitroActive && w)
                a += nitroExtraAccel;

            v = Mathf.Clamp(v + a * Time.fixedDeltaTime, 0f, speedLimit);
        }
        else
        {
            v = Mathf.MoveTowards(v, 0f, coastCur * Time.fixedDeltaTime);
        }

        if (v > speedLimit)
            v = speedLimit;

        // === movimiento lateral ===
        float dx = targetX - rb.position.x;
        float xSpeedTarget = dx / Time.fixedDeltaTime;
        float xSpeed = Mathf.Clamp(xSpeedTarget, -laneChangeSpeed, laneChangeSpeed);

        rb.linearVelocity =
            new Vector3(xSpeed, rb.linearVelocity.y, v);

        // factor de velocidad 0..1 (para efectos visuales y de audio)
        float speed01 = Mathf.Clamp01(
            v / Mathf.Max(0.0001f, maxSpeedMS)
        );

        // suavizar factor visual de mojado (para que no corte seco)
        float wetTargetVisual = (wetTires && speed01 > 0.01f) ? 1f : 0f;
        wetVisualFactor = Mathf.MoveTowards(
            wetVisualFactor,
            wetTargetVisual,
            Time.fixedDeltaTime * 2f // 0.5 s aprox de fade
        );

        // ======================
        // visual del auto
        // ======================

        float desiredRoll =
            -(xSpeed / Mathf.Max(0.0001f, laneChangeSpeed)) * maxRollDeg;

        if (nitroActive)
            desiredRoll *= 1.3f;

        rollZ = Mathf.SmoothDampAngle(
            rollZ,
            desiredRoll,
            ref rollVel,
            rollSmoothTime
        );

        float targetPitch = nitroActive ? nitroPitchDeg : 0f;

        nitroPitch = Mathf.SmoothDampAngle(
            nitroPitch,
            targetPitch,
            ref nitroPitchVel,
            rollSmoothTime
        );

        float yawJitter = 0f;

        // tembleque de nitro (igual que siempre)
        if (nitroActive)
            yawJitter = Mathf.Sin(Time.time * nitroYawSpeed) * nitroYawDeg;

        // movimiento extra por ruedas mojadas, en OTRO eje
        float wetSway = 0f;
        if (wetVisualFactor > 0f && speed01 > 0f)
        {
            float amp = wetYawDeg * wetVisualFactor * speed01; // 0..wetYawDeg (max 10)
            wetSway = Mathf.Sin(Time.time * wetYawSpeed) * amp;
        }

        Quaternion rotRoll = Quaternion.AngleAxis(rollZ, Vector3.up);
        Quaternion rotPitch = Quaternion.AngleAxis(-nitroPitch, Vector3.right);
        Quaternion rotYaw = Quaternion.AngleAxis(yawJitter, Vector3.up);
        Quaternion rotWet = Quaternion.AngleAxis(wetSway, Vector3.forward); // tercer eje

        visual.localRotation =
            visualBase * rotPitch * rotYaw * rotRoll * rotWet;

        // ======================
        // audio motor
        // ======================
        if (engineAudio)
        {
            float t = Mathf.Clamp01(v / Mathf.Max(0.0001f, maxSpeedMS));

            engineAudio.pitch = Mathf.Lerp(minPitch, maxPitch, t);
            engineAudio.volume = Mathf.Lerp(minVolume, maxVolume, t);
        }

        // ======================
        // audio nitro
        // ======================
        if (nitroAudio)
        {
            if (nitroActive)
            {
                if (!nitroAudio.isPlaying)
                {
                    nitroAudio.volume = nitroVolume;
                    nitroAudio.Play();
                }
            }
            else
            {
                if (nitroAudio.isPlaying)
                {
                    nitroAudio.volume = Mathf.MoveTowards(
                        nitroAudio.volume,
                        0f,
                        3f * Time.deltaTime
                    );

                    if (nitroAudio.volume <= 0.01f)
                        nitroAudio.Stop();
                }
            }
        }

        // ======================
        // audio mojado
        // ======================
        if (wetAudio)
        {
            // volumen segun velocidad y si sigue activo el efecto
            float targetWetVol =
                (wetTires && speed01 > 0.01f) ? wetVolume * speed01 : 0f;

            wetAudio.volume = Mathf.MoveTowards(
                wetAudio.volume,
                targetWetVol,
                2f * Time.deltaTime
            );

            if (wetAudio.volume > 0.01f)
            {
                if (!wetAudio.isPlaying)
                    wetAudio.Play();
            }
            else
            {
                if (wetAudio.isPlaying)
                    wetAudio.Stop();
            }
        }

        // ======================
        // particulas nitro
        // ======================
        if (nitroParticles)
        {
            if (nitroActive)
            {
                if (!nitroParticles.isPlaying)
                    nitroParticles.Play(true);
            }
            else
            {
                if (nitroParticles.isPlaying)
                    nitroParticles.Stop(true);
            }
        }

        // ======================
        // particulas mojado
        // ======================
        if (wetParticles)
        {
            // particulas solo si hay efecto visual y algo de velocidad
            if (wetVisualFactor > 0f && speed01 > 0.05f)
            {
                if (!wetParticles.isPlaying)
                    wetParticles.Play(true);

                // escalar emision segun velocidad y efecto
                var emission = wetParticles.emission;
                float baseRate = 50f; 
                emission.rateOverTime = baseRate * wetVisualFactor * (0.3f + 0.7f * speed01);
            }
            else
            {
                if (wetParticles.isPlaying)
                    wetParticles.Stop(true);
            }
        }
    }

    void ChangeLane(int delta)
    {
        int newIndex = Mathf.Clamp(
            laneIndex + delta,
            0,
            laneX.Length - 1
        );

        if (newIndex != laneIndex)
            SetLaneIndex(newIndex);
    }

    void SetLaneIndex(int idx)
    {
        laneIndex = idx;
        targetX = laneX[laneIndex];
    }

    // fuerza a apagar todos los efectos temporales
        public void ResetAllEffects()
    {
        // --- nitro ---
        nitroActive = false;
        nitroHeld = false;

        if (nitroAudio)
        {
            nitroAudio.Stop();
            nitroAudio.volume = 0f;
        }

        if (nitroParticles && nitroParticles.isPlaying)
            nitroParticles.Stop(true);

        // --- agua  ---
        wetTires = false;
        wetTimer = 0f;
        wetVisualFactor = 0f;

        if (wetAudio)
        {
            wetAudio.Stop();
            wetAudio.volume = 0f;
        }

        if (wetParticles && wetParticles.isPlaying)
            wetParticles.Stop(true);
    }




    // llamado por puddle al pisar un charco
    public void OnPuddleEnter(Puddle puddle)
    {
        if (!puddle) return;

        switch (puddle.type)
        {
            case PuddleType.Water:
                wetTires = true;
                wetTimer = Mathf.Max(wetTimer, puddle.effectDuration);
                break;

            case PuddleType.Oil:
                // lo implementamos despues (trompo / perder control)
                break;
        }
    }
}
