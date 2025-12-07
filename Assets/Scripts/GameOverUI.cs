using UnityEngine;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private VehicleHUD hud;

    void Awake()
    {
        if (!hud) hud = Object.FindFirstObjectByType<VehicleHUD>();
    }

    public void Show()
    {
        gameObject.SetActive(true);

        float km = hud ? hud.TotalKilometers : 0f;
        if (distanceText) distanceText.text = $"{km:0.00} kms recorridos";
    }
}
