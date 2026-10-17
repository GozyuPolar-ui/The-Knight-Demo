using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Plays the UI hover / click sounds for a Button.
// Added automatically to every Button by AudioManager when a scene loads.
[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button.interactable)
        {
            AudioManager.Play(Sfx.UIHover);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (button.interactable)
        {
            AudioManager.Play(Sfx.UIClick);
        }
    }
}
