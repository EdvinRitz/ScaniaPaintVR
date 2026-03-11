using UnityEngine;
using UnityEngine.XR;

public class ControllerTextureRaycast : MonoBehaviour
{
    [SerializeField] private float rayDistance = 10f;
    [SerializeField] private LayerMask raycastMask = ~0;
    [SerializeField] private XRNode controllerNode = XRNode.RightHand;
    [SerializeField] private SprayPainter sprayPainter;

    [SerializeField] private Transform hitMarker;
    [SerializeField] private float hitMarkerOffset = 0.002f;



    private InputDevice device;
    private bool lastPressed = false;

    private void Start()
    {
        device = InputDevices.GetDeviceAtXRNode(controllerNode);
    }

    private void Update()
    {
        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(controllerNode);

        if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed))
        {
            if (pressed && !lastPressed)
                ShootRay();

            lastPressed = pressed;
        }
    }

    private void ShootRay()
    {
        Ray ray = new Ray(transform.position, transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, raycastMask))
            return;

        Renderer rend = hit.collider.GetComponent<Renderer>() ??
                        hit.collider.GetComponentInParent<Renderer>();

        if (rend == null)
            return;

        Texture2D tex = rend.material.mainTexture as Texture2D;
        if (tex == null)
            return;

        if (!TryGetQuadUV(hit, out Vector2 uv))
            return;

        Color pickedColor = tex.GetPixelBilinear(uv.x, uv.y);

        if (hitMarker != null)
        {
            hitMarker.SetParent(hit.collider.transform, true);
            hitMarker.position = hit.point + hit.normal * hitMarkerOffset;
            hitMarker.rotation = Quaternion.LookRotation(hit.normal);
            hitMarker.gameObject.SetActive(true);
        }


        sprayPainter?.SetSprayColor(pickedColor);

        Debug.Log(
            $"Picked RGB: " +
            $"{Mathf.RoundToInt(pickedColor.r * 255f)}, " +
            $"{Mathf.RoundToInt(pickedColor.g * 255f)}, " +
            $"{Mathf.RoundToInt(pickedColor.b * 255f)}"
        );
    }

    private bool TryGetQuadUV(RaycastHit hit, out Vector2 uv)
    {
        uv = Vector2.zero;

        Transform t = hit.collider.transform;
        Vector3 localPoint = t.InverseTransformPoint(hit.point);

        float u = localPoint.x + 0.5f;
        float v = localPoint.y + 0.5f;

        if (u < 0f || u > 1f || v < 0f || v > 1f)
            return false;

        uv = new Vector2(u, v);
        return true;
    }
}
