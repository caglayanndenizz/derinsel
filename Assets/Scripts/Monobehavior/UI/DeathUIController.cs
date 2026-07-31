using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DeathUIController : MonoBehaviour
{
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private TransitionFader fader;
    [SerializeField] private string gameSceneName = "Game";
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private Player player;

    private void Awake()
    {
        retryButton.onClick.AddListener(OnRetryClicked);
        mainMenuButton.onClick.AddListener(OnMainMenuClicked);

        deathPanel.SetActive(false);

        if (player == null)
            player = FindAnyObjectByType<Player>();

        if (player != null)
            player.Died += ShowDeathPanel;
    }

    private void ShowDeathPanel()
    {
        deathPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    public void OnRetryClicked()
    {
        StartCoroutine(ReloadScene(gameSceneName));
    }

    public void OnMainMenuClicked()
    {
        StartCoroutine(ReloadScene(mainMenuSceneName));
    }

    private IEnumerator ReloadScene(string sceneName)
    {
        Time.timeScale = 1f;

        if (fader != null)
            yield return fader.FadeTo(1f);

        SceneManager.LoadScene(sceneName);
    }
}
