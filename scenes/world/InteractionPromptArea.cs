using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    public static class RarityColors
    {
        public static Color GetColor(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => new Color(1, 1, 1, 1),
                ItemRarity.Uncommon => new Color(0.12f, 1, 0, 1),
                ItemRarity.Rare => new Color(0, 0.44f, 1, 1),
                ItemRarity.Epic => new Color(0.64f, 0.21f, 0.93f, 1),
                ItemRarity.Legendary => new Color(1, 0.5f, 0, 1),
                _ => new Color(1, 1, 1, 1)
            };
        }

        public static string GetLabel(ItemRarity rarity)
        {
            return rarity switch
            {
                ItemRarity.Common => "COMUM",
                ItemRarity.Uncommon => "INCOMUM",
                ItemRarity.Rare => "RARO",
                ItemRarity.Epic => "ÉPICO",
                ItemRarity.Legendary => "LENDÁRIO",
                _ => ""
            };
        }
    }
}

namespace StreetChaos
{
    public partial class InteractionPromptArea : Area3D
    {
        [Export] public float Radius { get; set; } = 1.5f;

        private CollisionShape3D _shape;
        private Sprite3D _promptSprite;
        private SubViewport _viewport;
        private Control _rootControl;
        private Label _keyLabel;
        private Label _nameLabel;
        private Label _descLabel;
        private PlayerController _currentPlayer;
        private Panel _promptPanel;
 
        private const float PromptOffsetY = 2.2f;
        private const int ViewportW = 300;
        private const int ViewportH = 130;

        public override void _Ready()
        {
            Monitoring = true;
            Monitorable = false;
            CollisionLayer = 0;
            CollisionMask = 1;

            _shape = GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
            if (_shape == null)
            {
                _shape = new CollisionShape3D { Name = "CollisionShape3D" };
                AddChild(_shape);
            }
            _shape.Shape = new SphereShape3D { Radius = Radius };

            CreatePrompt();
            BodyEntered += OnBodyEntered;
            BodyExited += OnBodyExited;
        }

        private void CreatePrompt()
        {
            _promptSprite = new Sprite3D();
            _promptSprite.Name = "PromptSprite";
            _promptSprite.Position = new Vector3(0, PromptOffsetY, 0);
            _promptSprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
            _promptSprite.PixelSize = 0.006f;
            _promptSprite.Centered = true;
            _promptSprite.Visible = false;
            AddChild(_promptSprite);

            _viewport = new SubViewport();
            _viewport.Name = "PromptViewport";
            _viewport.Size = new Vector2I(ViewportW, ViewportH);
            _viewport.TransparentBg = true;
            _viewport.Disable3D = true;
            _viewport.HandleInputLocally = false;
            _promptSprite.AddChild(_viewport);

            _promptSprite.Texture = _viewport.GetTexture();

            _rootControl = new Control();
            _rootControl.Size = new Vector2(ViewportW, ViewportH);
            _viewport.AddChild(_rootControl);

            _promptPanel = new Panel();
            _promptPanel.Size = _rootControl.Size;
            _promptPanel.Position = Vector2.Zero;

            var bg = new StyleBoxFlat();
            bg.BgColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
            bg.BorderColor = new Color(0.85f, 0.4f, 0.05f, 1f);
            bg.BorderWidthTop = 2;
            bg.BorderWidthBottom = 2;
            bg.BorderWidthLeft = 2;
            bg.BorderWidthRight = 2;
            bg.CornerRadiusTopLeft = 8;
            bg.CornerRadiusTopRight = 8;
            bg.CornerRadiusBottomLeft = 8;
            bg.CornerRadiusBottomRight = 8;
            _promptPanel.AddThemeStyleboxOverride("panel", bg);
            _rootControl.AddChild(_promptPanel);

            var vbox = new VBoxContainer();
            vbox.Size = new Vector2(ViewportW - 24, ViewportH - 16);
            vbox.Position = new Vector2(12, 8);
            vbox.AddThemeConstantOverride("separation", 2);
            _rootControl.AddChild(vbox);

            _keyLabel = new Label();
            _keyLabel.Text = "[F]";
            _keyLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _keyLabel.AddThemeFontSizeOverride("font_size", 22);
            _keyLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.4f, 0.05f));
            _keyLabel.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
            _keyLabel.AddThemeConstantOverride("outline_size", 1);
            vbox.AddChild(_keyLabel);

            _nameLabel = new Label();
            _nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _nameLabel.AddThemeFontSizeOverride("font_size", 18);
            _nameLabel.AddThemeColorOverride("font_color", Colors.White);
            _nameLabel.AutowrapMode = TextServer.AutowrapMode.Off;
            vbox.AddChild(_nameLabel);

            _descLabel = new Label();
            _descLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _descLabel.AddThemeFontSizeOverride("font_size", 13);
            _descLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
            _descLabel.AutowrapMode = TextServer.AutowrapMode.Word;
            vbox.AddChild(_descLabel);
        }

        private void OnBodyEntered(Node3D body)
        {
            if (body is not PlayerController player)
                return;

            _currentPlayer = player;
            ShowPrompt();
        }

        private void OnBodyExited(Node3D body)
        {
            if (body is not PlayerController player || _currentPlayer != player)
                return;

            _currentPlayer = null;
            HidePrompt();
        }

        private void ShowPrompt()
        {
            var target = ResolveTarget();
            if (target == null)
                return;

            _nameLabel.Text = target.InteractionPrompt;
            _descLabel.Text = target.InteractionDescription ?? "";

            Color borderColor;
            if (target is PickupItem p && p.ItemData != null)
            {
                borderColor = RarityColors.GetColor(p.ItemData.Rarity);
                _nameLabel.AddThemeColorOverride("font_color", borderColor);
            }
            else
            {
                borderColor = new Color(0.85f, 0.4f, 0.05f, 1f);
                _nameLabel.AddThemeColorOverride("font_color", Colors.White);
            }

            var bg = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.08f, 0.9f),
                BorderColor = borderColor,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8
            };
            _promptPanel.AddThemeStyleboxOverride("panel", bg);

            _promptSprite.Visible = true;
        }

        public void HidePrompt()
        {
            _promptSprite.Visible = false;
        }

        public void RefreshPrompt()
        {
            if (GetOverlappingBodies().Count > 0)
                ShowPrompt();
        }

        private InteractionPromptTarget ResolveTarget()
        {
            Node node = GetParent();
            while (node != null)
            {
                if (node is InteractionPromptTarget target)
                    return target;

                node = node.GetParent();
            }

            return null;
        }
    }

    public interface InteractionPromptTarget
    {
        string InteractionPrompt { get; }
        string InteractionDescription { get; }
    }
}
