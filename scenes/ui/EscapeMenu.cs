using Godot;

namespace StreetChaos
{
    public partial class EscapeMenu : Control
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
            CreateBorders();
            CreateTitle();
            CreateQuitButton();

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
        }

        private void CreateBorders()
        {
            var borderColor = new Color(0.9f, 0.1f, 0.1f, 1f);
            for (int i = 0; i < 4; i++)
            {
                var border = new ColorRect();
                border.Color = borderColor;
                border.MouseFilter = MouseFilterEnum.Ignore;
                AddChild(border);
            }
        }

        private void CreateTitle()
        {
            var title = new Label();
            title.Name = "TitleLabel";
            title.Text = "MENU";
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.VerticalAlignment = VerticalAlignment.Center;
            title.AddThemeFontSizeOverride("font_size", FontSize(72));
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.1f, 0.1f, 1f));
            AddChild(title);
        }

        private void CreateQuitButton()
        {
            var quitBtn = new Button();
            quitBtn.Name = "QuitButton";
            quitBtn.Text = "SAIR DA PARTIDA";
            quitBtn.Pressed += OnQuitPressed;
            AddChild(quitBtn);

            var resumeBtn = new Button();
            resumeBtn.Name = "ResumeButton";
            resumeBtn.Text = "VOLTAR";
            resumeBtn.Pressed += OnResumePressed;
            AddChild(resumeBtn);
        }

        private void OnResumePressed()
        {
            Close();
        }

        private void OnQuitPressed()
        {
            GetTree().Quit();
        }

        public void Close()
        {
            var parent = GetParent();
            QueueFree();
            parent?.QueueFree();

            if (_player != null && IsInstanceValid(_player))
                _player.ResumeFromMenu();
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
                title.Position = new Vector2(pad * sx, 100f * sy);
                title.Size = new Vector2(w - pad * 2 * sx, 100f * sy);
            }

            var quitBtn = GetNodeOrNull<Button>("QuitButton");
            if (quitBtn != null)
            {
                float btnW = 280f * sx;
                float btnH = 50f * sy;
                quitBtn.Size = new Vector2(btnW, btnH);
                quitBtn.Position = new Vector2(w * 0.5f - btnW * 0.5f, h * 0.5f - btnH - 5f * sy);
            }

            var resumeBtn = GetNodeOrNull<Button>("ResumeButton");
            if (resumeBtn != null)
            {
                float btnW = 280f * sx;
                float btnH = 50f * sy;
                resumeBtn.Size = new Vector2(btnW, btnH);
                resumeBtn.Position = new Vector2(w * 0.5f - btnW * 0.5f, h * 0.5f + 5f * sy);
                resumeBtn.AddThemeFontSizeOverride("font_size", FontSize(18));
            }

            title = GetNodeOrNull<Label>("TitleLabel");
            if (title != null)
                title.AddThemeFontSizeOverride("font_size", FontSize(72));

            quitBtn = GetNodeOrNull<Button>("QuitButton");
            if (quitBtn != null)
                quitBtn.AddThemeFontSizeOverride("font_size", FontSize(18));
        }
    }
}
