using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Damage Ability",
    menuName = "Fractured Frequencies/Abilities/Damage")]
public class DamageAbility : Ability
{
    [SerializeField] private int damage = 50;
    [SerializeField] private bool applyKnockback = false;
    [SerializeField] private bool onlyAffectPlayer = false;

    protected override bool ShouldAffectTarget(Entity target)
    {
        return (target is Player) == onlyAffectPlayer;
    }

    protected override void ApplyToTarget(
        Entity target,
        PatternFunctionParameters patternParameters,
        Vector3 areaOrigin)
    {
        target.TakeDamage(damage, areaOrigin, applyKnockback);
    }
}
