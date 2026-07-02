using Godot;
using System;

namespace StreetChaos
{
    public partial class MainMenu : Control
    {
        private Button _playButton;
        private Button _optionsButton;
        private Button _quitButton;
        private Button _hostButton;
        private Button _joinButton;
        private LineEdit _ipInput;
        private Control _mainButtons;
        private Control _hostJoinButtons;
        private Label _statusLabel;
        private Control _splashContainer;
        private Button _splashButton;
        private bool _splashActive = true;

        public override void _Ready()
        {
            // ── Splash Screen (comes before menu background) ────────────
            _splashContainer = new Control
            {
                AnchorsPreset = (int)LayoutPreset.FullRect
            };
            AddChild(_splashContainer);

            // Background image
            var splashBg = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://splash_bg.png"),
                ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional,
                AnchorsPreset = (int)LayoutPreset.FullRect,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
            };
            _splashContainer.AddChild(splashBg);

            // Dark overlay for readability
            var overlay = new ColorRect
            {
                Color = new Color(0, 0, 0, 0.35f),
                AnchorsPreset = (int)LayoutPreset.FullRect
            };
            _splashContainer.AddChild(overlay);

            var splashTitle = new Label
            {
                Text = "STREET CHAOS",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AnchorLeft = 0.5f, AnchorTop = 0.5f,
                AnchorRight = 0.5f, AnchorBottom = 0.5f,
                OffsetLeft = -300f, OffsetTop = -80f,
                OffsetRight = 300f, OffsetBottom = 80f,
            };
            splashTitle.AddThemeFontSizeOverride("font_size", 96);
            splashTitle.AddThemeColorOverride("font_color", new Color(0.9f, 0.2f, 0.2f, 1.0f));
            splashTitle.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
            splashTitle.AddThemeConstantOverride("outline_size", 4);
            _splashContainer.AddChild(splashTitle);

            _splashButton = new Button
            {
                Text = "APERTE QUALQUER TECLA",
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                AnchorLeft = 0.5f, AnchorTop = 0.75f,
                AnchorRight = 0.5f, AnchorBottom = 0.75f,
                OffsetLeft = -200f, OffsetTop = -20f,
                OffsetRight = 200f, OffsetBottom = 20f,
            };
            _splashButton.AddThemeFontSizeOverride("font_size", 22);
            _splashButton.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 1f));
            _splashButton.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
            _splashButton.AddThemeConstantOverride("outline_size", 2);
            _splashButton.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            _splashButton.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
            _splashButton.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
            _splashButton.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            _splashContainer.AddChild(_splashButton);

            var splashVersion = new Label
            {
                Text = "v0.1.0 Alpha",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                AnchorLeft = 0.5f, AnchorTop = 1.0f,
                AnchorRight = 0.5f, AnchorBottom = 1.0f,
                OffsetLeft = -50f, OffsetTop = -30f,
                OffsetRight = 50f, OffsetBottom = -8f,
            };
            splashVersion.AddThemeFontSizeOverride("font_size", 12);
            splashVersion.AddThemeColorOverride("font_color", new Color(0.8f, 0.8f, 0.8f, 1f));
            splashVersion.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.5f));
            splashVersion.AddThemeConstantOverride("outline_size", 1);
            _splashContainer.AddChild(splashVersion);

            // ── Menu Background (hidden until splash ends) ──────────────
            var menuBg = new ColorRect
            {
                Color = new Color(0.05f, 0.05f, 0.08f, 1.0f),
                AnchorsPreset = (int)LayoutPreset.FullRect,
                Visible = false
            };
            AddChild(menuBg);
            // Use menuBg reference stored as a Control children won't need ref

            // ── Main Buttons Container (hidden until splash ends) ──────
            _mainButtons = new VBoxContainer
            {
                AnchorLeft = 0.5f, AnchorTop = 0.4f,
                AnchorRight = 0.5f, AnchorBottom = 0.4f,
                OffsetLeft = -120f, OffsetTop = 0f,
                OffsetRight = 120f, OffsetBottom = 300f,
                Visible = false
            };
            _mainButtons.AddThemeConstantOverride("separation", 16);
            AddChild(_mainButtons);

            _playButton = CreateMenuButton("JOGAR", ShowHostJoinMenu);
            _optionsButton = CreateMenuButton("OPÇÕES", () => GD.Print("Options - not implemented yet"));
            _quitButton = CreateMenuButton("SAIR", () => GetTree().Quit());

            _mainButtons.AddChild(_playButton);
            _mainButtons.AddChild(_optionsButton);
            _mainButtons.AddChild(_quitButton);

            // ── Host/Join Buttons Container (hidden initially) ──────────
            _hostJoinButtons = new VBoxContainer
            {
                AnchorLeft = 0.5f, AnchorTop = 0.4f,
                AnchorRight = 0.5f, AnchorBottom = 0.4f,
                OffsetLeft = -160f, OffsetTop = 0f,
                OffsetRight = 160f, OffsetBottom = 250f,
                Visible = false
            };
            _hostJoinButtons.AddThemeConstantOverride("separation", 12);
            AddChild(_hostJoinButtons);

            // IP Input
            var ipLabel = new Label
            {
                Text = "Endereço do Servidor:",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _hostJoinButtons.AddChild(ipLabel);

            _ipInput = new LineEdit
            {
                PlaceholderText = "127.0.0.1",
                Text = "127.0.0.1",
                SizeFlagsHorizontal = SizeFlags.Fill,
                MaxLength = 45
            };
            _hostJoinButtons.AddChild(_ipInput);

            var hostBtn = CreateMenuButton("CRIAR PARTIDA", StartHost);
            _hostJoinButtons.AddChild(hostBtn);

            var joinBtn = CreateMenuButton("ENTRAR EM PARTIDA", StartJoin);
            _hostJoinButtons.AddChild(joinBtn);

            var backBtn = CreateMenuButton("VOLTAR", ShowMainMenu);
            _hostJoinButtons.AddChild(backBtn);

            // ── Status Label ─────────────────────────────────────────────
            _statusLabel = new Label
            {
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AnchorLeft = 0.5f, AnchorTop = 0.85f,
                AnchorRight = 0.5f, AnchorBottom = 0.85f,
                OffsetLeft = -300f, OffsetTop = -20f,
                OffsetRight = 300f, OffsetBottom = 20f,
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 18);
            _statusLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.5f, 1f));
            AddChild(_statusLabel);

            // ── Network Signals ─────────────────────────────────────────
            var net = NetworkManager.Instance;
            if (net != null)
            {
                net.ConnectionFailed += () =>
                    SetStatus("Falha na conexão!", new Color(1f, 0.3f, 0.3f, 1f));
                net.ServerStarted += () =>
                {
                    SetStatus("Servidor criado! Aguardando...", new Color(0.3f, 1f, 0.3f, 1f));
                    ChangeToWorld();
                };
                net.ClientConnected += () =>
                {
                    SetStatus("Conectado ao servidor!", new Color(0.3f, 1f, 0.3f, 1f));
                    ChangeToWorld();
                };
            }

            // Botão do splash fecha a splash e mostra o menu
            _splashButton.Pressed += DismissSplash;

            // Animação pulsante no botão
            _splashButton.Scale = Vector2.One;
            var pulseTween = CreateTween().SetLoops();
            pulseTween.TweenProperty(_splashButton, "scale", new Vector2(1.05f, 1.05f), 0.7f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Sine);
            pulseTween.Parallel().TweenProperty(_splashButton, "modulate", new Color(1, 1, 1, 0.3f), 0.7f)
                .SetEase(Tween.EaseType.In);
            pulseTween.TweenProperty(_splashButton, "scale", Vector2.One, 0.7f)
                .SetEase(Tween.EaseType.Out)
                .SetTrans(Tween.TransitionType.Bounce);
            pulseTween.Parallel().TweenProperty(_splashButton, "modulate", new Color(1, 1, 1, 1f), 0.7f)
                .SetEase(Tween.EaseType.Out);
        }

        public override void _Input(InputEvent @event)
        {
            if (!_splashActive) return;
            if (@event is InputEventKey { Pressed: true } || @event is InputEventMouseButton { Pressed: true })
            {
                DismissSplash();
            }
        }

        private void DismissSplash()
        {
            if (!_splashActive) return;
            _splashActive = false;
            _splashContainer.Visible = false;
            _mainButtons.Visible = true;
        }

        private Button CreateMenuButton(string text, Action action)
        {
            var btn = new Button
            {
                Text = text,
                SizeFlagsHorizontal = SizeFlags.Fill,
                CustomMinimumSize = new Vector2(240, 48)
            };
            btn.AddThemeFontSizeOverride("font_size", 20);
            btn.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f, 1f));
            btn.AddThemeStyleboxOverride("normal", CreateButtonStyle(new Color(0.3f, 0.3f, 0.35f, 1f)));
            btn.AddThemeStyleboxOverride("hover", CreateButtonStyle(new Color(0.4f, 0.4f, 0.5f, 1f)));
            btn.AddThemeStyleboxOverride("pressed", CreateButtonStyle(new Color(0.5f, 0.2f, 0.2f, 1f)));
            btn.Pressed += action;
            return btn;
        }

        private static StyleBoxFlat CreateButtonStyle(Color bgColor)
        {
            return new StyleBoxFlat
            {
                BgColor = bgColor,
                BorderWidthLeft = 2,
                BorderWidthTop = 2,
                BorderWidthRight = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.6f, 0.6f, 0.6f, 1f),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
        }

        private void ShowHostJoinMenu()
        {
            _mainButtons.Visible = false;
            _hostJoinButtons.Visible = true;
        }

        private void ShowMainMenu()
        {
            _hostJoinButtons.Visible = false;
            _mainButtons.Visible = true;
            _statusLabel.Text = "";
        }

        private void StartHost()
        {
            var net = NetworkManager.Instance;
            if (net == null) { SetStatus("Erro: NetworkManager não encontrado!", new Color(1f, 0.3f, 0.3f, 1f)); return; }
            SetStatus("Criando servidor...", new Color(1f, 1f, 0.5f, 1f));
            net.HostGame();
        }

        private void StartJoin()
        {
            var net = NetworkManager.Instance;
            if (net == null) { SetStatus("Erro: NetworkManager não encontrado!", new Color(1f, 0.3f, 0.3f, 1f)); return; }
            string ip = string.IsNullOrWhiteSpace(_ipInput.Text) ? "127.0.0.1" : _ipInput.Text.Trim();
            SetStatus($"Conectando a {ip}...", new Color(1f, 1f, 0.5f, 1f));
            net.JoinGame(ip);
        }

        private void SetStatus(string text, Color color)
        {
            _statusLabel.Text = text;
            _statusLabel.AddThemeColorOverride("font_color", color);
        }

        private void ChangeToWorld()
        {
            GetTree().ChangeSceneToFile("res://scenes/world/world.tscn");
        }
    }
}
