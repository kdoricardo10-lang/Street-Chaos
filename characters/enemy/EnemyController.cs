using Godot;

namespace StreetChaos
{
    public partial class EnemyController : CharacterBody3D
    {
        [Export] public float MoveSpeed { get; set; } = 3f;
        [Export] public float DetectionRadius { get; set; } = 20f;
        [Export] public float AttackRange { get; set; } = 2f;
        [Export] public float AttackCooldown { get; set; } = 1.5f;
        [Export] public float AttackDamage { get; set; } = 8f;
        [Export] public float KnockbackForce { get; set; } = 5f;

        private HealthComponent _health;
        private Node3D _target;
        private float _attackTimer;

        public override void _Ready()
        {
            _health = new HealthComponent();
            _health.Name = "HealthComponent";
            _health.MaxHealth = 50f;
            AddChild(_health);

            _health.Died += OnDied;

        }

        public override void _PhysicsProcess(double delta)
        {
            float dt = (float)delta;

            if (_health != null && !_health.IsAlive())
                return;

            FindTarget();

            if (_target != null)
            {
                Vector3 dir = (_target.GlobalPosition - GlobalPosition);
                float dist = dir.Length();
                dir.Y = 0;
                dir = dir.Normalized();

                if (dist > AttackRange)
                {
                    Vector3 vel = Velocity;
                    vel.X = dir.X * MoveSpeed;
                    vel.Z = dir.Z * MoveSpeed;
                    vel.Y += GetGravity().Y * dt;
                    Velocity = vel;

                    if (dir.Length() > 0.01f)
                    {
                        float targetYaw = Mathf.Atan2(dir.X, dir.Z);
                        Rotation = new Vector3(0, targetYaw, 0);
                    }
                }
                else
                {
                    Velocity = new Vector3(0, Velocity.Y + GetGravity().Y * dt, 0);
                    TryAttack(dt);
                }
            }
            else
            {
                Velocity = new Vector3(0, Velocity.Y + GetGravity().Y * dt, 0);
            }

            MoveAndSlide();
        }

        private void FindTarget()
        {
            _target = null;
            float closestDist = DetectionRadius;

            var players = GetTree().GetNodesInGroup("players");
            foreach (var p in players)
            {
                if (p == this) continue;
                if (p is not Node3D node) continue;

                float d = GlobalPosition.DistanceTo(node.GlobalPosition);
                if (d < closestDist)
                {
                    closestDist = d;
                    _target = node;
                }
            }

            if (_target == null)
            {
                var gm = GameManager.Instance;
                if (gm?.CurrentPlayer is Node3D targetNode && targetNode != this)
                {
                    float d = GlobalPosition.DistanceTo(targetNode.GlobalPosition);
                    if (d < DetectionRadius)
                        _target = targetNode;
                }
            }
        }

        private void TryAttack(float delta)
        {
            _attackTimer -= delta;
            if (_attackTimer > 0) return;

            _attackTimer = AttackCooldown;

            if (_target == null) return;

            var health = _target.GetNode<HealthComponent>("HealthComponent");
            if (health != null)
            {
                health.TakeDamage(AttackDamage, this);

                if (_target is PlayerController player)
                    player.ApplyKnockback(
                        (_target.GlobalPosition - GlobalPosition).Normalized() * KnockbackForce);
            }
        }

        private void OnDied()
        {
            SetCollisionLayerValue(1, false);
            SetCollisionMaskValue(1, false);

            var tween = CreateTween();
            tween.TweenProperty(this, "scale", Vector3.Zero, 0.5f);
            tween.TweenCallback(Callable.From(QueueFree));
        }
    }
}
