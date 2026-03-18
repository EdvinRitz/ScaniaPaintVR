using UnityEngine;

public class SprayDebugOverlay : MonoBehaviour
{
    [Header("Refs")]
    public SprayPainter sprayPainter;
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grabInteractable;
    public Transform nozzle;
    public LayerMask paintableMask;
    public RenderTexture paintRT;
    public Renderer targetRenderer;

    private string debugText = "";

    void Update()
    {
        bool grabbed = grabInteractable != null && grabInteractable.isSelected;
        bool isSpraying = sprayPainter != null && sprayPainter.isSpraying;
        bool hasPainter = sprayPainter != null;
        bool hasGrabInteractable = grabInteractable != null;

        bool raycastHit = false;
        string hitName = "None";
        string hitTransformName = "None";
        string hitColliderType = "None";
        Vector2 hitUV = Vector2.zero;
        string hitMaterialName = "None";
        string hitShaderName = "None";
        bool hitMatHasPaintRT = false;
        string hitMatRTName = "None";

        if (nozzle != null)
        {
            float rayDistance = sprayPainter != null ? sprayPainter.maxDistance : 0f;

            if (Physics.Raycast(nozzle.position, nozzle.forward, out RaycastHit hit, rayDistance, paintableMask))
            {
                raycastHit = true;
                hitName = hit.collider != null ? hit.collider.name : "Unknown";
                hitTransformName = hit.transform != null ? hit.transform.name : "Unknown";
                hitColliderType = hit.collider != null ? hit.collider.GetType().Name : "Unknown";
                hitUV = hit.textureCoord;

                Renderer hitRenderer = hit.collider != null ? hit.collider.GetComponent<Renderer>() : null;
                if (hitRenderer != null && hitRenderer.material != null)
                {
                    Material hitMat = hitRenderer.material;
                    hitMaterialName = hitMat.name;
                    hitShaderName = hitMat.shader != null ? hitMat.shader.name : "NULL";
                    hitMatHasPaintRT = hitMat.HasProperty("_PaintRT");

                    if (hitMatHasPaintRT)
                    {
                        Texture tex = hitMat.GetTexture("_PaintRT");
                        hitMatRTName = tex != null ? tex.name : "NULL";
                    }
                }
            }
        }

        string targetRendererMatName = "None";
        string targetRendererShaderName = "None";
        bool targetRendererHasPaintRT = false;
        string targetRendererRTName = "None";

        if (targetRenderer != null && targetRenderer.material != null)
        {
            Material mat = targetRenderer.material;
            targetRendererMatName = mat.name;
            targetRendererShaderName = mat.shader != null ? mat.shader.name : "NULL";
            targetRendererHasPaintRT = mat.HasProperty("_PaintRT");

            if (targetRendererHasPaintRT)
            {
                Texture tex = mat.GetTexture("_PaintRT");
                targetRendererRTName = tex != null ? tex.name : "NULL";
            }
        }

        string brushMatName = "NULL";
        string brushShaderName = "NULL";

        if (sprayPainter != null && sprayPainter.brushMat != null)
        {
            brushMatName = sprayPainter.brushMat.name;
            brushShaderName = sprayPainter.brushMat.shader != null
                ? sprayPainter.brushMat.shader.name
                : "NULL";
        }

        string paintRTInfo = "NULL";
        string paintRTCreated = "false";

        if (paintRT != null)
        {
            paintRTInfo = $"{paintRT.name} ({paintRT.width}x{paintRT.height})";
            paintRTCreated = paintRT.IsCreated().ToString();
        }

        debugText =
            $"Uses XR Activate Events: true\n" +
            $"SprayPainter Ref: {hasPainter}\n" +
            $"GrabInteractable Ref: {hasGrabInteractable}\n" +
            $"Grabbed: {grabbed}\n" +
            $"isSpraying: {isSpraying}\n" +
            $"\n" +
            $"Nozzle: {(nozzle != null ? nozzle.name : "NULL")}\n" +
            $"Raycast Hit: {raycastHit}\n" +
            $"Hit Object: {hitName}\n" +
            $"Hit Transform: {hitTransformName}\n" +
            $"Hit Collider Type: {hitColliderType}\n" +
            $"Hit UV: {hitUV}\n" +
            $"Hit Material: {hitMaterialName}\n" +
            $"Hit Shader: {hitShaderName}\n" +
            $"Hit Has _PaintRT: {hitMatHasPaintRT}\n" +
            $"Hit _PaintRT: {hitMatRTName}\n" +
            $"\n" +
            $"Target Renderer Mat: {targetRendererMatName}\n" +
            $"Target Renderer Shader: {targetRendererShaderName}\n" +
            $"Target Renderer Has _PaintRT: {targetRendererHasPaintRT}\n" +
            $"Target Renderer _PaintRT: {targetRendererRTName}\n" +
            $"\n" +
            $"Brush Mat: {brushMatName}\n" +
            $"Brush Shader: {brushShaderName}\n" +
            $"paintRT: {paintRTInfo}\n" +
            $"paintRT Created: {paintRTCreated}";
    }

    void OnGUI()
    {
        GUI.Box(new Rect(15, 15, 700, 700), "Spray Debug");

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 20;
        style.normal.textColor = Color.white;
        style.wordWrap = true;

        GUI.Label(new Rect(30, 50, 670, 670), debugText, style);
    }
}
