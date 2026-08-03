using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private TransitionFader fader;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Pause Panel Buttons")]
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitToMenuButton;
    [SerializeField] private Button quitGameButton;

    private bool _isPaused;

    private void Awake()
    {
        optionsButton.onClick.AddListener(OpenOptions);
        quitToMenuButton.onClick.AddListener(OnQuitToMenuClicked);
        quitGameButton.onClick.AddListener(OnQuitGameClicked);

        pausePanel.SetActive(false);
        optionsPanel.SetActive(false);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        // Shop panel owns Escape while it's open and closes itself; don't also toggle pause.
        if (AugmentShopPanel.IsAnyOpen)
            return;

        if (optionsPanel.activeSelf)
            CloseOptions();
        else
            SetPaused(!_isPaused);
    }

    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        pausePanel.SetActive(paused);
        Time.timeScale = paused ? 0f : 1f;
    }

    public void OpenOptions()
    {
        pausePanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        optionsPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    public void OnQuitToMenuClicked()
    {
        StartCoroutine(QuitToMainMenu());
    }

    public void OnQuitGameClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator QuitToMainMenu()
    {
        Time.timeScale = 1f;

        if (fader != null)
            yield return fader.FadeTo(1f);

        SceneManager.LoadScene(mainMenuSceneName);
    }
}
