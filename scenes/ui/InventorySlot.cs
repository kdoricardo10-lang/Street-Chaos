using Godot;

namespace StreetChaos
{
    public partial class InventorySlot : PanelContainer
    {
        public string SlotType { get; private set; }
        public int SlotIndex { get; private set; }
        public ItemData Item { get; private set; }
        public bool IsSelected { get; set; }

        private Label _keyLabel;
        private Label _nameLabel;
        private float _scale = 1f;

        private static readonly Color SlotEmptyBg = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        private static readonly Color SlotSelectedGlow = new Color(1f, 0.84f, 0f, 1f);
        private static readonly Color SlotBorder = new Color(0.3f, 0.3f, 0.3f, 1f);

        public void SetScale(float scale)
        {
            _scale = scale;
            UpdateVisuals();
        }

        private int FontSize(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * _scale));
        private int BorderW(int baseW) => Mathf.Max(1, Mathf.RoundToInt(baseW * _scale));

        public void Setup(string slotType, int slotIndex, string keyName)
        {
            SlotType = slotType;
            SlotIndex = slotIndex;

            MouseFilter = MouseFilterEnum.Stop;
            Modulate = new Color(1, 1, 1, 0.95f);

            var vbox = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            AddChild(vbox);

            _keyLabel = new Label
            {
                Text = $"[{keyName}]",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                SizeFlagsVertical = SizeFlags.ShrinkEnd
            };
            vbox.AddChild(_keyLabel);

            _nameLabel = new Label
            {
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                SizeFlagsVertical = SizeFlags.Fill | SizeFlags.Expand,
                ClipText = true
            };
            vbox.AddChild(_nameLabel);

            UpdateVisuals();
        }

        public void UpdateItem(ItemData item)
        {
            Item = item;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (Item != null)
            {
                _nameLabel.Text = Item.ItemName.ToUpper();
                _nameLabel.Modulate = RarityColors.GetColor(Item.Rarity);
            }
            else
            {
                _nameLabel.Text = "";
            }

            Color borderColor = SlotBorder;
            Color bg = SlotEmptyBg;
            if (IsSelected)
            {
                bg = new Color(
                    SlotEmptyBg.R * 1.5f,
                    SlotEmptyBg.G * 1.5f,
                    SlotEmptyBg.B * 1.5f,
                    SlotEmptyBg.A);
                borderColor = SlotSelectedGlow;
            }
            else if (Item != null)
            {
                borderColor = RarityColors.GetColor(Item.Rarity);
            }

            AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = bg,
                BorderColor = borderColor,
                BorderWidthBottom = BorderW(2),
                BorderWidthTop = BorderW(3),
                BorderWidthLeft = BorderW(2),
                BorderWidthRight = BorderW(2),
                CornerRadiusTopLeft = BorderW(6),
                CornerRadiusTopRight = BorderW(6),
                CornerRadiusBottomLeft = BorderW(6),
                CornerRadiusBottomRight = BorderW(6),
                ShadowSize = BorderW(4),
                ShadowOffset = new Vector2(0, BorderW(2)),
                ShadowColor = new Color(0f, 0f, 0f, 0.3f)
            });

            if (_keyLabel != null)
                _keyLabel.AddThemeFontSizeOverride("font_size", FontSize(11));
            if (_nameLabel != null)
                _nameLabel.AddThemeFontSizeOverride("font_size", FontSize(10));
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            if (Item == null)
                return new Variant();

            var data = new Godot.Collections.Dictionary
            {
                { "slot_type", SlotType },
                { "slot_index", SlotIndex },
                { "item", Item }
            };

            var preview = new PanelContainer();
            preview.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.2f, 0.2f, 0.2f, 0.8f),
                BorderColor = RarityColors.GetColor(Item.Rarity),
                BorderWidthBottom = BorderW(2),
                BorderWidthTop = BorderW(2),
                BorderWidthLeft = BorderW(2),
                BorderWidthRight = BorderW(2)
            });
            var previewLabel = new Label
            {
                Text = Item.ItemName,
                Modulate = RarityColors.GetColor(Item.Rarity)
            };
            preview.AddChild(previewLabel);
            SetDragPreview(preview);

            return data;
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            var dict = data.AsGodotDictionary();
            string sourceType = dict["slot_type"].AsString();
            return sourceType == SlotType;
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            var dict = data.AsGodotDictionary();
            string sourceType = dict["slot_type"].AsString();
            int sourceIndex = dict["slot_index"].AsInt32();

            var player = GetTree().GetFirstNodeInGroup("players") as PlayerController;
            player?.SwapSlots(sourceType, sourceIndex, SlotType, SlotIndex);
        }
    }
}
