using Godot;

namespace StreetChaos
{
    [GlobalClass]
    public partial class Climbable : Node
    {
        [Export] public float Height { get; set; } = 3.0f;

        public override void _Ready()
        {
            AddToGroup("climbable");
            SetMeta("climb_height", Height);
        }
    }
}
