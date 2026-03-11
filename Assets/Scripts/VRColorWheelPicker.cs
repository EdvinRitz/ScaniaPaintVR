using UnityEngine;

public class ColorWheelPicker : MonoBehaviour
{
    private Texture2D colorTexture;

    public Color CurrentColor { get; private set; } = Color.white;

    private void Awake()
    {
        Renderer rend = GetComponent<Renderer>();

        if (rend == null)
        {
            Debug.LogError("ColorWheelPicker: No Renderer found.");
            return;
        }

        colorTexture = rend.material.mainTexture as Texture2D;

        if (colorTexture == null)
        {
            Debug.LogError("ColorWheelPicker: Material main texture is not a Texture2D.");
        }
    }

    public bool TryGetColor(RaycastHit hit, out Color color)
    {
        color = Color.clear;

        if (colorTexture == null)
            return false;

        Vector2 uv = hit.textureCoord;
        color = colorTexture.GetPixelBilinear(uv.x, uv.y);

        // Ignore transparent pixels outside the circle
        if (color.a < 0.1f)
            return false;

        CurrentColor = color;
        return true;
    }
}