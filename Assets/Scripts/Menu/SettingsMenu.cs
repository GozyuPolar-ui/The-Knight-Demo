using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Settings panel: Master / Music / Ambience / SFX volume sliders, a fullscreen toggle,
// "Reset" and "Back".
//
// The whole panel is built from code when it is opened, so no scene setup is needed:
//   * The "OptionsButton" in the MainMenu scene is wired to it automatically.
//   * A "Settings" button is added to the pause menu (see AddPauseMenuButton, called by PauseMenu).
//
// Volumes are stored by AudioManager (PlayerPrefs); fullscreen is stored here.
public class SettingsMenu : MonoBehaviour
{
    // True while the panel is on screen. PauseMenu checks it so that ESC closes the
    // settings first instead of also resuming the game.
    public static bool IsOpen { get; private set; }

    private const string FullscreenKey = "settings_fullscreen";
    private const string OptionsButtonName = "OptionsButton";

    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.75f);
    private static readonly Color BorderColor = new Color(0.85f, 0.68f, 0.30f, 1f);
    private static readonly Color PanelColor = new Color(0.10f, 0.09f, 0.12f, 1f);
    private static readonly Color TextColor = new Color(0.95f, 0.92f, 0.82f, 1f);
    private static readonly Color TrackColor = new Color(0.22f, 0.20f, 0.26f, 1f);
    private static readonly Color ButtonColor = new Color(0.40f, 0.35f, 0.50f, 1f);

    private static SettingsMenu instance;
    private static TMP_FontAsset font; // borrowed from the button that opened the menu

    private struct VolumeRow
    {
        public AudioChannel channel;
        public Slider slider;
        public TextMeshProUGUI valueText;
    }

    private readonly List<VolumeRow> rows = new List<VolumeRow>();
    private bool closeRequested;
    private float lastPreviewTime;

    // ---------------------------------------------------------------- startup hooks

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (PlayerPrefs.HasKey(FullscreenKey))
        {
            ApplyFullscreen(PlayerPrefs.GetInt(FullscreenKey) == 1);
        }

        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Hooks up the (previously empty) OptionsButton of the main menu.
    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        IsOpen = false;

        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Button button in buttons)
        {
            if (button.gameObject.name != OptionsButtonName)
            {
                continue;
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            button.onClick.AddListener(() => Open(label != null ? label.font : null));
        }
    }

    // Adds a "Settings" button to the pause menu (between Resume and Menu) and spreads
    // the buttons out evenly. Called from PauseMenu.Start.
    public static void AddPauseMenuButton(GameObject pauseMenuUI)
    {
        if (pauseMenuUI == null)
        {
            return;
        }

        Button resume = null;
        Button quit = null;
        List<RectTransform> others = new List<RectTransform>();

        foreach (Button b in pauseMenuUI.GetComponentsInChildren<Button>(true))
        {
            switch (b.gameObject.name)
            {
                case "SettingsButton":
                    return; // already added
                case "ResumeButton":
                    resume = b;
                    break;
                case "QuitButton":
                    quit = b;
                    break;
                case "MenuButton":
                    others.Add((RectTransform)b.transform);
                    break;
            }
        }

        if (resume == null)
        {
            return;
        }

        // Copy Resume so the look (sprite, font, size) matches ...
        GameObject clone = Instantiate(resume.gameObject, resume.transform.parent, false);
        clone.name = "SettingsButton";
        clone.transform.SetSiblingIndex(resume.transform.GetSiblingIndex() + 1);

        // ... but give it a fresh Button, otherwise it would keep Resume's OnClick from the scene.
        foreach (UIButtonSound sound in clone.GetComponents<UIButtonSound>())
        {
            DestroyImmediate(sound);
        }

        Button old = clone.GetComponent<Button>();
        Selectable.Transition transition = old.transition;
        ColorBlock colors = old.colors;
        SpriteState spriteState = old.spriteState;
        Navigation navigation = old.navigation;
        DestroyImmediate(old);

        Button settingsButton = clone.AddComponent<Button>();
        settingsButton.transition = transition;
        settingsButton.colors = colors;
        settingsButton.spriteState = spriteState;
        settingsButton.navigation = navigation;
        settingsButton.targetGraphic = clone.GetComponent<Graphic>();
        clone.AddComponent<UIButtonSound>();

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = "Settings";
        }

        settingsButton.onClick.AddListener(() => Open(label != null ? label.font : null));

        // Re-space: Resume (top) ... Quit (bottom), keeping the original outer positions.
        List<RectTransform> ordered = new List<RectTransform> { (RectTransform)resume.transform, (RectTransform)clone.transform };
        ordered.AddRange(others);
        if (quit != null)
        {
            ordered.Add((RectTransform)quit.transform);
        }

        float top = ordered[0].anchoredPosition.y;
        float bottom = quit != null ? ((RectTransform)quit.transform).anchoredPosition.y : top - 130f * (ordered.Count - 1);
        float step = (top - bottom) / (ordered.Count - 1);

        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].anchoredPosition = new Vector2(ordered[i].anchoredPosition.x, top - step * i);
        }
    }

    // ---------------------------------------------------------------- open / close

    public static void Open(TMP_FontAsset labelFont = null)
    {
        if (instance != null)
        {
            return;
        }

        if (labelFont != null)
        {
            font = labelFont;
        }

        EnsureEventSystem();

        GameObject root = new GameObject("SettingsMenu", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(SettingsMenu));
        instance = root.GetComponent<SettingsMenu>();
        instance.Build();
        IsOpen = true;
    }

    public static void Close()
    {
        if (instance == null)
        {
            return;
        }

        PlayerPrefs.Save();
        IsOpen = false;
        Destroy(instance.gameObject);
        instance = null;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            closeRequested = true;
        }
    }

    // Closed in LateUpdate so PauseMenu.Update (same frame) still sees IsOpen == true
    // and does not treat this ESC as "resume the game".
    private void LateUpdate()
    {
        if (closeRequested)
        {
            closeRequested = false;
            Close();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            IsOpen = false;
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    // ---------------------------------------------------------------- building the UI

    private void Build()
    {
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Full-screen dim; it also swallows clicks meant for the menu behind.
        Image dim = CreateImage("Dim", transform, DimColor);
        Stretch(dim.rectTransform);

        Image border = CreateImage("Border", dim.transform, BorderColor);
        Place(border.rectTransform, Vector2.zero, new Vector2(916f, 776f));

        Image panel = CreateImage("Panel", border.transform, PanelColor);
        Stretch(panel.rectTransform, 8f);

        Transform p = panel.transform;

        CreateLabel(p, "SETTINGS", 72f, new Vector2(0f, 310f), new Vector2(700f, 100f), TextAlignmentOptions.Midline);

        AddVolumeRow(p, "MASTER", AudioChannel.Master, 195f);
        AddVolumeRow(p, "MUSIC", AudioChannel.Music, 100f);
        AddVolumeRow(p, "AMBIENCE", AudioChannel.Ambience, 5f);
        AddVolumeRow(p, "SFX", AudioChannel.Sfx, -90f);

        AddFullscreenRow(p, -195f);

        CreateButton(p, "RESET", new Vector2(-170f, -310f), new Vector2(300f, 80f), ResetToDefaults);
        CreateButton(p, "BACK", new Vector2(170f, -310f), new Vector2(300f, 80f), Close);
    }

    private void AddVolumeRow(Transform parent, string title, AudioChannel channel, float y)
    {
        CreateLabel(parent, title, 40f, new Vector2(-270f, y), new Vector2(260f, 60f), TextAlignmentOptions.MidlineLeft);

        Slider slider = CreateSlider(parent, new Vector2(60f, y), new Vector2(440f, 28f));
        TextMeshProUGUI valueText = CreateLabel(parent, "", 40f, new Vector2(360f, y), new Vector2(140f, 60f), TextAlignmentOptions.MidlineRight);

        float current = AudioManager.GetVolume(channel);
        slider.SetValueWithoutNotify(current);
        valueText.text = Percent(current);

        slider.onValueChanged.AddListener(value =>
        {
            AudioManager.SetVolume(channel, value);
            valueText.text = Percent(value);
            PlayPreview(channel);
        });

        rows.Add(new VolumeRow { channel = channel, slider = slider, valueText = valueText });
    }

    private void AddFullscreenRow(Transform parent, float y)
    {
        CreateLabel(parent, "FULLSCREEN", 40f, new Vector2(-270f, y), new Vector2(260f, 60f), TextAlignmentOptions.MidlineLeft);

        Toggle toggle = CreateToggle(parent, new Vector2(-130f, y), Screen.fullScreen);
        toggle.onValueChanged.AddListener(on =>
        {
            ApplyFullscreen(on);
            PlayerPrefs.SetInt(FullscreenKey, on ? 1 : 0);
        });
    }

    private void ResetToDefaults()
    {
        AudioManager.ResetVolumes();

        foreach (VolumeRow row in rows)
        {
            float value = AudioManager.GetVolume(row.channel);
            row.slider.SetValueWithoutNotify(value);
            row.valueText.text = Percent(value);
        }
    }

    // Gives audible feedback while dragging the loudness-related sliders.
    private void PlayPreview(AudioChannel channel)
    {
        if (channel != AudioChannel.Master && channel != AudioChannel.Sfx)
        {
            return; // music / ambience are already audible while dragging
        }

        if (Time.unscaledTime - lastPreviewTime < 0.15f)
        {
            return;
        }

        lastPreviewTime = Time.unscaledTime;
        AudioManager.Play(Sfx.Hit);
    }

    private static string Percent(float value)
    {
        return Mathf.RoundToInt(value * 100f) + "%";
    }

    private static void ApplyFullscreen(bool on)
    {
        Screen.fullScreenMode = on ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
    }

    // ---------------------------------------------------------------- widget helpers
    // Only plain coloured rectangles are used (no built-in sprites), so this also works in builds.

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static void Place(RectTransform rt, Vector2 position, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string text, float size, Vector2 position, Vector2 boxSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, position, boxSize);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
        {
            label.font = font;
        }

        label.text = text;
        label.fontSize = size;
        label.color = TextColor;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static Slider CreateSlider(Transform parent, Vector2 position, Vector2 size)
    {
        GameObject root = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        Place((RectTransform)root.transform, position, size);

        // Track
        Image background = CreateImage("Background", root.transform, TrackColor);
        Stretch(background.rectTransform);

        // Fill (the Slider drives its anchors)
        RectTransform fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(root.transform, false);
        fillArea.anchorMin = new Vector2(0f, 0.2f);
        fillArea.anchorMax = new Vector2(1f, 0.8f);
        fillArea.offsetMin = new Vector2(10f, 0f);
        fillArea.offsetMax = new Vector2(-10f, 0f);

        Image fill = CreateImage("Fill", fillArea, BorderColor);
        fill.rectTransform.sizeDelta = Vector2.zero;

        // Handle (the Slider drives its x anchors; it is a bit taller than the track)
        RectTransform handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(root.transform, false);
        Stretch(handleArea);
        handleArea.offsetMin = new Vector2(14f, 0f);
        handleArea.offsetMax = new Vector2(-14f, 0f);

        Image handle = CreateImage("Handle", handleArea, TextColor);
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(24f, 12f);

        Slider slider = root.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        return slider;
    }

    private static Toggle CreateToggle(Transform parent, Vector2 position, bool isOn)
    {
        GameObject root = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
        root.transform.SetParent(parent, false);
        Place((RectTransform)root.transform, position, new Vector2(56f, 56f));

        Image background = CreateImage("Background", root.transform, TrackColor);
        Stretch(background.rectTransform);

        Image check = CreateImage("Checkmark", background.transform, BorderColor);
        Stretch(check.rectTransform, 10f);

        Toggle toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = check;
        toggle.SetIsOnWithoutNotify(isOn);
        return toggle;
    }

    private static Button CreateButton(Transform parent, string text, Vector2 position, Vector2 size, UnityAction onClick)
    {
        GameObject go = new GameObject(text + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Place((RectTransform)go.transform, position, size);

        Image image = go.GetComponent<Image>();
        image.color = ButtonColor;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        // The image colour is multiplied by these tints.
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI label = CreateLabel(go.transform, text, 40f, Vector2.zero, size, TextAlignmentOptions.Midline);
        Stretch(label.rectTransform);

        button.onClick.AddListener(onClick);
        go.AddComponent<UIButtonSound>();
        return button;
    }
}
