using Godot;

namespace StreetChaos
{
    public static class BaseStyles
    {
        public static FightingStyleData CreateStreetBrawler()
        {
            var style = new FightingStyleData
            {
                StyleName = "Street Brawler",
                Description = "Luta de rua sem regras. Golpes sujos e brutalidade.",
                DamageMultiplier = 1.0f,
                SpeedMultiplier = 1.0f,
                DefenseMultiplier = 1.0f,
                StaminaRegenMultiplier = 1.0f,
                LightPunch = new AttackData
                {
                    Type = AttackType.LightPunch, Input = AttackInput.Light,
                    Damage = 8, StaminaCost = 8, Range = 2f, Speed = 1.2f,
                    HitDuration = 0.1f, RecoveryTime = 0.2f, KnockbackForce = 3f, StunDuration = 0.2f
                },
                HeavyPunch = new AttackData
                {
                    Type = AttackType.HeavyPunch, Input = AttackInput.Heavy,
                    Damage = 15, StaminaCost = 15, Range = 2.2f, Speed = 0.8f,
                    HitDuration = 0.15f, RecoveryTime = 0.4f, KnockbackForce = 7f, StunDuration = 0.4f,
                    BreaksGuard = true
                },
                LightKick = new AttackData
                {
                    Type = AttackType.LightKick, Input = AttackInput.KickLight,
                    Damage = 10, StaminaCost = 10, Range = 2.5f, Speed = 1.0f,
                    HitDuration = 0.12f, RecoveryTime = 0.3f, KnockbackForce = 4f, StunDuration = 0.25f
                },
                HeavyKick = new AttackData
                {
                    Type = AttackType.HeavyKick, Input = AttackInput.KickHeavy,
                    Damage = 18, StaminaCost = 18, Range = 2.8f, Speed = 0.7f,
                    HitDuration = 0.18f, RecoveryTime = 0.5f, KnockbackForce = 9f, StunDuration = 0.5f,
                    LaunchesEnemy = true, BreaksGuard = true
                },
                Uppercut = new AttackData
                {
                    Type = AttackType.Uppercut, Input = AttackInput.Uppercut,
                    Damage = 20, StaminaCost = 20, Range = 2f, Speed = 0.6f,
                    HitDuration = 0.12f, RecoveryTime = 0.45f, KnockbackForce = 8f, StunDuration = 0.6f,
                    LaunchesEnemy = true
                },
                Combos = new[]
                {
                    new ComboData
                    {
                        ComboName = "Jab Cross", InputSequence = new[] { AttackInput.Light, AttackInput.Heavy },
                        DamageMultiplier = 1.8f, FinalHitKnockback = 10f, Unblockable = false
                    },
                    new ComboData
                    {
                        ComboName = "Low High", InputSequence = new[] { AttackInput.KickLight, AttackInput.Heavy },
                        DamageMultiplier = 2.0f, LaunchesEnemy = true, FinalHitKnockback = 12f
                    },
                    new ComboData
                    {
                        ComboName = "Devastation", InputSequence = new[] { AttackInput.Light, AttackInput.Light, AttackInput.Heavy },
                        DamageMultiplier = 2.5f, LaunchesEnemy = true, Unblockable = true, FinalHitKnockback = 15f
                    }
                }
            };
            return style;
        }

        public static FightingStyleData CreateBoxing()
        {
            var style = CreateStreetBrawler();
            style.StyleName = "Boxe";
            style.Description = "Boxe clássico. Jabs, crosses e hooks rápidos.";
            style.DamageMultiplier = 0.9f;
            style.SpeedMultiplier = 1.3f;
            style.DefenseMultiplier = 1.1f;

            style.LightPunch.Damage = 7;
            style.LightPunch.Speed = 1.5f;
            style.LightPunch.StaminaCost = 6;

            style.HeavyPunch.Damage = 14;
            style.HeavyPunch.Speed = 1.0f;
            style.HeavyPunch.StaminaCost = 12;

            style.LightKick.Damage = 8;
            style.LightKick.Speed = 1.2f;

            style.HeavyKick.Damage = 15;
            style.HeavyKick.Speed = 0.9f;

            style.Uppercut.Damage = 18;
            style.Uppercut.Speed = 0.8f;

            style.Combos = new[]
            {
                new ComboData { ComboName = "Jab Jab Cross", InputSequence = new[] { AttackInput.Light, AttackInput.Light, AttackInput.Heavy }, DamageMultiplier = 2.0f, FinalHitKnockback = 10f },
                new ComboData { ComboName = "Body Blow", InputSequence = new[] { AttackInput.Heavy, AttackInput.Heavy }, DamageMultiplier = 2.2f, LaunchesEnemy = true, FinalHitKnockback = 12f },
            };
            return style;
        }

        public static FightingStyleData CreateMuayThai()
        {
            var style = CreateStreetBrawler();
            style.StyleName = "Muay Thai";
            style.Description = "Artes marciais tailandesas. Joelhadas e cotoveladas devastadoras.";
            style.DamageMultiplier = 1.2f;
            style.SpeedMultiplier = 0.9f;
            style.DefenseMultiplier = 0.9f;

            style.LightPunch.Damage = 9;
            style.LightPunch.StaminaCost = 10;

            style.HeavyPunch.Damage = 16;
            style.HeavyPunch.StaminaCost = 18;

            style.LightKick.Damage = 12;
            style.LightKick.StaminaCost = 12;

            style.HeavyKick.Damage = 22;
            style.HeavyKick.StaminaCost = 22;
            style.HeavyKick.KnockbackForce = 12f;

            style.Uppercut.Damage = 18;
            style.Uppercut.StaminaCost = 18;

            style.Knee = new AttackData
            {
                Type = AttackType.Knee, Input = AttackInput.Special,
                Damage = 25, StaminaCost = 25, Range = 1.8f, Speed = 0.6f,
                HitDuration = 0.15f, RecoveryTime = 0.4f, KnockbackForce = 10f, StunDuration = 0.6f,
                LaunchesEnemy = true, BreaksGuard = true
            };
            style.SpecialAttack = style.Knee;

            style.Combos = new[]
            {
                new ComboData { ComboName = "Thai Combo", InputSequence = new[] { AttackInput.Light, AttackInput.Light, AttackInput.KickHeavy }, DamageMultiplier = 2.4f, LaunchesEnemy = true, FinalHitKnockback = 14f },
            };
            return style;
        }
    }
}
