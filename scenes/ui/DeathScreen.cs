using Godot;

namespace StreetChaos
{
    public partial class DeathScreen : Control
    {
        private PlayerController _player;
        private float _scale = 1f;

        private static readonly float RefW = 1920f;
        private static readonly float RefH = 1080f;

        private int FontSize(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * _scale));
        private int BorderW(int baseW) => Mathf.Max(1, Mathf.RoundToInt(baseW * _scale));

        public void SetPlayer(PlayerController player)
        {
            _player = player;
        }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            CreateBackdrop();
            CreateTitle();
            CreateButtons();

            Resized += RecalculateLayout;
            CallDeferred(nameof(RecalculateLayout));
            ProcessMode = ProcessModeEnum.Always;
        }

        private void CreateBackdrop()
        {
            var backdrop = new ColorRect();
            backdrop.Color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
            backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            backdrop.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(backdrop);

            var borderColor = new Color(0.9f, 0.1f, 0.1f, 1f);

            var topBorder = new ColorRect();
            topBorder.Color = borderColor;
            topBorder.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(topBorder);

            var bottomBorder = new ColorRect();
            bottomBorder.Color = borderColor;
            bottomBorder.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(bottomBorder);

            var leftBorder = new ColorRect();
            leftBorder.Color = borderColor;
            leftBorder.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(leftBorder);

            var rightBorder = new ColorRect();
            rightBorder.Color = borderColor;
            rightBorder.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(rightBorder);
        }

        private void CreateTitle()
        {
            var title = new Label();
            title.Name = "TitleLabel";
            title.Text = "JOGOU MAL";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.VerticalAlignment = VerticalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", FontSize(72));
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.1f, 0.1f, 1f));
            AddChild(title);
        }

        private void CreateButtons()
        {
            var spectateBtn = new Button();
            spectateBtn.Name = "SpectateButton";
            spectateBtn.Text = "ESPECTAR";
            spectateBtn.Pressed += OnSpectatePressed;
            AddChild(spectateBtn);

            var quitBtn = new Button();
            quitBtn.Name = "QuitButton";
            quitBtn.Text = "SAIR";
            quitBtn.Pressed += OnQuitPressed;
            AddChild(quitBtn);
        }

        private void OnSpectatePressed()
        {
            var parent = GetParent();
            QueueFree();
            parent?.QueueFree();
            if (_player != null && IsInstanceValid(_player))
            {
                _player.EnterSpectateMode();
            }
        }

        private void OnQuitPressed()
        {
            GetTree().Quit();
        }

        private void RecalculateLayout()
        {
            float w = Size.X;
            float h = Size.Y;
            float sx = w / RefW;
            float sy = h / RefH;
            _scale = Mathf.Min(sx, sy);
            float bt = BorderW(6);

            int i = 0;
            foreach (var child in GetChildren())
            {
                if (child is ColorRect cr && cr.Color.R > 0.5f)
                {
                    switch (i)
                    {
                        case 0:
                            cr.Position = Vector2.Zero;
                            cr.Size = new Vector2(w, bt);
                            break;
                        case 1:
                            cr.Position = new Vector2(0, h - bt);
                            cr.Size = new Vector2(w, bt);
                            break;
                        case 2:
                            cr.Position = Vector2.Zero;
                            cr.Size = new Vector2(bt, h);
                            break;
                        case 3:
                            cr.Position = new Vector2(w - bt, 0);
                            cr.Size = new Vector2(bt, h);
                            break;
                    }
                    i++;
                }
            }

            var title = GetNodeOrNull<Label>("TitleLabel");
            if (title != null)
            {
                float pad = 200f;
                title.Position = new Vector2(pad * sx, pad * sy);
                title.Size = new Vector2(w - pad * 2 * sx, h - pad * 2 * sy);
            }

            var spectateBtn = GetNodeOrNull<Button>("SpectateButton");
            if (spectateBtn != null)
            {
                float btnW = 180f * sx;
                float btnH = 50f * sy;
                spectateBtn.Size = new Vector2(btnW, btnH);
                spectateBtn.Position = new Vector2(w - btnW - 40f * sx, h * 0.5f - btnH - 5f * sy);
            }

            var quitBtn = GetNodeOrNull<Button>("QuitButton");
            if (quitBtn != null)
            {
                float btnW = 180f * sx;
                float btnH = 50f * sy;
                quitBtn.Size = new Vector2(btnW, btnH);
                quitBtn.Position = new Vector2(w - btnW - 40f * sx, h * 0.5f + 5f * sy);
                quitBtn.AddThemeFontSizeOverride("font_size", FontSize(18));
            }

            if (title != null)
                title.AddThemeFontSizeOverride("font_size", FontSize(72));

            spectateBtn = GetNodeOrNull<Button>("SpectateButton");
            if (spectateBtn != null)
                spectateBtn.AddThemeFontSizeOverride("font_size", FontSize(18));
        }
    }
}
