using Godot;
using System;

namespace StreetChaos
{
    public partial class CombatManager : Node
    {
        public static CombatManager Instance { get; private set; }

        [Export] public float GlobalDamageMultiplier { get; set; } = 1.0f;
        [Export] public float FriendlyFire { get; set; } = 1.0f;
        [Export] public int MaxCombos { get; set; } = 5;

        public override void _Ready()
        {
            Instance = this;
        }

        public float CalculateDamage(float baseDamage, FightingStyleData attackerStyle,
            FightingStyleData defenderStyle, bool isCounterAttack)
        {
            float damage = baseDamage;

            if (attackerStyle != null)
                damage *= attackerStyle.DamageMultiplier;

            if (defenderStyle != null)
                damage /= defenderStyle.DefenseMultiplier;

            damage *= GlobalDamageMultiplier;

            if (isCounterAttack)
                damage *= 1.5f;

            return Mathf.Round(damage);
        }

        public float CalculateStunDuration(float baseDuration, FightingStyleData attackerStyle)
        {
            float duration = baseDuration;
            if (attackerStyle != null)
                duration *= attackerStyle.SpeedMultiplier;
            return duration;
        }
    }
}
