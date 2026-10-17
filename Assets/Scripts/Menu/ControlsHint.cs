using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shows the key bindings once, at the bottom of the screen, the first time the game scene
// is entered ("seen" is remembered in PlayerPrefs, so it never pops up again).
// The same list is shown in Settings > Controls, so players can look it up later.
//
// Keep Entries in sync when a key binding changes in the scripts.
public class ControlsHint : MonoBehaviour
{
    // { key, what it does } - taken from the Input.* calls in the scripts.
    public static readonly string[,] Entries =
    {
        { "W A S D / ARROWS", "Move" },                // KnightController
        { "SHIFT", "Sprint" },                         // KnightController
        { "LEFT CLICK", "Attack / use tool" },         // ItemParent
        { "1 - 4", "Hotbar slot" },                    // HotBar / SlotSwitch
        { "I", "Inventory" },                          // InventoryController
        { "B", "Build (at sign)" },                    // Sign
        { "ESC", "Pause menu" },                       // PauseMenu
    };

    private const string SeenKey = "controls_hint_seen";
    private const string GameScene = "MainTown";

    private const float DelayBeforeShow = 2f;
    private const float FadeInTime = 0.8f;
    private const float HoldTime = 9f;
    private const float FadeOutTime = 1.2f;

    private CanvasGroup group;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != GameScene || PlayerPrefs.GetInt(SeenKey, 0) == 1)
        {
            return;
        }

        GameObject root = new GameObject("ControlsHint", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(ControlsHint));
        root.GetComponent<ControlsHint>().Build();
    }

    private void Build()
    {
        // No GraphicRaycaster: the hint never blocks clicks.
        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject box = new GameObject("Box", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        box.transform.SetParent(transform, false);
        RectTransform boxRect = (RectTransform)box.transform;
        boxRect.anchorMin = boxRect.anchorMax = boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.anchoredPosition = new Vector2(0f, 90f);
        boxRect.sizeDelta = new Vector2(1760f, 140f);
        Image background = box.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(box.transform, false);
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 10f);
        textRect.offsetMax = new Vector2(-20f, -10f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        TMP_FontAsset font = DeathScreen.FindFont();
        if (font != null)
        {
            text.font = font;
        }

        text.text = Line(0, 3) + "\n" + Line(3, Entries.GetLength(0));
        text.fontSize = 32f;
        text.color = new Color(0.95f, 0.92f, 0.82f, 1f);
        text.alignment = TextAlignmentOptions.Midline;
        text.raycastTarget = false;

        StartCoroutine(Run());
    }

    // "KEY: Action   |   KEY: Action" for entries [from, to).
    private static string Line(int from, int to)
    {
        string line = "";

        for (int i = from; i < to; i++)
        {
            if (i > from)
            {
                line += "   |   ";
            }

            line += Entries[i, 0] + ": " + Entries[i, 1];
        }

        return line;
    }

    private IEnumerator Run()
    {
        yield return Wait(DelayBeforeShow);

        // Count it as seen as soon as it is shown.
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();

        yield return Fade(1f, FadeInTime);
        yield return Wait(HoldTime);
        yield return Fade(0f, FadeOutTime);

        Destroy(gameObject);
    }

    // Unscaled time, so the hint also behaves while the game is paused.
    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            yield return null;
        }
    }

    private IEnumerator Fade(float target, float duration)
    {
        float start = group.alpha;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Lerp(start, target, t / duration);
            yield return null;
        }

        group.alpha = target;
    }
}
