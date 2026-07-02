using Godot;
using System;

namespace StreetChaos
{
    public partial class Lobby : Control
    {
        private Control _lobbyContent;
        private Button _hostButton;
        private Button _joinButton;
        private LineEdit _ipInput;
        private Label _statusLabel;

        public override void _Ready()
        {
            // ── Background ────────────────────────────────────────────
            var bg = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://tela inicial.png"),
                ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional,
                AnchorsPreset = (int)LayoutPreset.FullRect,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
            };
            AddChild(bg);

            // ── Lobby content ────────────────────────────────────────
            _lobbyContent = new Control
            {
                AnchorsPreset = (int)LayoutPreset.FullRect
            };
            AddChild(_lobbyContent);

            // Title
            var title = new Label
            {
                Text = "LOBBY",
                HorizontalAlignment = HorizontalAlignment.Center,
                AnchorLeft = 0.5f, AnchorTop = 0.15f,
                AnchorRight = 0.5f, AnchorBottom = 0.15f,
                OffsetLeft = -200f, OffsetTop = -40f,
                OffsetRight = 200f, OffsetBottom = 40f,
            };
            title.AddThemeFontSizeOverride("font_size", 72);
            title.AddThemeColorOverride("font_color", new Color(0.9f, 0.2f, 0.2f, 1.0f));
            title.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
            title.AddThemeConstantOverride("outline_size", 4);
            _lobbyContent.AddChild(title);

            // Container for buttons
            var vbox = new VBoxContainer
            {
                AnchorLeft = 0.5f, AnchorTop = 0.45f,
                AnchorRight = 0.5f, AnchorBottom = 0.45f,
                OffsetLeft = -120f, OffsetTop = 0f,
                OffsetRight = 120f, OffsetBottom = 200f
            };
            vbox.AddThemeConstantOverride("separation", 16);
            _lobbyContent.AddChild(vbox);

            _hostButton = CreateLobbyButton("HOSPEDAR", () =>
            {
                var net = NetworkManager.Instance;
                if (net == null) { SetStatus("Erro: NetworkManager não encontrado!", new Color(1f, 0.3f, 0.3f, 1f)); return; }
                SetStatus("Criando servidor...", new Color(1f, 1f, 0.5f, 1f));
                net.HostGame();
                GetTree().ChangeSceneToFile("res://scenes/world/world.tscn");
            });
            vbox.AddChild(_hostButton);

            _joinButton = CreateLobbyButton("ENTRAR", () =>
            {
                var net = NetworkManager.Instance;
                if (net == null) { SetStatus("Erro: NetworkManager não encontrado!", new Color(1f, 0.3f, 0.3f, 1f)); return; }
                string ip = string.IsNullOrWhiteSpace(_ipInput.Text) ? "127.0.0.1" : _ipInput.Text.Trim();
                SetStatus($"Conectando a {ip}...", new Color(1f, 1f, 0.5f, 1f));
                net.JoinGame(ip);
                GetTree().ChangeSceneToFile("res://scenes/world/world.tscn");
            });
            vbox.AddChild(_joinButton);

            _ipInput = new LineEdit
            {
                PlaceholderText = "IP do servidor (deixe vazio para localhost)",
                SizeFlagsHorizontal = SizeFlags.Fill,
                CustomMinimumSize = new Vector2(240, 36),
                Alignment = HorizontalAlignment.Center
            };
            _ipInput.AddThemeFontSizeOverride("font_size", 16);
            vbox.AddChild(_ipInput);

            _statusLabel = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.Fill,
                CustomMinimumSize = new Vector2(240, 24)
            };
            _statusLabel.AddThemeFontSizeOverride("font_size", 14);
            vbox.AddChild(_statusLabel);

            // Network events
            var net = NetworkManager.Instance;
            if (net != null)
            {
                net.ConnectionFailed += () =>
                    SetStatus("Falha na conexão!", new Color(1f, 0.3f, 0.3f, 1f));
                net.ServerStarted += () =>
                    SetStatus("Servidor criado! Aguardando...", new Color(0.3f, 1f, 0.3f, 1f));
                net.ClientConnected += () =>
                    SetStatus("Conectado ao servidor!", new Color(0.3f, 1f, 0.3f, 1f));
            }
        }

        public override void _Input(InputEvent @event)
        {
        }

        private void SetStatus(string text, Color color)
        {
            _statusLabel.Text = text;
            _statusLabel.AddThemeColorOverride("font_color", color);
        }

        private static Button CreateLobbyButton(string text, Action action)
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
    }
}
