using Godot;

namespace StreetChaos
{
    public partial class InteractableObject : StaticBody3D, InteractionPromptTarget
    {
        [Export] public string InteractionPrompt { get; set; } = "Interagir";
        [Export] public string InteractionDescription { get; set; } = "";

        public override void _Ready()
        {
            AddToGroup("interactables");
        }

        public virtual void Interact(PlayerController player)
        {
        }
    }
}
