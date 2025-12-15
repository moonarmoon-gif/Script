using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(Button))]
public class AskForFavourButton : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("Number of favour choices shown when requesting a favour.")]
    [SerializeField] private int choices = 3;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }

    private void OnClick()
    {
        if (CardSelectionManager.Instance == null) return;
        if (!CardSelectionManager.Instance.UseFavourSoulSystem) return;
        if (CardSelectionManager.Instance.AutomaticLevelingFavourSystem) return;
        if (FavourExpUI.Instance == null) return;

        int soulLevel = FavourExpUI.Instance.CurrentSoulLevel;
        if (soulLevel < 1)
        {
            return;
        }

        StartCoroutine(RequestFavourRoutine(soulLevel));
    }

    private IEnumerator RequestFavourRoutine(int soulLevelAtClick)
    {
        var manager = CardSelectionManager.Instance;
        if (manager == null) yield break;

        float delay = manager.favourSelectionDelay;
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        int count = choices > 0 ? choices : manager.SoulFavourChoices;
        if (count <= 0) count = 3;

        manager.ShowSoulFavourCardsForSoulLevel(soulLevelAtClick, count);

        while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
        {
            yield return null;
        }

        if (FavourExpUI.Instance != null)
        {
            FavourExpUI.Instance.RebaseAfterFavourSelection();
        }
    }
}
