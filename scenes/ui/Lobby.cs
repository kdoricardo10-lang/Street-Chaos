using Godot;
using System;

namespace StreetChaos
{
    public partial class Lobby : Control
    {
        private bool _splashDone;
        private Control _lobbyContent;
        private Control _hostJoinButtons;
        private LineEdit _ipInput;
        private Label _statusLabel;

        public override void _Ready()
        {
            _lobbyContent = GetNode<Control>("LobbyContent");

            _hostJoinButtons = new VBoxContainer
            {
                AnchorLeft = 0.5f, AnchorTop = 0.4f,
                AnchorRight = 0.5f, AnchorBottom = 0.4f,
                OffsetLeft = -160f, OffsetTop = 0f,
                OffsetRight = 160f, OffsetBottom = 200f,
                Visible = true
            };
            _hostJoinButtons.AddThemeConstantOverride("separation", 12);
            _lobbyContent.AddChild(_hostJoinButtons);

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

            _hostJoinButtons.AddChild(CreateMenuButton("CRIAR PARTIDA", StartHost));
            _hostJoinButtons.AddChild(CreateMenuButton("ENTRAR EM PARTIDA", StartJoin));

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
            _lobbyContent.AddChild(_statusLabel);

            try
            {
                var net = NetworkManager.Instance;
                if (net != null)
                {
                    net.ConnectionFailed += () =>
                        SetStatus("Falha na conexão!", new Color(1f, 0.3f, 0.3f, 1f));
                    net.ServerStarted += () =>
                    {
                        SetStatus("Servidor criado! Aguardando...", new Color(0.3f, 1f, 0.3f, 1f));
                        GetTree().ChangeSceneToFile("res://scenes/world/world.tscn");
                    };
                    net.ClientConnected += () =>
                    {
                        SetStatus("Conectado ao servidor!", new Color(0.3f, 1f, 0.3f, 1f));
                        GetTree().ChangeSceneToFile("res://scenes/world/world.tscn");
                    };
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Lobby network init error: {ex}");
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (!_splashDone && @event is InputEventKey { Pressed: true })
            {
                _splashDone = true;
                GetNode("Splash").QueueFree();
                _lobbyContent.Visible = true;
            }
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
    }
}
