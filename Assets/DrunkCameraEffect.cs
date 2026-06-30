using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DefaultExecutionOrder(10000)]
public sealed class DrunkCameraEffect : MonoBehaviour
{
    [SerializeField] private float maxPositionShake = 0.09f;
    [SerializeField] private float maxRotationShake = 2.8f;
    [SerializeField] private float shakeFrequency = 7.5f;
    [SerializeField] private float smoothing = 3.5f;

    private PlayerNeedsManager manager;
    private Volume volume;
    private VolumeProfile runtimeProfile;
    private DepthOfField depthOfField;
    private MotionBlur motionBlur;
    private LensDistortion lensDistortion;
    private Vector3 lastLocalPositionOffset;
    private Quaternion lastLocalRotationOffset = Quaternion.identity;
    private Vector3 lastAppliedWorldPosition;
    private Quaternion lastAppliedWorldRotation;
    private float currentIntensity;
    private bool hasAppliedOffset;

    public void SetManager(PlayerNeedsManager needsManager)
    {
        manager = needsManager;
    }

    private void Awake()
    {
        ConfigurePostProcessing();
    }

    private void OnDestroy()
    {
        if (runtimeProfile != null)
        {
            Destroy(runtimeProfile);
        }
    }

    private void LateUpdate()
    {
        float targetIntensity = manager != null ? manager.Drunk01 : 0f;
        currentIntensity = Mathf.MoveTowards(currentIntensity, targetIntensity, smoothing * Time.deltaTime);

        bool cameraUnchangedSinceLastOffset = hasAppliedOffset
            && Vector3.Distance(transform.position, lastAppliedWorldPosition) < 0.02f
            && Quaternion.Angle(transform.rotation, lastAppliedWorldRotation) < 0.5f;

        if (cameraUnchangedSinceLastOffset)
        {
            transform.localPosition -= lastLocalPositionOffset;
            transform.localRotation *= Quaternion.Inverse(lastLocalRotationOffset);
        }

        ApplyPostProcessing(currentIntensity);

        if (currentIntensity <= 0.001f)
        {
            lastLocalPositionOffset = Vector3.zero;
            lastLocalRotationOffset = Quaternion.identity;
            hasAppliedOffset = false;
            return;
        }

        float time = Time.time * shakeFrequency;
        float shakeStrength = currentIntensity * currentIntensity;

        lastLocalPositionOffset = new Vector3(
            (Mathf.PerlinNoise(time, 0.11f) - 0.5f) * 2f,
            (Mathf.PerlinNoise(0.23f, time) - 0.5f) * 2f,
            0f) * maxPositionShake * shakeStrength;

        lastLocalRotationOffset = Quaternion.Euler(
            (Mathf.PerlinNoise(time, 1.17f) - 0.5f) * maxRotationShake * shakeStrength,
            (Mathf.PerlinNoise(2.31f, time) - 0.5f) * maxRotationShake * shakeStrength,
            (Mathf.PerlinNoise(time, 3.43f) - 0.5f) * maxRotationShake * shakeStrength);

        transform.localPosition += lastLocalPositionOffset;
        transform.localRotation *= lastLocalRotationOffset;

        lastAppliedWorldPosition = transform.position;
        lastAppliedWorldRotation = transform.rotation;
        hasAppliedOffset = true;
    }

    private void ConfigurePostProcessing()
    {
        Camera camera = GetComponent<Camera>();

        if (camera != null)
        {
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            cameraData.renderPostProcessing = true;
        }

        GameObject volumeObject = new GameObject("Drunk Camera Volume");
        volumeObject.transform.SetParent(transform, false);
        volume = volumeObject.AddComponent<Volume>();

        runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeProfile.name = "Runtime Drunk Camera Profile";

        depthOfField = runtimeProfile.Add<DepthOfField>(true);
        depthOfField.mode.Override(DepthOfFieldMode.Gaussian);
        depthOfField.gaussianStart.Override(0.1f);
        depthOfField.gaussianEnd.Override(12f);
        depthOfField.gaussianMaxRadius.Override(0.5f);
        depthOfField.highQualitySampling.Override(true);

        motionBlur = runtimeProfile.Add<MotionBlur>(true);
        motionBlur.mode.Override(MotionBlurMode.CameraOnly);
        motionBlur.quality.Override(MotionBlurQuality.Low);
        motionBlur.intensity.Override(0f);
        motionBlur.clamp.Override(0.04f);

        lensDistortion = runtimeProfile.Add<LensDistortion>(true);
        lensDistortion.intensity.Override(0f);
        lensDistortion.scale.Override(1f);

        volume.isGlobal = true;
        volume.priority = 500f;
        volume.weight = 0f;
        volume.profile = runtimeProfile;
    }

    private void ApplyPostProcessing(float intensity)
    {
        if (volume == null || depthOfField == null || motionBlur == null || lensDistortion == null)
        {
            return;
        }

        float eased = intensity * intensity;

        volume.weight = Mathf.Clamp01(intensity);
        depthOfField.gaussianEnd.Override(Mathf.Lerp(14f, 2.2f, eased));
        depthOfField.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, 1.45f, eased));
        motionBlur.intensity.Override(Mathf.Lerp(0f, 0.55f, eased));
        lensDistortion.intensity.Override(Mathf.Lerp(0f, -0.18f, eased));
        lensDistortion.scale.Override(Mathf.Lerp(1f, 1.08f, eased));
    }
}
