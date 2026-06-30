using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class WeatherManager : MonoBehaviour
{
    private const string SampleSceneName = "SampleScene";

    public static WeatherManager Instance { get; private set; }

    [SerializeField] private Vector2 dryDurationRange = new Vector2(60f, 180f);
    [SerializeField] private Vector2 rainDurationRange = new Vector2(45f, 120f);
    [SerializeField] private Vector2 rainEmissionRange = new Vector2(450f, 1100f);
    [SerializeField] private Vector2 splashEmissionRange = new Vector2(70f, 180f);
    [SerializeField] private Vector2 rainAreaSize = new Vector2(34f, 28f);
    [SerializeField] private float rainHeightAboveCamera = 12f;
    [SerializeField] private float rainForwardOffset = 7f;
    [SerializeField] private float splashGroundOffset = 2.4f;
    [SerializeField] private float rainFallSpeed = 27f;

    private GameObject weatherRoot;
    private ParticleSystem rainParticles;
    private ParticleSystem splashParticles;
    private Material rainMaterial;
    private Material splashMaterial;
    private Camera activeCamera;
    private bool isSampleSceneActive;
    private bool isRaining;
    private float weatherTimer;
    private float currentRainIntensity;

    public bool IsRaining => isRaining;
    public float TimeUntilWeatherChange => weatherTimer;

    public static void EnsureExists()
    {
        if (Instance != null)
        {
            return;
        }

        new GameObject(nameof(WeatherManager)).AddComponent<WeatherManager>();
    }

    public void ResetForNewGame()
    {
        isRaining = false;
        currentRainIntensity = 0f;
        activeCamera = null;
        ScheduleDryPeriod();
        UpdateRainEmission();
        UpdateRainVisibility();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateRainRig();
        ScheduleDryPeriod();
        SceneManager.sceneLoaded += OnSceneLoaded;
        UpdateSceneState(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }

        if (rainMaterial != null)
        {
            Destroy(rainMaterial);
        }

        if (splashMaterial != null)
        {
            Destroy(splashMaterial);
        }
    }

    private void Update()
    {
        weatherTimer -= Time.deltaTime;

        if (weatherTimer <= 0f)
        {
            if (isRaining)
            {
                StopRain();
            }
            else
            {
                StartRain();
            }
        }

        if (isSampleSceneActive && isRaining)
        {
            FollowActiveCamera();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateSceneState(scene);
    }

    private void UpdateSceneState(Scene scene)
    {
        isSampleSceneActive = scene.name == SampleSceneName;
        activeCamera = null;
        UpdateRainVisibility();
    }

    private void StartRain()
    {
        isRaining = true;
        currentRainIntensity = Random.Range(0.55f, 1f);
        weatherTimer = Random.Range(rainDurationRange.x, rainDurationRange.y);
        UpdateRainEmission();
        UpdateRainVisibility();
    }

    private void StopRain()
    {
        isRaining = false;
        currentRainIntensity = 0f;
        ScheduleDryPeriod();
        UpdateRainEmission();
        UpdateRainVisibility();
    }

    private void ScheduleDryPeriod()
    {
        weatherTimer = Random.Range(dryDurationRange.x, dryDurationRange.y);
    }

    private void UpdateRainVisibility()
    {
        if (weatherRoot == null)
        {
            return;
        }

        bool shouldRender = isSampleSceneActive && isRaining;

        if (weatherRoot.activeSelf != shouldRender)
        {
            weatherRoot.SetActive(shouldRender);
        }

        if (shouldRender)
        {
            FollowActiveCamera();
            PlayRainParticles();
            return;
        }

        StopRainParticles();
    }

    private void FollowActiveCamera()
    {
        if (activeCamera == null || !activeCamera.isActiveAndEnabled || activeCamera.gameObject.scene != SceneManager.GetActiveScene())
        {
            activeCamera = FindActiveSceneCamera();
        }

        if (activeCamera == null)
        {
            return;
        }

        Vector3 cameraPosition = activeCamera.transform.position;
        Vector3 cameraForward = Vector3.ProjectOnPlane(activeCamera.transform.forward, Vector3.up);

        if (cameraForward.sqrMagnitude < 0.001f)
        {
            cameraForward = Vector3.forward;
        }

        cameraForward.Normalize();

        rainParticles.transform.position = cameraPosition + cameraForward * rainForwardOffset + Vector3.up * rainHeightAboveCamera;
        splashParticles.transform.position = cameraPosition + cameraForward * rainForwardOffset - Vector3.up * splashGroundOffset;
    }

    private Camera FindActiveSceneCamera()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Camera mainCamera = Camera.main;

        if (mainCamera != null && mainCamera.isActiveAndEnabled && mainCamera.gameObject.scene == activeScene)
        {
            return mainCamera;
        }

        Camera[] cameras = Camera.allCameras;

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];

            if (camera != null && camera.isActiveAndEnabled && camera.gameObject.scene == activeScene)
            {
                return camera;
            }
        }

        return null;
    }

    private void PlayRainParticles()
    {
        if (rainParticles != null && !rainParticles.isPlaying)
        {
            rainParticles.Play();
        }

        if (splashParticles != null && !splashParticles.isPlaying)
        {
            splashParticles.Play();
        }
    }

    private void StopRainParticles()
    {
        if (rainParticles != null)
        {
            rainParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (splashParticles != null)
        {
            splashParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void UpdateRainEmission()
    {
        if (rainParticles == null || splashParticles == null)
        {
            return;
        }

        float rainRate = isRaining ? Mathf.Lerp(rainEmissionRange.x, rainEmissionRange.y, currentRainIntensity) : 0f;
        float splashRate = isRaining ? Mathf.Lerp(splashEmissionRange.x, splashEmissionRange.y, currentRainIntensity) : 0f;

        ParticleSystem.EmissionModule rainEmission = rainParticles.emission;
        rainEmission.rateOverTime = rainRate;

        ParticleSystem.EmissionModule splashEmission = splashParticles.emission;
        splashEmission.rateOverTime = splashRate;
    }

    private void CreateRainRig()
    {
        weatherRoot = new GameObject("Procedural Rain Effects");
        weatherRoot.transform.SetParent(transform, false);
        weatherRoot.SetActive(false);

        rainMaterial = CreateParticleMaterial("Procedural Rain Material", new Color(0.58f, 0.72f, 0.88f, 0.48f));
        splashMaterial = CreateParticleMaterial("Procedural Rain Splash Material", new Color(0.66f, 0.78f, 0.95f, 0.35f));

        rainParticles = CreateRainParticleSystem(weatherRoot.transform);
        splashParticles = CreateSplashParticleSystem(weatherRoot.transform);

    }

    private ParticleSystem CreateRainParticleSystem(Transform parent)
    {
        GameObject rainObject = new GameObject("Falling Rain");
        rainObject.transform.SetParent(parent, false);

        ParticleSystem particles = rainObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 4200;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.25f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.58f, 0.72f, 0.88f, 0.34f), new Color(0.78f, 0.88f, 1f, 0.56f));

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(rainAreaSize.x, 1f, rainAreaSize.y);

        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0.65f);
        velocity.y = new ParticleSystem.MinMaxCurve(-rainFallSpeed);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.35f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.18f;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.35f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 3.2f;
        renderer.velocityScale = 0.08f;
        renderer.minParticleSize = 0.001f;
        renderer.maxParticleSize = 0.04f;
        renderer.material = rainMaterial;

        return particles;
    }

    private ParticleSystem CreateSplashParticleSystem(Transform parent)
    {
        GameObject splashObject = new GameObject("Rain Splashes");
        splashObject.transform.SetParent(parent, false);

        ParticleSystem particles = splashObject.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 4f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 900;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.66f, 0.78f, 0.95f, 0.1f), new Color(0.8f, 0.92f, 1f, 0.28f));
        main.gravityModifier = 1.2f;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(rainAreaSize.x * 0.85f, 0.15f, rainAreaSize.y * 0.85f);

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.minParticleSize = 0.001f;
        renderer.maxParticleSize = 0.025f;
        renderer.material = splashMaterial;

        return particles;
    }

    private static Material CreateParticleMaterial(string materialName, Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");

        if (shader == null)
        {
            shader = Shader.Find("Particles/Standard Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        Material material = new Material(shader)
        {
            name = materialName
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }

        return material;
    }
}
