using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

public class CameraCrosshairIndicator : MonoBehaviour
{
    [Header("References")]
    public Camera mainCamera;
    public CameraManager cameraManager;
    public PlayerController playerController;

    [Header("Indicator Settings")]
    public GameObject indicatorPrefab; // Prefab to instantiate for indicators
    public float indicatorSize = 0.2f;
    public Color aimIndicatorColor = Color.red; // Color for aim indicator
    public Color targetIndicatorColor = Color.cyan; // Color for 2.5D target indicator
    public LayerMask hitLayers; // What layers can be hit
    public float maxRaycastDistance = 100f;
    public float isometricIndicatorDistance = 3f; // Distance in front of player in isometric mode

    [Header("Visual Options")]
    public bool showIndicator = true;
    public bool updateEveryFrame = true; // Move with aim point constantly

    [Header("Isometric UI Crosshair")]
    public bool showIsometricCrosshair = true;
    public Color isometricCrosshairColor = Color.white;
    public float isometricCrosshairSize = 8f;
    public float isometricCrosshairAlpha = 0.8f;

    private GameObject aimIndicator; // Red indicator for aiming
    private Renderer aimIndicatorRenderer;

    // Screen center indicator (green)
    private GameObject screenCenterIndicator;
    private Renderer screenCenterRenderer;
    private Vector3 screenCenterHitPoint;
    private bool hasScreenCenterHit = false;

    // Target location indicator (cyan) - shows where player is moving to in 2.5D mode
    private GameObject targetLocationIndicator;
    private Renderer targetLocationRenderer;

    // UI Crosshair for isometric mode
    private GameObject crosshairUI;
    private Image crosshairImage;
    private Canvas canvas;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (cameraManager == null)
            cameraManager = FindObjectOfType<CameraManager>();

        // Instantiate all indicators
        CreateAimIndicator();
        CreateScreenCenterIndicator();
        CreateTargetLocationIndicator();
        CreateIsometricCrosshair();
    }

    void CreateAimIndicator()
    {
        if (indicatorPrefab != null)
        {
            // Instantiate from prefab
            aimIndicator = Instantiate(indicatorPrefab);
            aimIndicator.name = "AimIndicator";
            aimIndicator.transform.localScale = Vector3.one * indicatorSize;

            aimIndicatorRenderer = aimIndicator.GetComponent<Renderer>();
            if (aimIndicatorRenderer != null)
            {
                aimIndicatorRenderer.material.color = aimIndicatorColor;
            }
        }
        else
        {
            // Create a simple sphere
            aimIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            aimIndicator.name = "AimIndicator";

            // Remove collider so it doesn't interfere with raycasts
            Destroy(aimIndicator.GetComponent<Collider>());

            // Set size
            aimIndicator.transform.localScale = Vector3.one * indicatorSize;

            // Create and apply material with color
            aimIndicatorRenderer = aimIndicator.GetComponent<Renderer>();
            Material mat = CreateIndicatorMaterial(aimIndicatorColor);
            aimIndicatorRenderer.material = mat;
        }

        aimIndicator.SetActive(showIndicator);
    }

    void CreateScreenCenterIndicator()
    {
        if (indicatorPrefab != null)
        {
            // Instantiate from prefab
            screenCenterIndicator = Instantiate(indicatorPrefab);
            screenCenterIndicator.name = "ScreenCenterIndicator";
            screenCenterIndicator.transform.localScale = Vector3.one * indicatorSize * 0.8f;

            screenCenterRenderer = screenCenterIndicator.GetComponent<Renderer>();
            if (screenCenterRenderer != null)
            {
                screenCenterRenderer.material.color = Color.green;
            }
        }
        else
        {
            // Create a sphere for screen center indicator
            screenCenterIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            screenCenterIndicator.name = "ScreenCenterIndicator";

            // Remove collider so it doesn't interfere with raycasts
            Destroy(screenCenterIndicator.GetComponent<Collider>());

            // Set size (slightly smaller than main indicator)
            screenCenterIndicator.transform.localScale = Vector3.one * indicatorSize * 0.8f;

            // Create and apply material with green color
            screenCenterRenderer = screenCenterIndicator.GetComponent<Renderer>();
            Material mat = CreateIndicatorMaterial(Color.green);
            screenCenterRenderer.material = mat;
        }

        screenCenterIndicator.SetActive(false);
    }

    void CreateTargetLocationIndicator()
    {
        if (indicatorPrefab != null)
        {
            // Instantiate from prefab
            targetLocationIndicator = Instantiate(indicatorPrefab);
            targetLocationIndicator.name = "TargetLocationIndicator";
            targetLocationIndicator.transform.localScale = Vector3.one * indicatorSize * 1.2f;

            targetLocationRenderer = targetLocationIndicator.GetComponent<Renderer>();
            if (targetLocationRenderer != null)
            {
                targetLocationRenderer.material.color = targetIndicatorColor;
            }
        }
        else
        {
            // Create a sphere for target location indicator
            targetLocationIndicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            targetLocationIndicator.name = "TargetLocationIndicator";

            // Remove collider so it doesn't interfere with raycasts
            Destroy(targetLocationIndicator.GetComponent<Collider>());

            // Set size (slightly larger than aim indicator)
            targetLocationIndicator.transform.localScale = Vector3.one * indicatorSize * 1.2f;

            // Create and apply material with cyan color
            targetLocationRenderer = targetLocationIndicator.GetComponent<Renderer>();
            Material mat = CreateIndicatorMaterial(targetIndicatorColor);
            targetLocationRenderer.material = mat;
        }

        targetLocationIndicator.SetActive(false);
    }

    Material CreateIndicatorMaterial(Color color)
    {
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        // If URP shader not found, try Standard
        if (mat.shader.name == "Hidden/InternalErrorShader")
        {
            mat = new Material(Shader.Find("Standard"));
        }

        // If Standard not found, use Unlit/Color (always available)
        if (mat.shader.name == "Hidden/InternalErrorShader")
        {
            mat = new Material(Shader.Find("Unlit/Color"));
        }

        mat.color = color;

        // Try to set metallic/smoothness if shader supports it
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", 0.5f);
        if (mat.HasProperty("_Glossiness"))
            mat.SetFloat("_Glossiness", 0.8f);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", 0.8f);

        return mat;
    }

    void Update()
    {
        // Find the local player if we don't have one
        if (playerController == null)
        {
            FindLocalPlayer();
        }

        if (cameraManager == null || cameraManager.player == null)
            return;

        bool isThirdPerson = cameraManager.GetCurrentMode() == CameraManager.CameraMode.ThirdPerson;

        // Show 3D world indicator in BOTH modes
        if (showIndicator && aimIndicator != null && updateEveryFrame)
        {
            UpdateAimIndicatorPosition();
        }
        else if (aimIndicator != null)
        {
            aimIndicator.SetActive(false);
        }

        // Update screen center indicator
        UpdateScreenCenterIndicator();

        // Update target location indicator (2.5D go-to location)
        UpdateTargetLocationIndicator();

        // Hide UI crosshair - we don't need it
        if (crosshairUI != null)
        {
            crosshairUI.SetActive(false);
        }
    }

    void FindLocalPlayer()
    {
        // Find all PlayerControllers
        PlayerController[] players = FindObjectsOfType<PlayerController>();

        foreach (PlayerController player in players)
        {
            // Find the one that is owned by this client
            if (player.IsOwner)
            {
                playerController = player;
                Debug.Log("CameraCrosshairIndicator found local player!");
                break;
            }
        }
    }

    void UpdateScreenCenterIndicator()
    {
        // Cast ray from center of screen to ground
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRaycastDistance, hitLayers))
        {
            screenCenterHitPoint = hit.point;
            hasScreenCenterHit = true;

            // Position indicator at hit point with small upward offset
            screenCenterIndicator.transform.position = hit.point + Vector3.up * 0.02f;
            screenCenterIndicator.SetActive(true);
        }
        else
        {
            hasScreenCenterHit = false;
            screenCenterIndicator.SetActive(false);
        }
    }

    void UpdateTargetLocationIndicator()
    {
        if (playerController == null)
        {
            targetLocationIndicator.SetActive(false);
            return;
        }

        bool isIsometric = cameraManager.GetCurrentMode() == CameraManager.CameraMode.Isometric;

        // Only show target location indicator in isometric mode when player is moving
        if (isIsometric && playerController.isMovingToTarget)
        {
            Vector3 targetPos = playerController.clickMoveTarget;

            // Raycast down from above the target position to find the ground
            RaycastHit hit;
            if (Physics.Raycast(targetPos + Vector3.up * 10f, Vector3.down, out hit, 20f, hitLayers))
            {
                targetLocationIndicator.transform.position = hit.point + Vector3.up * 0.05f;
                targetLocationIndicator.SetActive(true);
            }
            else
            {
                // Fallback - just use target position
                targetLocationIndicator.transform.position = targetPos + Vector3.up * 0.05f;
                targetLocationIndicator.SetActive(true);
            }
        }
        else
        {
            targetLocationIndicator.SetActive(false);
        }
    }

    void UpdateAimIndicatorPosition()
    {
        if (playerController == null)
            return;

        bool isIsometric = cameraManager.GetCurrentMode() == CameraManager.CameraMode.Isometric;

        // In isometric mode, position indicator in front of player
        if (isIsometric)
        {
            // Check if player is moving to a target
            Vector3 targetPos = playerController.clickMoveTarget;
            bool isMoving = playerController.isMovingToTarget;

            if (isMoving)
            {
                // Calculate direction from player to target
                Vector3 playerPos = playerController.transform.position;
                Vector3 directionToTarget = (targetPos - playerPos).normalized;
                directionToTarget.y = 0; // Keep on horizontal plane

                // Position indicator in front of player by specified distance
                Vector3 indicatorPos = playerPos + directionToTarget * isometricIndicatorDistance;

                // Raycast down to find ground surface
                RaycastHit hit;
                if (Physics.Raycast(indicatorPos + Vector3.up * 10f, Vector3.down, out hit, 20f, hitLayers))
                {
                    // Position indicator on ground with small upward offset to avoid z-fighting
                    aimIndicator.transform.position = hit.point + Vector3.up * 0.01f;
                    aimIndicator.SetActive(true);
                }
                else
                {
                    // Fallback if no ground found - use player's Y position
                    indicatorPos.y = playerPos.y;
                    aimIndicator.transform.position = indicatorPos + Vector3.up * 0.01f;
                    aimIndicator.SetActive(true);
                }
            }
            else
            {
                aimIndicator.SetActive(false);
            }
        }
        else
        {
            // In third person mode, use raycast from center of screen
            Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, maxRaycastDistance, hitLayers))
            {
                // Position indicator at hit point with small upward offset to avoid z-fighting
                aimIndicator.transform.position = hit.point + Vector3.up * 0.01f;
                aimIndicator.SetActive(true);
            }
            else
            {
                // No hit - hide indicator
                aimIndicator.SetActive(false);
            }
        }
    }

    void CreateIsometricCrosshair()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("IsometricCrosshairCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Create crosshair dot
        crosshairUI = new GameObject("IsometricCrosshair");
        crosshairUI.transform.SetParent(canvasObj.transform, false);

        crosshairImage = crosshairUI.AddComponent<Image>();

        // Create a circular texture for the dot
        Texture2D texture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        Vector2 center = new Vector2(16, 16);

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= 8f)
                {
                    Color col = isometricCrosshairColor;
                    col.a = isometricCrosshairAlpha;
                    pixels[y * 32 + x] = col;
                }
                else
                {
                    pixels[y * 32 + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
        crosshairImage.sprite = sprite;

        // Position in center and set size
        RectTransform rectTransform = crosshairUI.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(isometricCrosshairSize, isometricCrosshairSize);

        crosshairUI.SetActive(false); // Start hidden
    }

    // Public method to get the current aim point (useful for shooting, targeting, etc.)
    public Vector3 GetAimPoint()
    {
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRaycastDistance, hitLayers))
        {
            return hit.point;
        }

        // If nothing hit, return a point far in front of camera
        return ray.GetPoint(maxRaycastDistance);
    }

    // Public method to get what we're aiming at
    public GameObject GetAimTarget()
    {
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRaycastDistance, hitLayers))
        {
            return hit.collider.gameObject;
        }

        return null;
    }

    // Toggle indicator visibility
    public void ToggleIndicator()
    {
        showIndicator = !showIndicator;
        if (aimIndicator != null)
            aimIndicator.SetActive(showIndicator);
    }

    // Change indicator color (useful for different states: friendly, enemy, etc.)
    public void SetIndicatorColor(Color color)
    {
        aimIndicatorColor = color;
        if (aimIndicatorRenderer != null)
        {
            aimIndicatorRenderer.material.color = color;
        }
    }

    // Get the position of the aim indicator (for camera to aim at)
    public Vector3 GetIndicatorPosition()
    {
        if (aimIndicator != null && aimIndicator.activeSelf)
        {
            return aimIndicator.transform.position;
        }
        // Fallback to player position if indicator is not active
        if (playerController != null)
        {
            return playerController.transform.position;
        }
        return Vector3.zero;
    }

    // Check if the indicator is currently active
    public bool IsIndicatorActive()
    {
        return aimIndicator != null && aimIndicator.activeSelf;
    }

    // Draw gizmos in the editor for visualization
    void OnDrawGizmos()
    {
        if (mainCamera == null)
            return;

        // Draw ray from screen center
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));

        // Set gizmo color based on hit
        if (hasScreenCenterHit)
        {
            // Draw green line from camera to hit point
            Gizmos.color = Color.green;
            Gizmos.DrawLine(ray.origin, screenCenterHitPoint);

            // Draw a small sphere at the hit point
            Gizmos.DrawWireSphere(screenCenterHitPoint, 0.3f);
        }
        else
        {
            // Draw red line showing no hit
            Gizmos.color = Color.red;
            Gizmos.DrawRay(ray.origin, ray.direction * 50f);
        }
    }

    void OnDestroy()
    {
        // Clean up instantiated indicators
        if (aimIndicator != null)
            Destroy(aimIndicator);
        if (screenCenterIndicator != null)
            Destroy(screenCenterIndicator);
        if (targetLocationIndicator != null)
            Destroy(targetLocationIndicator);
    }
}