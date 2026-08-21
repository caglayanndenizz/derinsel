using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private TransitionFader fader;
    [SerializeField] private string gameSceneName = "Game";

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    [Header("Sound")]
    private AudioSource _audioSource;
    public AudioClip menuMusic;

    private void Awake()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;
        if (menuMusic != null)
        {
            _audioSource.clip = menuMusic;
            _audioSource.Play();
        }
    }

    public void OnPlayClicked()
    {
        StartCoroutine(LoadGameScene());
    }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator LoadGameScene()
    {
        if (fader != null)
            yield return fader.FadeTo(1f);

        SceneManager.LoadScene(gameSceneName);
    }
}
