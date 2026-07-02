using Godot;

namespace StreetChaos
{
    public partial class Lobby : Control
    {
        private bool _splashDone;

        public override void _Ready()
        {
            var bg = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://p1.png"),
                ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional,
                AnchorsPreset = (int)LayoutPreset.FullRect,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
            };
            AddChild(bg);
        }

        public override void _Input(InputEvent @event)
        {
            if (_splashDone) return;
            if (@event is InputEventKey { Pressed: true })
            {
                _splashDone = true;
                GetTree().ChangeSceneToFile("res://scenes/world/world.tscn");
            }
        }
    }
}
