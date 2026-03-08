using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class SprayPainter : MonoBehaviour
{
    [Header("Refs")]
    public Transform nozzle;                 // Nozzle child (blue Z forward)
    public LayerMask paintableMask;          // Includes "Paintable"
    public RenderTexture paintRT;            // Same RT used by the truck material
    public Material brushMat;                // Your M_SprayBrush
    public float maxDistance = 3f;

    [Header("Brush")]
    public Color color = Color.black;
    public bool isSpraying = false;

    [Header("Distance-based sizing")]
    public float sizeNearDist = 0.2f;        // meters
    public float sizeFarDist = 2.5f;         // meters
    public float brushUVNear = 0.010f;       // smaller when close
    public float brushUVFar = 0.022f;        // larger when far

    [Header("Stroke Spacing")]
    public float stampSpacing = 0.005f;      // world-space meters between stamps

    [Header("Stroke Safety")]
    public float maxUvJump = 0.25f;          // 0..1 UV distance; tune 0.15–0.35
    public float maxNormalAngle = 35f;       // degrees; tune 25–60

    [Header("Reticle")]
    public GameObject reticle;               // assign the Quad
    public float reticleOffset = 0.002f;     // lift off surface to avoid z-fight
    public float reticleSizeNear = 0.08f;    // world meters when near
    public float reticleSizeFar = 0.22f;     // world meters when far

    [Header("Performance")]
    public int maxStampsPerFrame = 40;

    private bool hadPreviousHit = false;
    private Vector3 previousHitPoint;
    private Vector2 previousHitUV;
    private Collider previousCollider;
    private Vector3 previousNormal;

    void Start()
    {
        // Warm-up: one tiny invisible stamp
        if (brushMat && paintRT)
        {
            brushMat.SetVector("_UV", new Vector4(0.5f, 0.5f, 0, 0));
            brushMat.SetFloat("_Size", 0.0001f);
            brushMat.SetColor("_Color", new Color(0,0,0,0));

            var prev = RenderTexture.active;
            RenderTexture.active = paintRT;
            GL.PushMatrix(); GL.LoadOrtho();
            Graphics.Blit(null as Texture, paintRT, brushMat);
            GL.PopMatrix();
            RenderTexture.active = prev;
        }
    }
    void Update()
    {
        UpdateReticleRaycast();

        if (!isSpraying)
        {
            hadPreviousHit = false;
            return;
        }

        SprayContinuous();
    }

    public void OnActivated(ActivateEventArgs args)
    {
        isSpraying = true;
    }

    public void OnDeactivated(DeactivateEventArgs args)
    {
        isSpraying = false;
        hadPreviousHit = false;
    }

    void UpdateReticle(RaycastHit hit)
    {
        if (!reticle) return;

        reticle.SetActive(true);
        reticle.transform.position = hit.point + hit.normal * reticleOffset;
        reticle.transform.rotation = Quaternion.LookRotation(hit.normal);
        reticle.transform.Rotate(0f, 180f, 0f);
        reticle.transform.localScale = Vector3.one * ReticleSizeFor(hit.distance);
    }

    void UpdateReticleRaycast()
    {
        if (!nozzle || !reticle) return;

        if (Physics.Raycast(nozzle.position, nozzle.forward, out var hit, maxDistance, paintableMask))
        {
            UpdateReticle(hit);
        }
        else
        {
            reticle.SetActive(false);
        }
    }

    void SprayContinuous()
    {
        if (!nozzle) return;

        if (!Physics.Raycast(nozzle.position, nozzle.forward, out var hit, maxDistance, paintableMask))
        {
            hadPreviousHit = false;
            return;
        }

        // Only spray if the surface has valid UVs
        if (hit.textureCoord.x <= 0.0001f && hit.textureCoord.y <= 0.0001f)
        {
            hadPreviousHit = false;
            return;
        }

        Vector2 currentUV = hit.textureCoord;

        if (!hadPreviousHit)
        {
            StampAtUV(currentUV, hit.distance);
            previousHitPoint = hit.point;
            previousHitUV = currentUV;
            hadPreviousHit = true;
            return;
        }

        bool surfaceChanged =
        previousCollider != hit.collider ||
        Vector3.Angle(previousNormal, hit.normal) > maxNormalAngle ||
        Vector2.Distance(previousHitUV, currentUV) > maxUvJump;

        if (surfaceChanged)
        {
            // Start a new stroke here: just stamp once at the new place
            StampAtUV(currentUV, hit.distance);

            previousHitPoint = hit.point;
            previousHitUV = currentUV;
            previousCollider = hit.collider;
            previousNormal = hit.normal;
            hadPreviousHit = true;
            return;
        }

        float worldDistance = Vector3.Distance(previousHitPoint, hit.point);
        int steps = Mathf.Max(1, Mathf.CeilToInt(worldDistance / Mathf.Max(0.0001f, stampSpacing)));

        for (int i = 1; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 uv = Vector2.Lerp(previousHitUV, currentUV, t);
            float distanceFromNozzle = Mathf.Lerp(Vector3.Distance(nozzle.position, previousHitPoint), hit.distance, t);
            StampAtUV(uv, distanceFromNozzle);
        }

        previousHitPoint = hit.point;
        previousHitUV = currentUV;
        previousCollider = hit.collider;
        previousNormal = hit.normal;
    }

    void StampAtUV(Vector2 uv, float distance)
    {
        brushMat.SetVector("_UV", new Vector4(uv.x, uv.y, 0, 0));

        float minBrushUV = 2f / paintRT.width;
        brushMat.SetFloat("_Size", Mathf.Max(minBrushUV, BrushUVFor(distance)));
        brushMat.SetColor("_Color", color);

        var prev = RenderTexture.active;
        RenderTexture.active = paintRT;
        GL.PushMatrix();
        GL.LoadOrtho();
        Graphics.Blit(null as Texture, paintRT, brushMat);
        GL.PopMatrix();
        RenderTexture.active = prev;
    }

    private float BrushUVFor(float distance)
    {
        return Mathf.Lerp(brushUVNear, brushUVFar, Dist01(distance));
    }

    private float Dist01(float distance)
    {
        return Mathf.InverseLerp(sizeNearDist, sizeFarDist, Mathf.Clamp(distance, sizeNearDist, sizeFarDist));
    }

    private float ReticleSizeFor(float distance)
    {
        return Mathf.Lerp(reticleSizeNear, reticleSizeFar, Dist01(distance));
    }
}
