using Godot;
using System;

namespace StreetChaos
{
    public partial class HealthComponent : Node
    {
        [Signal] public delegate void HealthChangedEventHandler(float current, float max);
        [Signal] public delegate void StaminaChangedEventHandler(float current, float max);
        [Signal] public delegate void ShieldChangedEventHandler(float current, float max);
        [Signal] public delegate void DiedEventHandler();
        [Signal] public delegate void TookDamageEventHandler(float damage, Node attacker);

        [Export] public float MaxHealth { get; set; } = 1000f;
        [Export] public float MaxStamina { get; set; } = 100f;
        [Export] public float MaxShield { get; set; } = 0f;
        [Export] public float StaminaRegenRate { get; set; } = 25f;
        [Export] public float StaminaRegenDelay { get; set; } = 1.5f;

        public float CurrentHealth { get; private set; }
        public float CurrentStamina { get; private set; }
        public float CurrentShield { get; private set; }

        private float _staminaDelayTimer;

        public override void _Ready()
        {
            CurrentHealth = MaxHealth;
            CurrentStamina = MaxStamina;
            CurrentShield = MaxShield;
        }

        public override void _Process(double delta)
        {
            if (_staminaDelayTimer > 0)
            {
                _staminaDelayTimer -= (float)delta;
                return;
            }
            if (CurrentStamina < MaxStamina)
            {
                CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + StaminaRegenRate * (float)delta);
                EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
            }
        }

        public bool TakeDamage(float damage, Node attacker = null, bool unblockable = false)
        {
            if (CurrentHealth <= 0) return false;

            // Block check: if player is blocking and damage isn't special, ignore it
            if (!unblockable && IsOwnerBlocking())
                return false;

            float remaining = damage;

            if (CurrentShield > 0)
            {
                float shieldAbsorb = Mathf.Min(CurrentShield, remaining);
                CurrentShield -= shieldAbsorb;
                remaining -= shieldAbsorb;
                EmitSignal(SignalName.ShieldChanged, CurrentShield, MaxShield);
            }

            CurrentHealth -= remaining;
            CurrentHealth = Mathf.Max(0, CurrentHealth);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
            EmitSignal(SignalName.TookDamage, damage, attacker);

            if (CurrentHealth <= 0)
            {
                EmitSignal(SignalName.Died);
                return true;
            }
            return false;
        }

        public bool UseStamina(float amount)
        {
            if (CurrentStamina < amount) return false;
            CurrentStamina -= amount;
            _staminaDelayTimer = StaminaRegenDelay;
            EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
            return true;
        }

        public void RestoreStamina(float amount)
        {
            CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + amount);
            EmitSignal(SignalName.StaminaChanged, CurrentStamina, MaxStamina);
        }

        public void Heal(float amount)
        {
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
            EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
        }

        public void SetShield(float amount)
        {
            MaxShield = amount;
            CurrentShield = amount;
            EmitSignal(SignalName.ShieldChanged, CurrentShield, MaxShield);
        }

        public bool IsAlive() => CurrentHealth > 0;

        /// <summary>
        /// Called by the server to broadcast current health to all clients.
        /// </summary>
        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        public void RpcSyncHealth(float hp, float maxHp, float stamina, float maxStamina, float shield, float maxShield)
        {
            CurrentHealth = hp;
            MaxHealth = maxHp;
            CurrentStamina = stamina;
            MaxStamina = maxStamina;
            CurrentShield = shield;
            MaxShield = maxShield;
            EmitSignal(SignalName.HealthChanged, hp, maxHp);
            EmitSignal(SignalName.StaminaChanged, stamina, maxStamina);
            EmitSignal(SignalName.ShieldChanged, shield, maxShield);
        }

        /// <summary>
        /// Called by the server to broadcast downed/dead state to the owning client.
        /// </summary>
        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        public void RpcSetDowned(bool downed)
        {
            if (downed && CurrentHealth > 0)
            {
                CurrentHealth = 0;
                EmitSignal(SignalName.HealthChanged, 0f, MaxHealth);
                EmitSignal(SignalName.Died);
            }
        }

        /// <summary>
        /// Called by the server to sync HP only (for remote proxy NPCs on clients).
        /// </summary>
        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        public void RpcProxyHealth(float hp, float maxHp)
        {
            CurrentHealth = hp;
            MaxHealth = maxHp;
            EmitSignal(SignalName.HealthChanged, hp, maxHp);
        }

        /// <summary>
        /// Server-side only: applies damage and broadcasts result.
        /// </summary>
        public bool ServerTakeDamage(float damage, Node attacker = null, bool unblockable = false)
        {
            bool died = TakeDamage(damage, attacker, unblockable);
            if (Multiplayer.MultiplayerPeer != null && Multiplayer.IsServer())
            {
                Rpc(nameof(RpcSyncHealth), CurrentHealth, MaxHealth, CurrentStamina, MaxStamina, CurrentShield, MaxShield);
                if (died)
                    Rpc(nameof(RpcSetDowned), true);
            }
            return died;
        }

        public void BroadcastHealth()
        {
            if (Multiplayer.MultiplayerPeer != null && Multiplayer.IsServer())
            {
                Rpc(nameof(RpcSyncHealth), CurrentHealth, MaxHealth, CurrentStamina, MaxStamina, CurrentShield, MaxShield);
            }
        }

        private bool IsOwnerBlocking()
        {
            var owner = GetParent();
            if (owner == null) return false;
            var combat = owner.GetNodeOrNull<CombatComponent>("CombatComponent");
            return combat != null && combat.IsBlocking;
        }
    }
}
