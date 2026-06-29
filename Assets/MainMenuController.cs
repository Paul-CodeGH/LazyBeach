using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class MainMenuController : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private string startSceneName = "SampleScene";

    private void Awake()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsureEventSystem();

        if (startButton != null)
        {
            startButton.onClick.AddListener(StartGame);
        }

        if (exitButton != null)
        {
            exitButton.onClick.AddListener(ExitGame);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(StartGame);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(ExitGame);
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(startSceneName);
    }

    public void ExitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
