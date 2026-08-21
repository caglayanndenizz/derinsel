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
    [Tooltip("Olum animasyonu bittikten sonra death panel'in ekrana gelmesine kadar beklenecek sure (saniye).")]
    [SerializeField] private float showDelayAfterDeath = 2f;

    private void Awake()
    {
        retryButton.onClick.AddListener(OnRetryClicked);
        mainMenuButton.onClick.AddListener(OnMainMenuClicked);

        deathPanel.SetActive(false);

        if (player == null)
            player = FindAnyObjectByType<Player>();

        if (player != null)
            player.Died += OnPlayerDied;
    }

    private void OnPlayerDied()
    {
        StartCoroutine(ShowDeathPanelAfterDelay());
    }

    private IEnumerator ShowDeathPanelAfterDelay()
    {
        yield return new WaitForSeconds(showDelayAfterDeath);
        deathPanel.SetActive(true);
        LevelManager.Instance?.FadeOutMusic(3f);
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
