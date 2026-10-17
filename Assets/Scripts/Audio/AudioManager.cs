using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Every one-shot / looping sound effect the game can play.
// The audio files live in Assets/Resources/Audio (see the Defs table below for the mapping).
public enum Sfx
{
    SwordSwing,
    Hit,
    EnemyDeath,
    KnightHurt,
    KnightDeath,
    TreeHit,
    TreeBreak,
    StoneHit,
    StoneBreak,
    ItemPickup,
    CoinCollect,
    EquipItem,
    HotbarSwitch,
    InventoryOpen,
    GamePause,
    GameSaved,
    LevelClear,
    Construction,
    Teleport,
    UIClick,
    UIHover,
    PortalHum,
    FootstepWalk,
    FootstepRun
}

public enum FootstepMode
{
    None,
    Walk,
    Run
}

// Central audio system. It creates itself automatically before the first scene loads,
// survives scene changes (DontDestroyOnLoad) and needs no setup in the scene.
//
// Usage from any script:
//     AudioManager.Play(Sfx.Hit);
//     AudioManager.SetFootsteps(FootstepMode.Walk);
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // Folder inside any "Resources" directory that holds the clips.
    private const string Folder = "Audio/";

    // ---- Tweak these defaults to taste ----
    private float masterVolume = 1f;
    private float musicVolume = 0.5f;
    private float ambienceVolume = 0.35f;
    private float sfxVolume = 1f;
    private float fadeTime = 1f;          // seconds for the music crossfade between scenes
    private float pausedMusicScale = 0.3f; // music/ambience loudness while the game is paused

    // Scene name -> (music file, ambience file). A null entry means "nothing".
    private static readonly Dictionary<string, (string music, string ambience)> SceneAudio =
        new Dictionary<string, (string, string)>
        {
            { "MainMenu",  ("BGM Main Menu", null) },
            { "MainTown",  ("BGM Town", "wind breeze loop") },
            { "MainLevel", ("BGM Dungeon", "dungeon ambient loop") },
        };

    // Sfx -> (file name without extension, relative volume).
    private static readonly Dictionary<Sfx, (string file, float volume)> Defs =
        new Dictionary<Sfx, (string, float)>
        {
            { Sfx.SwordSwing,    ("Sword swing", 0.8f) },
            { Sfx.Hit,           ("Hit", 0.9f) },
            { Sfx.EnemyDeath,    ("Death Enemy", 0.9f) },
            { Sfx.KnightHurt,    ("Knight kena damage", 1f) },
            { Sfx.KnightDeath,   ("Knight mati", 1f) },
            { Sfx.TreeHit,       ("Tebang pohon", 0.9f) },
            { Sfx.TreeBreak,     ("pohon pecah", 1f) },
            { Sfx.StoneHit,      ("pickaxe hit stone", 0.9f) },
            { Sfx.StoneBreak,    ("stone shatter", 1f) },
            { Sfx.ItemPickup,    ("item pickup", 0.5f) },
            { Sfx.CoinCollect,   ("coin collect", 0.7f) },
            { Sfx.EquipItem,     ("equip item", 0.7f) },
            { Sfx.HotbarSwitch,  ("Ganti Slot Hotbar", 0.6f) },
            { Sfx.InventoryOpen, ("inventory open", 0.7f) },
            { Sfx.GamePause,     ("game pause", 0.7f) },
            { Sfx.GameSaved,     ("game saved", 0.8f) },
            { Sfx.LevelClear,    ("level clear sound", 1f) },
            { Sfx.Construction,  ("construction sound", 0.9f) },
            { Sfx.Teleport,      ("teleportation", 1f) },
            { Sfx.UIClick,       ("ui click", 0.7f) },
            { Sfx.UIHover,       ("ui hover", 0.4f) },
            { Sfx.PortalHum,     ("portal hum loop", 0.6f) },
            { Sfx.FootstepWalk,  ("footsteps grass walk", 0.5f) },
            { Sfx.FootstepRun,   ("footsteps grass run", 0.5f) },
        };

    private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();

    private AudioSource musicSource;
    private AudioSource ambienceSource;
    private AudioSource footstepSource;
    private AudioSource sfxSource;

    private FootstepMode footstepMode = FootstepMode.None;
    private string currentMusic;
    private string currentAmbience;
    private float fade;   // 0..1, driven by the crossfade coroutine
    private float duck = 1f; // 1..pausedMusicScale, driven by the pause state
    private Coroutine switchRoutine;

    public static float SfxVolume => Instance != null ? Instance.masterVolume * Instance.sfxVolume : 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();
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

        musicSource = CreateSource(true);
        ambienceSource = CreateSource(true);
        footstepSource = CreateSource(true);
        sfxSource = CreateSource(false);

        // Load all short effects up front so the first hit/swing does not stutter.
        foreach (var def in Defs.Values)
        {
            LoadClip(def.file);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // plain 2D sound
        return source;
    }

    private void Update()
    {
        bool paused = Time.timeScale == 0f;

        // Lower the music while paused.
        duck = Mathf.MoveTowards(duck, paused ? pausedMusicScale : 1f, Time.unscaledDeltaTime * 3f);

        musicSource.volume = masterVolume * musicVolume * fade * duck;
        ambienceSource.volume = masterVolume * ambienceVolume * fade * duck;

        // Footsteps are started/stopped by the player script, but that script stops
        // running while the game is paused, so silence the loop here.
        if (footstepMode != FootstepMode.None)
        {
            footstepSource.volume = masterVolume * sfxVolume * FootstepVolume(footstepMode);

            if (paused && footstepSource.isPlaying)
            {
                footstepSource.Pause();
            }
            else if (!paused && !footstepSource.isPlaying)
            {
                footstepSource.UnPause();
            }
        }
    }

    // ---------------------------------------------------------------- scenes

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetFootsteps(FootstepMode.None);
        HookButtons();

        if (SceneAudio.TryGetValue(scene.name, out var audio))
        {
            ChangeTracks(audio.music, audio.ambience);
        }
    }

    // Adds hover/click sounds to every Button in the scene (including inactive ones
    // such as the pause menu), so no per-button setup is needed.
    private void HookButtons()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button.GetComponent<UIButtonSound>() == null)
            {
                button.gameObject.AddComponent<UIButtonSound>();
            }
        }
    }

    private void ChangeTracks(string music, string ambience)
    {
        if (music == currentMusic && ambience == currentAmbience)
        {
            return;
        }

        if (switchRoutine != null)
        {
            StopCoroutine(switchRoutine);
        }

        switchRoutine = StartCoroutine(SwitchTracks(music, ambience));
    }

    private IEnumerator SwitchTracks(string music, string ambience)
    {
        // Fade out whatever is playing (nothing to fade on the very first scene).
        while (fade > 0f)
        {
            fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime / fadeTime);
            yield return null;
        }

        currentMusic = music;
        currentAmbience = ambience;
        Assign(musicSource, music);
        Assign(ambienceSource, ambience);

        while (fade < 1f)
        {
            fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / fadeTime);
            yield return null;
        }

        switchRoutine = null;
    }

    private void Assign(AudioSource source, string file)
    {
        AudioClip clip = string.IsNullOrEmpty(file) ? null : LoadClip(file);

        if (clip == null)
        {
            source.Stop();
            source.clip = null;
            return;
        }

        source.clip = clip;
        source.Play();
    }

    // ---------------------------------------------------------------- public API

    public static void Play(Sfx sfx, float volumeScale = 1f)
    {
        if (Instance == null || !Defs.TryGetValue(sfx, out var def))
        {
            return;
        }

        AudioClip clip = Instance.LoadClip(def.file);
        if (clip != null)
        {
            Instance.sfxSource.PlayOneShot(clip, Instance.masterVolume * Instance.sfxVolume * def.volume * volumeScale);
        }
    }

    public static AudioClip GetClip(Sfx sfx)
    {
        if (Instance == null || !Defs.TryGetValue(sfx, out var def))
        {
            return null;
        }

        return Instance.LoadClip(def.file);
    }

    // Starts, switches or stops the looping footstep sound. Safe to call every frame.
    public static void SetFootsteps(FootstepMode mode)
    {
        if (Instance == null || Instance.footstepMode == mode)
        {
            return;
        }

        Instance.footstepMode = mode;

        if (mode == FootstepMode.None)
        {
            Instance.footstepSource.Stop();
            return;
        }

        Sfx sfx = mode == FootstepMode.Run ? Sfx.FootstepRun : Sfx.FootstepWalk;
        Instance.footstepSource.clip = GetClip(sfx);
        Instance.footstepSource.volume = Instance.masterVolume * Instance.sfxVolume * FootstepVolume(mode);
        Instance.footstepSource.Play();
    }

    private static float FootstepVolume(FootstepMode mode)
    {
        return Defs[mode == FootstepMode.Run ? Sfx.FootstepRun : Sfx.FootstepWalk].volume;
    }

    // ---------------------------------------------------------------- loading

    private AudioClip LoadClip(string file)
    {
        if (clipCache.TryGetValue(file, out AudioClip cached))
        {
            return cached;
        }

        AudioClip clip = Resources.Load<AudioClip>(Folder + file);
        if (clip == null)
        {
            Debug.LogWarning("AudioManager: could not find clip 'Resources/" + Folder + file + "'");
        }

        // Cache misses too, so the warning is only logged once.
        clipCache[file] = clip;
        return clip;
    }
}
