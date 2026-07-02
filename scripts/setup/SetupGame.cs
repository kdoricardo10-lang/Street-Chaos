#if TOOLS
using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    [Tool]
    public partial class SetupGame : Node
    {
        public override void _Ready()
        {
            if (!Engine.IsEditorHint())
                return;

            GD.Print("=== SETUP GAME STARTED ===");

            // ── Fighting Styles ──────────────────────────────────────────
            string styleDir = ProjectSettings.GlobalizePath("res://resources/fighting_styles/");
            System.IO.Directory.CreateDirectory(styleDir);

            // 1. Street Brawler
            CreateStyle("street_brawler", "Street Brawler", "Luta de rua sem regras. Golpes sujos e brutalidade.",
                1.0f, 1.0f, 1.0f, 1.0f,
                Atk(0, 0, 8, 8, 2f, 1.2f, 0.1f, 0.2f, 3f, 0.2f, false, false),
                Atk(1, 1, 15, 15, 2.2f, 0.8f, 0.15f, 0.4f, 7f, 0.4f, true, false),
                Atk(2, 2, 10, 10, 2.5f, 1f, 0.12f, 0.3f, 4f, 0.25f, false, false),
                Atk(3, 3, 18, 18, 2.8f, 0.7f, 0.18f, 0.5f, 9f, 0.5f, true, true),
                Atk(4, 4, 20, 20, 2f, 0.6f, 0.12f, 0.45f, 8f, 0.6f, false, true),
                null, null, null,
                new[]
                {
                    Cmb("Jab Cross", new[] { AttackInput.Light, AttackInput.Heavy }, 1.8f, 10f, false, false),
                    Cmb("Low High", new[] { AttackInput.KickLight, AttackInput.Heavy }, 2f, 12f, true, false),
                    Cmb("Devastation", new[] { AttackInput.Light, AttackInput.Light, AttackInput.Heavy }, 2.5f, 15f, true, true),
                });

            // 2. Boxe (Boxing)
            CreateStyle("boxe", "Boxe", "Boxe clássico. Jabs, crosses e hooks rápidos.",
                0.9f, 1.3f, 1.1f, 1.0f,
                Atk(0, 0, 7, 6, 2f, 1.5f, 0.1f, 0.2f, 3f, 0.2f, false, false),
                Atk(1, 1, 14, 12, 2.2f, 1.0f, 0.15f, 0.35f, 7f, 0.4f, true, false),
                Atk(2, 2, 8, 8, 2.5f, 1.2f, 0.12f, 0.25f, 4f, 0.25f, false, false),
                Atk(3, 3, 15, 15, 2.8f, 0.9f, 0.18f, 0.4f, 9f, 0.5f, true, true),
                Atk(4, 4, 18, 16, 2f, 0.8f, 0.12f, 0.4f, 8f, 0.5f, false, true),
                null, null, null,
                new[]
                {
                    Cmb("Jab Jab Cross", new[] { AttackInput.Light, AttackInput.Light, AttackInput.Heavy }, 2.0f, 10f, false, false),
                    Cmb("Body Blow", new[] { AttackInput.Heavy, AttackInput.Heavy }, 2.2f, 12f, true, false),
                });

            // 3. Muay Thai
            CreateStyle("muay_thai", "Muay Thai", "Artes marciais tailandesas. Joelhadas e cotoveladas devastadoras.",
                1.2f, 0.9f, 0.9f, 1.0f,
                Atk(0, 0, 9, 10, 2f, 1.0f, 0.1f, 0.25f, 3f, 0.25f, false, false),
                Atk(1, 1, 16, 18, 2.2f, 0.7f, 0.15f, 0.45f, 7f, 0.45f, true, false),
                Atk(2, 2, 12, 12, 2.5f, 0.9f, 0.12f, 0.35f, 5f, 0.3f, false, false),
                Atk(3, 3, 22, 22, 2.8f, 0.6f, 0.18f, 0.55f, 12f, 0.55f, true, true),
                Atk(4, 4, 18, 18, 2f, 0.55f, 0.12f, 0.5f, 8f, 0.55f, false, true),
                Atk(5, 5, 25, 25, 1.8f, 0.6f, 0.15f, 0.4f, 10f, 0.6f, true, true),
                null, null,
                new[]
                {
                    Cmb("Thai Combo", new[] { AttackInput.Light, AttackInput.Light, AttackInput.KickHeavy }, 2.4f, 14f, true, false),
                });

            GD.Print("  Fighting styles created/updated.");

            // ── Items ────────────────────────────────────────────────────
            string itemDir = ProjectSettings.GlobalizePath("res://resources/items/");
            System.IO.Directory.CreateDirectory(itemDir);

            // Weapons
            CreateWeapon("cano_ferro", "Cano de Ferro", "Um cano de ferro pesado. Simples e eficaz.",
                8f, 0.9f, 1.2f, ItemRarity.Common, 3f);
            CreateWeapon("taco_baseball_comum", "Taco de Baseball", "Taco de baseball comum. Dá pra quebrar uma cabeça.",
                8f, 0.9f, 1.3f, ItemRarity.Common, 3f);
            CreateWeapon("baseball_bat", "Taco de Baseball", "Taco de baseball reforçado. Dói só de olhar.",
                18f, 0.8f, 1.8f, ItemRarity.Rare, 6f);
            CreateWeapon("taco_baseball_epico", "Taco de Baseball Épico", "Taco lendário com poder de destruição.",
                28f, 0.7f, 2.0f, ItemRarity.Epic, 10f);
            CreateWeapon("espada_longa", "Espada Longa", "Uma espada longa afiada. Corte preciso e mortal.",
                25f, 0.75f, 2.2f, ItemRarity.Epic, 8f);
            CreateWeapon("martelo_guerra", "Martelo de Guerra", "Martelo de guerra massivo. Esmaga qualquer um.",
                35f, 0.5f, 1.5f, ItemRarity.Legendary, 15f);

            // Healing
            CreateHealing("bandagem", "Bandagem", "Curativo básico para ferimentos leves.", 15f, ItemRarity.Common);
            CreateHealing("kit_cura", "Kit de Cura", "Kit médico básico para ferimentos moderados.", 40f, ItemRarity.Uncommon);
            CreateHealing("kit_medico", "Kit Médico", "Kit médico avançado. Cura ferimentos graves.", 80f, ItemRarity.Rare);
            CreateHealing("soro_vida", "Soro de Vida", "Soro revitalizante. Recupera muito a saúde.", 150f, ItemRarity.Epic);
            CreateHealing("cura_total", "Cura Total", "Cura milagrosa. Restaura toda a saúde.", 300f, ItemRarity.Legendary);

            // Buffs
            CreateBuff("invisibility_buff", "Sombra Etérea", "Fica invisível por 30 segundos.",
                BuffType.Invisibility, 0f, 30f, ItemRarity.Epic);
            CreateBuff("super_speed_buff", "Super Velocidade", "Corre muito mais rápido por 30 segundos.",
                BuffType.Speed, 2.0f, 30f, ItemRarity.Rare);
            CreateBuff("super_jump_buff", "Super Pulo", "Pula muito mais alto por 30 segundos.",
                BuffType.Jump, 2.0f, 30f, ItemRarity.Rare);
            CreateBuff("dano_extra", "Dano Extra", "Aumenta o dano causado em 50% por 20 segundos.",
                BuffType.Damage, 1.5f, 20f, ItemRarity.Uncommon);
            CreateBuff("defesa_extra", "Defesa Reforçada", "Reduz o dano recebido em 30% por 25 segundos.",
                BuffType.Defense, 0.7f, 25f, ItemRarity.Uncommon);
            CreateBuff("stamina_infinita", "Energia Ilimitada", "Recupera stamina muito mais rápido por 15 segundos.",
                BuffType.StaminaRegen, 3.0f, 15f, ItemRarity.Rare);

            GD.Print("  Items created/updated.");

            // ── Completion ──────────────────────────────────────────────
            GD.Print("=== SETUP COMPLETE ===");
            GD.Print("All .tres files created in resources/fighting_styles/ and resources/items/");
            GD.Print("Assign them in world.tscn and player.tscn via the inspector.");

            QueueFree();
        }

        private static AttackData Atk(int type, int input, float damage, float stamina, float range,
            float speed, float hitDur, float recovery, float knockback, float stun,
            bool breaks, bool launches)
        {
            return new AttackData
            {
                Type = (AttackType)type,
                Input = (AttackInput)input,
                Damage = damage,
                StaminaCost = stamina,
                Range = range,
                Speed = speed,
                HitDuration = hitDur,
                RecoveryTime = recovery,
                KnockbackForce = knockback,
                StunDuration = stun,
                BreaksGuard = breaks,
                LaunchesEnemy = launches
            };
        }

        private static ComboData Cmb(string name, AttackInput[] inputs, float dmgMult, float knockback,
            bool launches, bool unblockable)
        {
            return new ComboData
            {
                ComboName = name,
                InputSequence = inputs,
                DamageMultiplier = dmgMult,
                FinalHitKnockback = knockback,
                LaunchesEnemy = launches,
                Unblockable = unblockable
            };
        }

        private static void CreateStyle(string filename, string name, string desc,
            float dmgMult, float spdMult, float defMult, float stamRegen,
            AttackData lightPunch, AttackData heavyPunch, AttackData lightKick,
            AttackData heavyKick, AttackData uppercut, AttackData knee,
            AttackData elbow, AttackData special, ComboData[] combos)
        {
            var style = new FightingStyleData
            {
                StyleName = name,
                Description = desc,
                DamageMultiplier = dmgMult,
                SpeedMultiplier = spdMult,
                DefenseMultiplier = defMult,
                StaminaRegenMultiplier = stamRegen,
                LightPunch = lightPunch,
                HeavyPunch = heavyPunch,
                LightKick = lightKick,
                HeavyKick = heavyKick,
                Uppercut = uppercut,
                Knee = knee,
                Elbow = elbow,
                SpecialAttack = special,
                Combos = combos
            };

            string path = $"res://resources/fighting_styles/{filename}.tres";
            Error err = ResourceSaver.Save(style, path);
            GD.Print(err == Error.Ok ? $"  Style: {path}" : $"  FAILED Style: {path} (error {err})");
        }

        private static void CreateWeapon(string filename, string name, string desc,
            float damage, float speed, float rangeMult, ItemRarity rarity, float knockback)
        {
            var w = new WeaponData
            {
                ItemName = name,
                Description = desc,
                Rarity = rarity,
                Type = ItemType.Weapon,
                DamageBonus = damage,
                SpeedPenalty = 1f - speed,
                RangeBonus = rangeMult - 1f,
                KnockbackBonus = knockback,
                Durability = 100f,
                BreaksOnUse = false,
                IsConsumable = false
            };
            string path = $"res://resources/items/{filename}.tres";
            Error err = ResourceSaver.Save(w, path);
            GD.Print(err == Error.Ok ? $"  Weapon: {path}" : $"  FAILED Weapon: {path} (error {err})");
        }

        private static void CreateBuff(string filename, string name, string desc,
            BuffType type, float value, float duration, ItemRarity rarity)
        {
            var b = new BuffData
            {
                ItemName = name,
                Description = desc,
                Rarity = rarity,
                Type = ItemType.Buff,
                BuffType = type,
                BuffValue = value,
                BuffDuration = duration,
                IsConsumable = true
            };
            string path = $"res://resources/items/{filename}.tres";
            Error err = ResourceSaver.Save(b, path);
            GD.Print(err == Error.Ok ? $"  Buff: {path}" : $"  FAILED Buff: {path} (error {err})");
        }

        private static void CreateHealing(string filename, string name, string desc,
            float healAmount, ItemRarity rarity)
        {
            var h = new HealingData
            {
                ItemName = name,
                Description = desc,
                Rarity = rarity,
                Type = ItemType.Healing,
                HealAmount = healAmount,
                IsConsumable = true
            };
            string path = $"res://resources/items/{filename}.tres";
            Error err = ResourceSaver.Save(h, path);
            GD.Print(err == Error.Ok ? $"  Healing: {path}" : $"  FAILED Healing: {path} (error {err})");
        }
    }
}
#endif
