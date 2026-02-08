using UnityEngine;
using TMPro;

public class TimerUI : MonoBehaviour
{
    public TextMeshProUGUI TimerText;

    private int elapsedSeconds;
    private float secondAccumulator;

    private EnemyCardSpawner enemyCardSpawner;
    private EnemySpawner enemySpawner;

    private void Awake()
    {
        elapsedSeconds = 0;
        secondAccumulator = 0f;
        UpdateText();
    }

    private void Update()
    {
        if (TimerText == null)
        {
            return;
        }

        if (IsPaused())
        {
            return;
        }

        float dt = GameStateManager.GetRunTimerDeltaTime();
        if (dt <= 0f)
        {
            return;
        }

        secondAccumulator += dt;
        if (secondAccumulator < 1f)
        {
            return;
        }

        int add = Mathf.FloorToInt(secondAccumulator);
        if (add <= 0)
        {
            return;
        }

        secondAccumulator -= add;
        elapsedSeconds += add;
        UpdateText();
    }

    private bool IsPaused()
    {
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return true;
        }

        if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
        {
            return true;
        }

        if (enemyCardSpawner == null)
        {
            enemyCardSpawner = FindObjectOfType<EnemyCardSpawner>();
        }
        if (enemyCardSpawner != null && (enemyCardSpawner.IsBossEventActive || enemyCardSpawner.IsPostBossRefillDelayActive))
        {
            return true;
        }

        if (enemySpawner == null)
        {
            enemySpawner = FindObjectOfType<EnemySpawner>();
        }
        if (enemySpawner != null && (enemySpawner.IsBossEventActive || enemySpawner.IsPostBossRefillDelayActive))
        {
            return true;
        }

        return false;
    }

    private void UpdateText()
    {
        if (TimerText == null)
        {
            return;
        }

        int minutes = Mathf.Max(0, elapsedSeconds) / 60;
        int seconds = Mathf.Max(0, elapsedSeconds) % 60;
        TimerText.text = minutes.ToString("00") + " : " + seconds.ToString("00");
    }
}
