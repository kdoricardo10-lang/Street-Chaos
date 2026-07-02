using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    public partial class CombatComponent : Node
    {
        [Signal] public delegate void AttackStartedEventHandler(AttackData attack);
        [Signal] public delegate void AttackHitEventHandler(Node target, AttackData attack);
        [Signal] public delegate void ComboCompletedEventHandler(ComboData combo);
        [Signal] public delegate void BlockedEventHandler(Node attacker);
        [Signal] public delegate void GrabbedEventHandler(Node target);

        [Export] public PackedScene HitSparkEffect { get; set; }

        private PlayerController _player;
        private HealthComponent _health;
        private ComboSystem _comboSystem;
        private FightingStyleData _currentStyle;
        private AttackData _currentAttack;
        public AttackData CurrentAttack => _currentAttack;
        private float _attackTimer;
        private float _recoveryTimer;
        private bool _isInHitFrame;
        private Area3D _hitbox;
        private float _hitWindowTimer;
        private WeaponData _equippedWeapon;
        private Node _grabbedTarget;
        private Node _grabber;
        private readonly System.Collections.Generic.HashSet<Node> _hitTargets = new();

        public bool IsAttacking => _attackTimer > 0 || _recoveryTimer > 0;
        public bool IsBlocking { get; private set; }
        public bool IsGrabbing => _grabbedTarget != null;
        public bool IsGrabbed => _grabber != null;
        public bool IsInHitFrame => _isInHitFrame;
        public FightingStyleData CurrentStyle => _currentStyle;
        public WeaponData EquippedWeapon => _equippedWeapon;

        public override void _Ready()
        {
            _player = GetParent<PlayerController>();
            _health = _player?.GetNode<HealthComponent>("HealthComponent");
            _comboSystem = new ComboSystem();
            AddChild(_comboSystem);

            CreateHitbox();
        }

        private void CreateHitbox()
        {
            var hitbox = new Area3D();
            hitbox.Name = "AttackHitbox";
            hitbox.CollisionMask = 2;
            hitbox.CollisionLayer = 0;
            var shape = new CollisionShape3D();
            shape.Shape = new BoxShape3D();
            hitbox.AddChild(shape);

            var hc = _player?.GetNode<Node3D>("HitboxContainer");
            if (hc != null)
            {
                hc.AddChild(hitbox);
                SetHitbox(hitbox);
            }
        }

        private void SetupHitbox(AttackData attack)
        {
            if (_hitbox == null) return;
            if (_hitbox.GetChild(0) is CollisionShape3D cs && cs.Shape is BoxShape3D box)
            {
                Vector3 size = attack.HitboxSize;
                size.Z = Mathf.Min(size.Z, 0.6f);
                box.Size = size;
            }
            float closeRange = Mathf.Min(attack.Range, 1.0f);
            float yaw = _player.ModelPivot?.Rotation.Y ?? 0f;
            _hitbox.Position = new Vector3(0, 0.5f, closeRange / 2f).Rotated(Vector3.Up, yaw);
            _hitbox.Rotation = new Vector3(0, yaw, 0);
            _hitWindowTimer = attack.HitDuration;
            EnableHitbox();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            if (_attackTimer > 0)
            {
                _attackTimer -= dt;
            }

            if (_hitWindowTimer > 0)
            {
                _hitWindowTimer -= dt;
                if (_hitWindowTimer <= 0)
                    DisableHitbox();
            }

            if (_recoveryTimer > 0)
            {
                _recoveryTimer -= dt;
                if (_recoveryTimer <= 0)
                {
                    _recoveryTimer = 0;
                    _currentAttack = null;
                }
            }

        }

        public override void _PhysicsProcess(double delta)
        {
        }

        public void SetFightingStyle(FightingStyleData style)
        {
            _currentStyle = style;
        }

        public void EquipWeapon(WeaponData weapon)
        {
            _equippedWeapon = weapon;
        }

        public void UnequipWeapon()
        {
            _equippedWeapon = null;
        }

        public bool PerformAttack(AttackInput input, bool detectCombo = true)
        {
            if (_currentStyle == null) return false;
            if (IsAttacking || IsGrabbed) return false;

            return StartAttack(input, detectCombo);
        }

        public bool ChainAttack(AttackInput input, bool detectCombo = true)
        {
            if (_currentStyle == null) return false;
            if (IsGrabbed) return false;

            return StartAttack(input, detectCombo);
        }

        private bool StartAttack(AttackInput input, bool detectCombo)
        {
            AttackData attack = _currentStyle.GetAttack(input);
            if (attack == null) return false;

            if (detectCombo)
            {
                _comboSystem.RegisterInput(input, _currentStyle);

                ComboData combo = _comboSystem.CurrentCombo;
                if (combo != null && _comboSystem.CurrentComboStep >= combo.InputSequence.Length)
                {
                    PerformComboAttack(combo);
                    return true;
                }
            }

            _currentAttack = attack;
            _attackTimer = attack.HitDuration + attack.RecoveryTime;
            _recoveryTimer = attack.RecoveryTime;
            SetupHitbox(attack);

            EmitSignal(SignalName.AttackStarted, attack);
            _player?.SetAnimationState("attack");

            return true;
        }

        private void PerformComboAttack(ComboData combo)
        {
            var baseAttack = _currentStyle.LightPunch;
            if (baseAttack == null) return;

            _currentAttack = new AttackData
            {
                Type = baseAttack.Type,
                Input = baseAttack.Input,
                AnimationName = baseAttack.AnimationName,
                Damage = baseAttack.Damage * combo.DamageMultiplier,
                StaminaCost = baseAttack.StaminaCost * combo.StaminaCostMultiplier,
                Range = baseAttack.Range,
                Speed = baseAttack.Speed,
                HitDuration = baseAttack.HitDuration,
                RecoveryTime = baseAttack.RecoveryTime,
                KnockbackForce = combo.FinalHitKnockback,
                StunDuration = baseAttack.StunDuration,
                LaunchesEnemy = combo.LaunchesEnemy,
                CanBeBlocked = !combo.Unblockable && baseAttack.CanBeBlocked,
                BreaksGuard = combo.Unblockable || baseAttack.BreaksGuard,
                HitboxSize = baseAttack.HitboxSize
            };

            _attackTimer = _currentAttack.HitDuration + _currentAttack.RecoveryTime;
            _recoveryTimer = _currentAttack.RecoveryTime;
            SetupHitbox(_currentAttack);

            EmitSignal(SignalName.AttackStarted, _currentAttack);
            EmitSignal(SignalName.ComboCompleted, combo);
            _player?.SetAnimationState("attack");

            _comboSystem.Reset();
        }

        public void StartBlock()
        {
            if (IsAttacking || IsGrabbed) return;
            IsBlocking = true;
            _player?.SetAnimationState("block");
        }

        public void StopBlock()
        {
            IsBlocking = false;
        }

        public void TryDodge()
        {
            if (IsAttacking || IsGrabbed) return;
            if (_health != null && !_health.UseStamina(20f)) return;

            _player?.SetAnimationState("dodge");
        }

        public bool TryGrab(Node target)
        {
            if (IsAttacking || IsGrabbed || target == null) return false;
            if (_health != null && !_health.UseStamina(15f)) return false;

            _grabbedTarget = target;
            EmitSignal(SignalName.Grabbed, target);
            _player?.SetAnimationState("grab");

            if (target is PlayerController targetPlayer)
                targetPlayer.OnGrabbed(_player);

            return true;
        }

        public void ReleaseGrab()
        {
            if (_grabbedTarget != null && _grabbedTarget is PlayerController targetPlayer)
                targetPlayer.OnReleased();

            _grabbedTarget = null;
        }

        public void ThrowGrabbedTarget()
        {
            if (_grabbedTarget == null) return;

            if (_grabbedTarget is PlayerController target)
            {
                Vector3 throwDir = (target.GlobalPosition - _player.GlobalPosition).Normalized();
                throwDir.Y = 0.3f;
                target.OnThrown(throwDir * 15f);
            }

            _grabbedTarget = null;
        }

        public void OnGrabbedBy(Node attacker)
        {
            _grabber = attacker;
            _player?.SetAnimationState("grabbed");
        }

        public void OnReleased()
        {
            _grabber = null;
        }

        public void OnThrown(Vector3 velocity)
        {
            _grabber = null;
            _player?.ApplyKnockback(velocity);
        }

        public void EnableHitbox()
        {
            if (_hitbox == null) return;
            _isInHitFrame = true;
            _hitbox.Monitoring = true;
            _hitbox.Monitorable = true;
            _hitTargets.Clear();

            Callable.From(() =>
            {
                if (_isInHitFrame && _hitbox != null)
                    foreach (var area in _hitbox.GetOverlappingAreas())
                        ProcessHit(area);
            }).CallDeferred();
        }

        public void DisableHitbox()
        {
            if (_hitbox == null) return;
            _isInHitFrame = false;
            _hitbox.Monitoring = false;
            _hitbox.Monitorable = false;
        }

        public void SetHitbox(Area3D hitbox)
        {
            _hitbox = hitbox;
            DisableHitbox();
            if (_hitbox != null)
                _hitbox.AreaEntered += OnHitboxEntered;
        }

        private void OnHitboxEntered(Area3D area)
        {
            if (!_isInHitFrame || _currentAttack == null) return;
            ProcessHit(area);
        }

        private void ProcessHit(Area3D area)
        {
            if (!_isInHitFrame || _currentAttack == null) return;

            Node target = ResolveHitTarget(area);
            if (target == null || target == _player) return;

            if (!_hitTargets.Add(target)) return;

            float damage = _currentAttack.Damage;
            if (_currentStyle != null)
                damage *= _currentStyle.DamageMultiplier;
            if (_equippedWeapon != null)
                damage += _equippedWeapon.DamageBonus;

            bool isNetworked = NetworkManager.Instance?.IsNetworkConnected == true;

            if (target is PlayerController targetPlayer)
            {
                bool blocked = false;
                var targetCombat = targetPlayer.GetNode<CombatComponent>("CombatComponent");
                if (targetCombat != null && targetCombat.IsBlocking)
                {
                    if (_currentAttack.BreaksGuard)
                    {
                        targetCombat.StopBlock();
                        targetPlayer.ApplyStun(_currentAttack.StunDuration);
                    }
                    else
                    {
                        blocked = true;
                        var targetHealth = targetPlayer.GetNode<HealthComponent>("HealthComponent");
                        if (targetHealth != null && !targetHealth.UseStamina(damage * 0.5f))
                        {
                            targetCombat.StopBlock();
                            targetPlayer.ApplyStun(_currentAttack.StunDuration);
                        }
                        EmitSignal(SignalName.Blocked, targetPlayer);
                    }
                }

                if (!blocked)
                {
                    if (isNetworked)
                    {
                        // Send hit to server for validation and application
                        int victimPeerId = targetPlayer.GetMultiplayerAuthority();
                        _player.Rpc(nameof(PlayerController.ReportHitServer), victimPeerId, damage, _currentAttack.BreaksGuard);
                    }
                    else
                    {
                        // Single player: apply locally
                        var targetHealth = targetPlayer.GetNode<HealthComponent>("HealthComponent");
                        if (targetHealth != null)
                            targetHealth.TakeDamage(damage, _player, _currentAttack.BreaksGuard);
                    }

                    if (_currentAttack.StunDuration > 0)
                        targetPlayer.ApplyStun(_currentAttack.StunDuration);

                    if (_currentAttack.KnockbackForce > 0)
                    {
                        Vector3 kbDir = (targetPlayer.GlobalPosition - _player.GlobalPosition).Normalized();
                        kbDir.Y = 0.1f;
                        targetPlayer.ApplyKnockback(kbDir * _currentAttack.KnockbackForce);

                        if (_equippedWeapon != null)
                            targetPlayer.ApplyKnockback(kbDir * _equippedWeapon.KnockbackBonus);
                    }

                    if (_currentAttack.LaunchesEnemy)
                        targetPlayer.ApplyKnockback(Vector3.Up * 8f);
                }

                EmitSignal(SignalName.AttackHit, targetPlayer, _currentAttack);
                SpawnHitEffect(targetPlayer.GlobalPosition);
            }
        }

        private static Node ResolveHitTarget(Area3D area)
        {
            Node owner = area.GetOwner();
            if (owner is PlayerController || owner is NpcController)
                return owner;

            Node current = area.GetParent();
            while (current != null)
            {
                if (current is PlayerController || current is NpcController)
                    return current;
                current = current.GetParent();
            }

            return null;
        }

        private void SpawnHitEffect(Vector3 position)
        {
            if (HitSparkEffect == null) return;
            var effect = HitSparkEffect.Instantiate<Node3D>();
            GetTree().Root.AddChild(effect);
            effect.GlobalPosition = position;
        }

        public void ResetCombat()
        {
            _currentAttack = null;
            _attackTimer = 0;
            _recoveryTimer = 0;
            _isInHitFrame = false;
            IsBlocking = false;
            _grabbedTarget = null;
            _grabber = null;
            _comboSystem.Reset();
            DisableHitbox();
        }
    }
}
