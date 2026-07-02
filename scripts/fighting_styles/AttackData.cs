using Godot;

namespace StreetChaos
{
    public enum AttackType
    {
        LightPunch,
        HeavyPunch,
        LightKick,
        HeavyKick,
        Uppercut,
        Knee,
        Elbow,
        Special
    }

    public enum AttackInput
    {
        Light,
        Heavy,
        KickLight,
        KickHeavy,
        Uppercut,
        Special
    }

    [GlobalClass]
    public partial class AttackData : Resource
    {
        [Export] public AttackType Type { get; set; } = AttackType.LightPunch;
        [Export] public AttackInput Input { get; set; } = AttackInput.Light;
        [Export] public string AnimationName { get; set; } = "";
        [Export] public float Damage { get; set; } = 10f;
        [Export] public float StaminaCost { get; set; } = 10f;
        [Export] public float Range { get; set; } = 2f;
        [Export] public float Speed { get; set; } = 1f;
        [Export] public float HitDuration { get; set; } = 0.15f;
        [Export] public float RecoveryTime { get; set; } = 0.3f;
        [Export] public float KnockbackForce { get; set; } = 5f;
        [Export] public float StunDuration { get; set; } = 0.3f;
        [Export] public bool LaunchesEnemy { get; set; } = false;
        [Export] public bool CanBeBlocked { get; set; } = true;
        [Export] public bool BreaksGuard { get; set; } = false;
        [Export] public Godot.Vector3 HitboxSize { get; set; } = new Godot.Vector3(1, 1, 1.5f);
    }
}
