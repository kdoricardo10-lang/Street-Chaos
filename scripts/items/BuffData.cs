using Godot;

namespace StreetChaos
{
    public enum BuffType
    {
        Damage,
        Speed,
        Defense,
        StaminaRegen,
        Jump,
        AttackSpeed,
        Invisibility
    }

    [GlobalClass]
    public partial class BuffData : ItemData
    {
        [ExportGroup("Buff Stats")]
        [Export] public BuffType BuffType { get; set; } = BuffType.Damage;
        [Export] public float BuffValue { get; set; } = 0.2f;
        [Export] public float BuffDuration { get; set; } = 30f;
    }
}
