using Godot;

namespace StreetChaos
{
    [GlobalClass]
    public partial class HealingData : ItemData
    {
        [Export] public float HealAmount { get; set; } = 25f;

        public HealingData()
        {
            Type = ItemType.Healing;
            IsConsumable = true;
        }
    }
}
