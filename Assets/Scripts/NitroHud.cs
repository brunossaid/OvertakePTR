using UnityEngine;
using UnityEngine.UI;

public class NitroHUD : MonoBehaviour
{
    public PlayerController player;  // referencia al player
    public Image nitroBar; // imagen filled de la barra

    void Update()
    {
        if (!player || !nitroBar) return;

        nitroBar.fillAmount = player.NitroNormalized;
    }
}
