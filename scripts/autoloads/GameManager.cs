using Godot;
using System;

namespace StreetChaos
{
    public partial class GameManager : Node
    {
        public static GameManager Instance { get; private set; }

        [Export] public FightingStyleData DefaultFightingStyle { get; set; }
        [Export] public PackedScene PlayerScene { get; set; }
        [Export] public PackedScene HUDScene { get; set; }

        private CanvasLayer _hudLayer;
        private Node _currentHUD;
        public Node CurrentPlayer { get; private set; }
        public HUD CurrentHUD => _currentHUD as HUD;
        public Vector3 RespawnPosition { get; set; }
        public void SetCurrentPlayer(Node player) { CurrentPlayer = player; }

        public override void _Ready()
        {
            Instance = this;
            SetupInputs();
        }

        public Node SpawnPlayer(Vector3 position)
        {
            if (PlayerScene == null) return null;

            CurrentPlayer = PlayerScene.Instantiate();
            var node3d = CurrentPlayer as Node3D;
            if (node3d != null)
                node3d.GlobalPosition = position;

            var world = GetTree().CurrentScene;
            if (world != null)
                world.AddChild(CurrentPlayer);

            var matchManager = MatchManager.Instance;
            if (CurrentPlayer != null)
            {
                int peerId = CurrentPlayer.GetMultiplayerAuthority();
                matchManager?.RegisterPlayer(peerId, CurrentPlayer);
            }

            SetupHUD();

            return CurrentPlayer;
        }

        public void SetupHUD()
        {
            if (_hudLayer != null)
            {
                _hudLayer.QueueFree();
                _hudLayer = null;
                _currentHUD = null;
            }

            if (HUDScene != null && CurrentPlayer != null)
            {
                _hudLayer = new CanvasLayer();
                _hudLayer.Layer = 1;
                _currentHUD = HUDScene.Instantiate();
                _hudLayer.AddChild(_currentHUD);
                GetTree().CurrentScene.AddChild(_hudLayer);

                var hud = _currentHUD as HUD;
                if (hud != null)
                    hud.Setup(CurrentPlayer);
            }
        }

        public void OnPlayerDied(Node player)
        {
            var matchManager = MatchManager.Instance;
            if (player != null)
                matchManager?.UnregisterPlayer(player.GetMultiplayerAuthority());
        }

        public void StartGame()
        {
            var matchManager = MatchManager.Instance;
            matchManager?.BeginMatch();
        }

        private void SetupInputs()
        {
            AddInputAction("move_forward", new InputEventKey { Keycode = Key.W });
            AddInputAction("move_back", new InputEventKey { Keycode = Key.S });
            AddInputAction("move_left", new InputEventKey { Keycode = Key.A });
            AddInputAction("move_right", new InputEventKey { Keycode = Key.D });
            AddInputAction("sprint", new InputEventKey { Keycode = Key.Capslock });
            AddInputAction("jump", new InputEventKey { Keycode = Key.Space });

            AddInputAction("attack_light", new InputEventMouseButton { ButtonIndex = MouseButton.Left });
            AddInputAction("attack_heavy", new InputEventMouseButton { ButtonIndex = MouseButton.Middle });
            AddInputAction("block", new InputEventMouseButton { ButtonIndex = MouseButton.Right });
            AddInputAction("dodge", new InputEventKey { Keycode = Key.R });
            AddInputAction("interact", new InputEventKey { Keycode = Key.F });
            AddInputAction("slot_1", new InputEventKey { Keycode = Key.Key1 });
            AddInputAction("slot_2", new InputEventKey { Keycode = Key.Key2 });
            AddInputAction("slot_3", new InputEventKey { Keycode = Key.Key3 });
            AddInputAction("slot_4", new InputEventKey { Keycode = Key.Key4 });
            AddInputAction("slot_5", new InputEventKey { Keycode = Key.Key5 });
            AddInputAction("slot_6", new InputEventKey { Keycode = Key.Key6 });
            AddInputAction("grab", new InputEventKey { Keycode = Key.E });
            AddInputAction("drop_item", new InputEventKey { Keycode = Key.G });
            AddInputAction("climb", new InputEventKey { Keycode = Key.Q });
            AddInputAction("kick_heavy", new InputEventKey { Keycode = Key.Z });
            AddInputAction("uppercut", new InputEventKey { Keycode = Key.X });
        }

        private static void AddInputAction(string name, InputEvent @event)
        {
            if (!InputMap.HasAction(name))
                InputMap.AddAction(name);
            InputMap.ActionAddEvent(name, @event);
        }
    }
}
