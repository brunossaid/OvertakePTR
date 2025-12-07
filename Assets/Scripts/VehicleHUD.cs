using UnityEngine;
using TMPro;

public class VehicleHUD : MonoBehaviour
{
    [Header("references")]
    [SerializeField] private Rigidbody targetRb;              // rb del player
    [SerializeField] private TextMeshProUGUI speedText;       // texto para velocidad
    [SerializeField] private TextMeshProUGUI distanceText;    // texto para km recorridos
    [SerializeField] private TextMeshProUGUI nitroText;       // texto para porcentaje de nitro
    [SerializeField] private PlayerController player;         // ref al player para leer nitro
    [SerializeField] private TextMeshProUGUI effectText;      // texto para efectos 

    [Header("optional")]
    [SerializeField, Range(0.01f, 1f)] private float smoothSeconds = 0.15f; // suavizado ui

    private float shownKmh = 0f;
    private float totalDistance = 0f;   // distancia total recorrida (m)
    public float TotalKilometers => totalDistance * 0.001f;
    private Vector3 lastPosition;

    void Reset()
    {
        if (!targetRb)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo) targetRb = playerGo.GetComponent<Rigidbody>();
        }

        if (!player)
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo) player = playerGo.GetComponent<PlayerController>();
        }

        if (!speedText) speedText = GetComponentInChildren<TextMeshProUGUI>();
    }

    void Start()
    {
        if (targetRb)
            lastPosition = targetRb.position;

        // ocultar texto de efectos al inicio
        if (effectText)
            effectText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!targetRb)
            return;

        // calcular velocidad (km/h)
        float kmh = targetRb.linearVelocity.magnitude * 3.6f;

        // suavizado
        float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothSeconds);
        shownKmh = Mathf.Lerp(shownKmh, kmh, k);

        // mostrar velocidad
        if (speedText)
            speedText.text = $"{shownKmh:0} km/h";

        // calcular distancia total (km totales)
        float deltaDist = Vector3.Distance(targetRb.position, lastPosition);
        totalDistance += deltaDist;
        lastPosition = targetRb.position;

        // mostrar distancia en km (con 2 decimales)
        if (distanceText)
            distanceText.text = $"{totalDistance / 1000f:0.00} km";

        // mostrar nitro en porcentaje (0..100)
        if (nitroText && player)
        {
            float nitro01 = player.NitroNormalized;
            float pct = nitro01 * 100f;
            nitroText.text = $"nitro: {pct:0}%";
        }

        // mostrar efectos (ruedas mojadas)
        if (effectText && player)
        {
            if (player.HasWetTires && player.WetTime > 0f)
            {
                if (!effectText.gameObject.activeSelf)
                    effectText.gameObject.SetActive(true);

                float t = player.WetTime;
                int total = Mathf.CeilToInt(t);
                int sec = total % 60;
                int min = total / 60;

                effectText.text = $"Mojado {min:00}:{sec:00}";
            }
            else
            {
                if (effectText.gameObject.activeSelf)
                    effectText.gameObject.SetActive(false);
            }
        }

    }
}
