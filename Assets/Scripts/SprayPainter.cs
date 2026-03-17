using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class SprayPainter : MonoBehaviour
{
    private static readonly int PaintColorId = Shader.PropertyToID("PaintColor");
    private static readonly int UnderscorePaintColorId = Shader.PropertyToID("_PaintColor");
    private static readonly int BaseColorId = Shader.PropertyToID("BaseColor");
    private static readonly int UnderscoreBaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("Color");
    private static readonly int UnderscoreColorId = Shader.PropertyToID("_Color");

    [Header("Refs")]
    public Transform nozzle;                 // Nozzle child (blue Z forward)
    public LayerMask paintableMask;          // Includes "Paintable"
    public RenderTexture paintRT;            // Same RT used by the truck material
    public Material brushMat;                // Your M_SprayBrush
    public Material targetMaterial;          // Material that should mirror the selected spray color
    public XRNode controllerNode = XRNode.RightHand;
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

    [Header("Audio")]
    public AudioSource sprayAudioSource;
    public AudioClip sprayLoopClip;
    [Range(0f, 1f)] public float sprayVolume = 1f;
    [Range(0.1f, 3f)] public float sprayPitch = 1f;
    [Range(0f, 0.5f)] public float sprayPitchRandomness = 0.05f;
    public bool randomizeSprayStartTime = true;

    private bool hadPreviousHit = false;
    private Vector3 previousHitPoint;
    private Vector2 previousHitUV;
    private Collider previousCollider;
    private Vector3 previousNormal;
    private InputDevice device;

    void Start()
    {
        device = InputDevices.GetDeviceAtXRNode(controllerNode);
        SetSprayColor(color);
        ConfigureSprayAudio();

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
        UpdateSprayInput();
        UpdateReticleRaycast();

        if (!isSpraying)
        {
            UpdateSprayAudio(false);
            hadPreviousHit = false;
            return;
        }

        bool sprayedThisFrame = SprayContinuous();
        UpdateSprayAudio(sprayedThisFrame);
    }

    private void UpdateSprayInput()
    {
        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(controllerNode);

        if (!device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed))
            return;

        if (pressed == isSpraying)
            return;

        isSpraying = pressed;

        if (!isSpraying)
        {
            hadPreviousHit = false;
            UpdateSprayAudio(false);
        }
    }

    public void OnActivated(ActivateEventArgs args)
    {
        isSpraying = true;
    }

    public void OnDeactivated(DeactivateEventArgs args)
    {
        isSpraying = false;
        hadPreviousHit = false;
        UpdateSprayAudio(false);
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

    bool SprayContinuous()
    {
        if (!nozzle) return false;

        if (!Physics.Raycast(nozzle.position, nozzle.forward, out var hit, maxDistance, paintableMask))
        {
            hadPreviousHit = false;
            return false;
        }

        // Only spray if the surface has valid UVs
        if (hit.textureCoord.x <= 0.0001f && hit.textureCoord.y <= 0.0001f)
        {
            hadPreviousHit = false;
            return false;
        }

        Vector2 currentUV = hit.textureCoord;

        if (!hadPreviousHit)
        {
            StampAtUV(currentUV, hit.distance);
            previousHitPoint = hit.point;
            previousHitUV = currentUV;
            previousCollider = hit.collider;
            previousNormal = hit.normal;
            hadPreviousHit = true;
            return true;
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
            return true;
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
        return true;
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

    public void SetSprayColor(Color newColor)
    {
        color = newColor;

        if (targetMaterial == null)
            return;

        if (targetMaterial.HasProperty(PaintColorId))
            targetMaterial.SetColor(PaintColorId, newColor);

        if (targetMaterial.HasProperty(UnderscorePaintColorId))
            targetMaterial.SetColor(UnderscorePaintColorId, newColor);

        if (targetMaterial.HasProperty(BaseColorId))
            targetMaterial.SetColor(BaseColorId, newColor);

        if (targetMaterial.HasProperty(UnderscoreBaseColorId))
            targetMaterial.SetColor(UnderscoreBaseColorId, newColor);

        if (targetMaterial.HasProperty(ColorId))
            targetMaterial.SetColor(ColorId, newColor);

        if (targetMaterial.HasProperty(UnderscoreColorId))
            targetMaterial.SetColor(UnderscoreColorId, newColor);
    }

    private void ConfigureSprayAudio()
    {
        if (sprayAudioSource == null)
            sprayAudioSource = GetComponent<AudioSource>();

        if (sprayAudioSource == null)
            return;

        sprayAudioSource.loop = true;
        sprayAudioSource.playOnAwake = false;
        sprayAudioSource.clip = sprayLoopClip;
        sprayAudioSource.volume = sprayVolume;
        sprayAudioSource.pitch = sprayPitch;
    }

    private void UpdateSprayAudio(bool shouldPlay)
    {
        if (sprayAudioSource == null)
            return;

        if (sprayAudioSource.clip != sprayLoopClip)
            sprayAudioSource.clip = sprayLoopClip;

        sprayAudioSource.volume = sprayVolume;

        if (shouldPlay && sprayLoopClip != null)
        {
            if (!sprayAudioSource.isPlaying)
            {
                float pitchOffset = Random.Range(-sprayPitchRandomness, sprayPitchRandomness);
                sprayAudioSource.pitch = Mathf.Max(0.1f, sprayPitch + pitchOffset);

                if (randomizeSprayStartTime && sprayLoopClip.length > 0f)
                    sprayAudioSource.time = Random.Range(0f, sprayLoopClip.length);
                else
                    sprayAudioSource.time = 0f;

                sprayAudioSource.Play();
            }
        }
        else if (sprayAudioSource.isPlaying)
        {
            sprayAudioSource.Stop();
            sprayAudioSource.pitch = sprayPitch;
        }
    }
}
