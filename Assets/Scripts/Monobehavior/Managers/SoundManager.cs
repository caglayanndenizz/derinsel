using UnityEngine;

/// <summary>
/// Centralized one-shot SFX + looping music playback. Auto-creates itself if no instance
/// exists yet (mirrors HitStopManager's lazy-singleton pattern) — any script can call
/// SoundManager.PlaySfx(clip) without holding a scene reference.
///
/// Music tracks live here too, as named clips (Menu, Game, ...) assigned once in the
/// Inspector on the one SoundManager instance. Any scene just calls e.g.
/// SoundManager.PlayMenuMusic() / SoundManager.PlayGameMusic() to switch tracks — no clip
/// references need to be duplicated per scene.
///
/// This component only ever needs to exist ONCE, for the whole game (DontDestroyOnLoad).
/// Do NOT place one per scene — a second instance destroys itself in Awake, taking any
/// Inspector-assigned clips with it.
///
/// [DefaultExecutionOrder(-1000)]: Unity does not guarantee Awake() order between different
/// GameObjects. Without forcing this one early, MainMenuController/LevelManager's Awake()
/// could call PlayMenuMusic()/PlayGameMusic() BEFORE this object's own Awake() has run —
/// EnsureInstance() would then spin up a second, blank SoundManager, and when the real
/// (Inspector-configured) one's Awake() finally runs, it finds Instance already taken and
/// destroys itself, silently discarding the assigned music clips.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("SFX")]
    [Tooltip("Optional override — leave empty, an AudioSource is added automatically. Only assign your own if you need custom routing (e.g. an Audio Mixer Group).")]
    [SerializeField] private AudioSource sfxSource;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

    [Header("Music")]
    [Tooltip("Optional override — leave empty, an AudioSource is added automatically. Only assign your own if you need custom routing (e.g. an Audio Mixer Group).")]
    [SerializeField] private AudioSource musicSource;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.6f;

    [Header("Music Library")]
    [Tooltip("Played via SoundManager.PlayMenuMusic().")]
    [SerializeField] private AudioClip menuMusic;
    [Tooltip("Played via SoundManager.PlayGameMusic().")]
    [SerializeField] private AudioClip gameMusic;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }
    }

    /// <summary>Fire-and-forget SFX. Safe to call from anywhere — creates the manager on first use.</summary>
    public static void PlaySfx(AudioClip clip, float volumeMult = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        EnsureInstance();

        float previousPitch = Instance.sfxSource.pitch;
        Instance.sfxSource.pitch = pitch;
        Instance.sfxSource.PlayOneShot(clip, Instance.sfxVolume * Mathf.Clamp01(volumeMult));
        Instance.sfxSource.pitch = previousPitch;
    }

    /// <summary>Starts looping background music. No-ops if the same clip is already playing.</summary>
    public static void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null) return;
        EnsureInstance();

        if (Instance.musicSource.clip == clip && Instance.musicSource.isPlaying) return;
        Instance.musicSource.clip = clip;
        Instance.musicSource.loop = loop;
        Instance.musicSource.volume = Instance.musicVolume;
        Instance.musicSource.Play();
    }

    public static void StopMusic()
    {
        if (Instance == null) return;
        Instance.musicSource.Stop();
    }

    /// <summary>Starts the Menu Music clip assigned on the SoundManager instance.</summary>
    public static void PlayMenuMusic(bool loop = true)
    {
        EnsureInstance();
        PlayMusic(Instance.menuMusic, loop);
    }

    /// <summary>Starts the Game Music clip assigned on the SoundManager instance.</summary>
    public static void PlayGameMusic(bool loop = true)
    {
        EnsureInstance();
        PlayMusic(Instance.gameMusic, loop);
    }

    private static void EnsureInstance()
    {
        if (Instance != null) return;
        GameObject managerObject = new GameObject("SoundManager");
        Instance = managerObject.AddComponent<SoundManager>();
    }
}
