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

        return cachedStaticStatus != null && cachedStaticStatus.IsInStaticPeriod;
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
