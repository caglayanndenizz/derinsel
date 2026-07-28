using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private TransitionFader fader;
    [SerializeField] private string gameSceneName = "Game";

    [Header("Main Panel Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        optionsButton.onClick.AddListener(OnOptionsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        ShowMainPanel();
    }

    public void OnPlayClicked()
    {
        StartCoroutine(LoadGameScene());
    }

    public void OnOptionsClicked()
    {
        mainPanel.SetActive(false);
        optionsPanel.SetActive(true);
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        optionsPanel.SetActive(false);
    }

    private IEnumerator LoadGameScene()
    {
        if (fader != null)
            yield return fader.FadeTo(1f);

        SceneManager.LoadScene(gameSceneName);
    }
}
