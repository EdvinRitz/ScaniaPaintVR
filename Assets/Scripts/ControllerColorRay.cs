using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ControllerColorRay : MonoBehaviour
{
    [SerializeField] private XRRayInteractor rayInteractor;

    public Color CurrentPickedColor { get; private set; } = Color.white;

    private void Update()
    {
        if (rayInteractor == null)
            return;

        if (rayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            ColorWheelPicker picker = hit.collider.GetComponent<ColorWheelPicker>();

            if (picker != null && picker.TryGetColor(hit, out Color pickedColor))
            {
                CurrentPickedColor = pickedColor;

                Debug.Log(
                    $"RGB: " +
                    $"{Mathf.RoundToInt(pickedColor.r * 255)}, " +
                    $"{Mathf.RoundToInt(pickedColor.g * 255)}, " +
                    $"{Mathf.RoundToInt(pickedColor.b * 255)}"
                );
            }
        }
    }
}