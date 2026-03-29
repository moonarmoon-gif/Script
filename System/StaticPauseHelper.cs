using System;
using System.Collections;
using UnityEngine;

public static class StaticPauseHelper
{
    public static bool IsStaticFrozen(Component owner, ref StaticStatus cachedStaticStatus)
    {
        if (cachedStaticStatus == null && owner != null)
        {
            cachedStaticStatus = owner.GetComponent<StaticStatus>();
        }

        if (cachedStaticStatus != null && cachedStaticStatus.IsInStaticPeriod)
        {
            return true;
        }

        if (owner == null)
        {
            return false;
        }

        StatusController statusController = owner.GetComponent<StatusController>() ?? owner.GetComponentInParent<StatusController>();
        return statusController != null && statusController.HasStatus(StatusId.Freeze);
    }

    public static IEnumerator WaitWhileStatic(Func<bool> shouldCancel, Func<bool> isStaticFrozen)
    {
        if (isStaticFrozen == null)
        {
            yield break;
        }

        while (isStaticFrozen())
        {
            if (shouldCancel != null && shouldCancel())
            {
                yield break;
            }

            yield return null;
        }
    }

    public static IEnumerator WaitForSecondsPauseSafeAndStatic(float seconds, Func<bool> shouldCancel, Func<bool> isStaticFrozen)
    {
        if (seconds <= 0f)
        {
            yield return WaitWhileStatic(shouldCancel, isStaticFrozen);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (shouldCancel != null && shouldCancel())
            {
                yield break;
            }

            if (isStaticFrozen != null && isStaticFrozen())
            {
                yield return null;
                continue;
            }

            float dt = GameStateManager.GetPauseSafeDeltaTime();
            if (dt > 0f)
            {
                elapsed += dt;
            }

            yield return null;
        }

        yield return WaitWhileStatic(shouldCancel, isStaticFrozen);
    }
}
