using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public sealed class LaptopInteraction : MonoBehaviour
{
    [SerializeField] private string laptopSceneName = "LaptopScene";
    [SerializeField] private string promptText = "E to Interact";
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float fadeInDuration = 0.4f;

    private Canvas promptCanvas;
    private RectTransform promptRect;
    private Text promptLabel;
    private bool playerInside;
    private bool isLoading;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        CreatePrompt();
        SetPromptVisible(false);
    }

    private void Update()
    {
        bool canInteract = !isLoading && playerInside;
        SetPromptVisible(canInteract);

        if (!canInteract)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            OpenLaptopScene();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null)
        {
            return;
        }

        playerInside = true;
        SetPromptVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null)
        {
            return;
        }

        playerInside = false;
        SetPromptVisible(false);
    }

    private void OpenLaptopScene()
    {
        if (string.IsNullOrWhiteSpace(laptopSceneName))
        {
            Debug.LogWarning($"{nameof(LaptopInteraction)} on {name} has no laptop scene assigned.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(laptopSceneName))
        {
            Debug.LogWarning($"{nameof(LaptopInteraction)} could not load scene '{laptopSceneName}'. Add it to Build Settings first.", this);
            return;
        }

        isLoading = true;
        SetPromptVisible(false);
        SceneFadeTransition.LoadScene(laptopSceneName, fadeOutDuration, fadeInDuration, Color.black);
    }

    private void CreatePrompt()
    {
        promptCanvas = new GameObject("Laptop Interaction Prompt", typeof(RectTransform)).AddComponent<Canvas>();
        promptCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        promptCanvas.sortingOrder = 1300;
        promptCanvas.transform.SetParent(transform, false);

        GameObject labelObject = new GameObject("Prompt", typeof(RectTransform));
        labelObject.transform.SetParent(promptCanvas.transform, false);

        Image background = labelObject.AddComponent<Image>();
        background.color = new Color(0.03f, 0.08f, 0.1f, 0.86f);
        background.raycastTarget = false;

        promptRect = labelObject.GetComponent<RectTransform>();
        promptRect.anchorMin = new Vector2(0.5f, 0.18f);
        promptRect.anchorMax = new Vector2(0.5f, 0.18f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = Vector2.zero;
        promptRect.sizeDelta = new Vector2(240f, 58f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform));
        textObject.transform.SetParent(labelObject.transform, false);

        promptLabel = textObject.AddComponent<Text>();
        promptLabel.font = UiFontUtility.DefaultFont;
        promptLabel.fontSize = 25;
        promptLabel.fontStyle = FontStyle.Bold;
        promptLabel.alignment = TextAnchor.MiddleCenter;
        promptLabel.color = new Color(0.72f, 1f, 0.94f, 1f);
        promptLabel.text = promptText;
        promptLabel.raycastTarget = false;

        RectTransform textRect = promptLabel.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptCanvas != null)
        {
            promptCanvas.gameObject.SetActive(visible);
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider boxCollider = GetComponent<BoxCollider>();

        if (boxCollider == null)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.2f, 1f, 0.75f, 0.8f);
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        Gizmos.matrix = previousMatrix;
    }
}
