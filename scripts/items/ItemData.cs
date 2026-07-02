using Godot;

namespace StreetChaos
{
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum ItemType
    {
        Weapon,
        Buff,
        Technique,
        Healing,
        Armor
    }

    [GlobalClass]
    public partial class ItemData : Resource
    {
        [Export] public string ItemName { get; set; } = "";
        [Export] public string Description { get; set; } = "";
        [Export] public ItemType Type { get; set; } = ItemType.Buff;
        [Export] public ItemRarity Rarity { get; set; } = ItemRarity.Common;
        [Export] public Texture2D Icon { get; set; }
        [Export] public PackedScene WorldModel { get; set; }
        [Export] public float Duration { get; set; } = 0f;
        [Export] public bool IsConsumable { get; set; } = true;
    }
}
