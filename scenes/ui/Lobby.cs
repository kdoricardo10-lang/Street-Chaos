using Godot;

namespace StreetChaos
{
    public partial class Lobby : Control
    {
        public override void _Ready()
        {
            var bg = new TextureRect
            {
                Texture = GD.Load<Texture2D>("res://tela inicial.png"),
                ExpandMode = TextureRect.ExpandModeEnum.FitHeightProportional,
                AnchorsPreset = (int)LayoutPreset.FullRect,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
            };
            AddChild(bg);
        }
    }
}
