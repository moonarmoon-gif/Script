using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class HoldRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Repeat")]
    [Min(0f)]
    public float InitialDelaySeconds = 0.35f;

    [Min(0.01f)]
    public float RepeatIntervalSeconds = 0.06f;

    public bool TriggerImmediatelyOnPress = false;

    private Button button;
    private Coroutine repeatRoutine;
    private bool isPressed;
    private bool repeatedThisPress;
    private bool invokedOnPress;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (button == null || !button.IsActive() || !button.IsInteractable())
        {
            return;
        }

        isPressed = true;
        repeatedThisPress = false;
        invokedOnPress = false;

        if (TriggerImmediatelyOnPress)
        {
            invokedOnPress = true;
            button.onClick.Invoke();
        }

        if (repeatRoutine != null)
        {
            StopCoroutine(repeatRoutine);
        }
        repeatRoutine = StartCoroutine(Repeat());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        isPressed = false;
        StopRepeat();

        if (eventData != null && (repeatedThisPress || invokedOnPress))
        {
            eventData.eligibleForClick = false;
        }

        if (button != null && (repeatedThisPress || invokedOnPress))
        {
            StartCoroutine(SuppressReleaseClickForOneFrame());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPressed = false;
        StopRepeat();
    }

    public void OnDisable()
    {
        isPressed = false;
        StopRepeat();
    }

    private void StopRepeat()
    {
        if (repeatRoutine != null)
        {
            StopCoroutine(repeatRoutine);
            repeatRoutine = null;
        }
    }

    private IEnumerator Repeat()
    {
        float delay = Mathf.Max(0f, InitialDelaySeconds);
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        float interval = Mathf.Max(0.01f, RepeatIntervalSeconds);

        while (isPressed)
        {
            if (button == null || !button.IsActive() || !button.IsInteractable())
            {
                yield break;
            }

            repeatedThisPress = true;
            button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(interval);
        }
    }

    private IEnumerator SuppressReleaseClickForOneFrame()
    {
        if (button == null)
        {
            yield break;
        }

        bool wasInteractable = button.interactable;
        button.interactable = false;
        yield return null;
        button.interactable = wasInteractable;
    }
}
