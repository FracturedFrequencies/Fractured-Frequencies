using System.Collections.Generic;
using UnityEngine;

public abstract class Ability : ScriptableObject
{
    public void Activate(
        PatternFunctionParameters patternParameters,
        IReadOnlyList<Entity> targets,
        Vector3 areaOrigin)

    {
        foreach (Entity target in targets)
        {
            if (target == null || !ShouldAffectTarget(target))
            {
                continue;
            }

            ApplyToTarget(target, patternParameters, areaOrigin);
        }
    }

    protected virtual bool ShouldAffectTarget(Entity target)
    {
        return true;
    }

    protected abstract void ApplyToTarget(
        Entity target,
        PatternFunctionParameters patternParameters,
        Vector3 areaOrigin);
}
