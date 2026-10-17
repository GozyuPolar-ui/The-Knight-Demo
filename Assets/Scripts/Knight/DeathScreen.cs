using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "YOU DIED" overlay: fades a dark screen + red title in when the knight dies and out again
// after the respawn. Built from code on first use, so no scene setup is needed.
//
// Usage (see KnightHealthManager.Respawn):
//     DeathScreen.Show(deathNumber);   // fade in
//     DeathScreen.Hide();              // fade out
public class DeathScreen : MonoBehaviour
{
    // True from Show() until the fade-out has finished. PauseMenu ignores ESC meanwhile.
    public static bool IsShowing { get; private set; }

    private const float FadeInTime = 1.2f;
    private const float FadeOutTime = 1.0f;
    private const float DimAlpha = 0.88f;
    private const float TitleStartScale = 1.15f; // the title slowly "settles" while fading in

    private static DeathScreen instance;

    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform titleRect;
    private TextMeshProUGUI subtitle;

    public static void Show(int deathNumber)
    {
        if (instance == null)
        {
            Create();
        }

        instance.subtitle.text = "DEATH #" + deathNumber;
        instance.StopAllCoroutines();
        IsShowing = true;
        instance.canvas.enabled = true;
        instance.StartCoroutine(instance.FadeTo(1f, FadeInTime));
    }

    public static void Hide()
    {
        if (instance == null)
        {
            IsShowing = false;
            return;
        }

        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.FadeOut());
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            IsShowing = false;
        }
    }

    // Unscaled time, so it still works if the game gets paused during the death sequence.
    private IEnumerator FadeTo(float target, float duration)
    {
        float start = group.alpha;

        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(start, target, t / duration));
            yield return null;
        }

        SetAlpha(target);
    }

    private IEnumerator FadeOut()
    {
        yield return FadeTo(0f, FadeOutTime);
        canvas.enabled = false;
        IsShowing = false;
    }

    private void SetAlpha(float alpha)
    {
        group.alpha = alpha;

        float scale = Mathf.Lerp(TitleStartScale, 1f, alpha);
        titleRect.localScale = new Vector3(scale, scale, 1f);
    }

    // ---------------------------------------------------------------- building the UI

    private static void Create()
    {
        GameObject root = new GameObject("DeathScreen", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup), typeof(DeathScreen));
        instance = root.GetComponent<DeathScreen>();
        instance.Build(FindFont());
    }

    private void Build(TMP_FontAsset font)
    {
        // No GraphicRaycaster on purpose: this overlay never blocks clicks.
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        group = GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject dimObject = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dimObject.transform.SetParent(transform, false);
        RectTransform dimRect = (RectTransform)dimObject.transform;
        dimRect.anchorMin = Vector2.zero;
        dimRect.anchorMax = Vector2.one;
        dimRect.offsetMin = Vector2.zero;
        dimRect.offsetMax = Vector2.zero;
        Image dim = dimObject.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, DimAlpha);
        dim.raycastTarget = false;

        TextMeshProUGUI title = CreateLabel("Title", "YOU DIED", 170f, new Vector2(0f, 40f), new Color(0.72f, 0.05f, 0.05f, 1f), font);
        title.characterSpacing = 20f;
        titleRect = title.rectTransform;

        subtitle = CreateLabel("Subtitle", "", 48f, new Vector2(0f, -110f), new Color(0.80f, 0.76f, 0.68f, 1f), font);

        SetAlpha(0f);
        canvas.enabled = false;
    }

    private TextMeshProUGUI CreateLabel(string name, string text, float size, Vector2 position, Color color, TMP_FontAsset font)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(1500f, 220f);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
        {
            label.font = font;
        }

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Midline;
        label.raycastTarget = false;
        return label;
    }

    // Reuse the game's pixel font (taken from any text already in the scene).
    public static TMP_FontAsset FindFont()
    {
        TMP_FontAsset fallback = null;

        foreach (TMP_Text text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (text.font == null)
            {
                continue;
            }

            if (text.font.name.ToLower().Contains("upheav"))
            {
                return text.font;
            }

            if (fallback == null)
            {
                fallback = text.font;
            }
        }

        return fallback;
    }
}
