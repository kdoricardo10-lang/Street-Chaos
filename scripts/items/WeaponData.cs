using Godot;

namespace StreetChaos
{
    [GlobalClass]
    public partial class WeaponData : ItemData
    {
        [ExportGroup("Weapon Stats")]
        [Export] public float DamageBonus { get; set; } = 5f;
        [Export] public float RangeBonus { get; set; } = 1f;
        [Export] public float SpeedPenalty { get; set; } = 0f;
        [Export] public float KnockbackBonus { get; set; } = 3f;
        [Export] public float Durability { get; set; } = 100f;
        [Export] public bool BreaksOnUse { get; set; } = false;
    }
}
