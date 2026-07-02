using Godot;

namespace StreetChaos
{
    public partial class Lobby : Control
    {
        private bool _splashDone;
        private ColorRect _splashBg;
        private TextureRect _splashImg;
        private Button _splashBtn;
        private Control _lobbyContent;
        private Button _btnJogar;
        private Button _btnOpcao1;
        private Button _btnOpcao2;
        private Tween _imgTween;

        public override void _Ready()
        {
            _splashBg = GetNode<ColorRect>("SplashBg");
            _splashImg = GetNode<TextureRect>("SplashImg");
            _splashBtn = GetNode<Button>("SplashBtn");
            _lobbyContent = GetNode<Control>("LobbyContent");
            _btnJogar = GetNode<Button>("LobbyContent/BtnJogar");
            _btnOpcao1 = GetNode<Button>("LobbyContent/BtnOpcao1");
            _btnOpcao2 = GetNode<Button>("LobbyContent/BtnOpcao2");

            _splashBtn.Pressed += GoToLobby;

            _imgTween = CreateTween().SetLoops();
            _imgTween.TweenProperty(_splashImg, "self_modulate",
                new Color(1, 1, 1, 0.3f), 1.0)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);
            _imgTween.TweenProperty(_splashImg, "self_modulate",
                new Color(1, 1, 1, 1f), 1.0)
                .SetTrans(Tween.TransitionType.Sine)
                .SetEase(Tween.EaseType.InOut);

            _btnJogar.Pressed += OnJogar;
            _btnOpcao1.Pressed += OnOpcao1;
            _btnOpcao2.Pressed += OnOpcao2;
        }

        public override void _Input(InputEvent @event)
        {
            if (!_splashDone && @event is InputEventKey { Pressed: true })
                GoToLobby();
        }

        private void GoToLobby()
        {
            if (_splashDone) return;
            _splashDone = true;
            _imgTween?.Kill();
            _splashBg.QueueFree();
            _splashImg.QueueFree();
            _splashBtn.QueueFree();
            _lobbyContent.Visible = true;
        }

        private void OnJogar()
        {
            GetTree().ChangeSceneToFile("res://scenes/world/world.tscn");
        }

        private void OnOpcao1()
        {
            GD.Print("Opcao 1 clicada");
        }

        private void OnOpcao2()
        {
            GD.Print("Opcao 2 clicada");
        }
    }
}
