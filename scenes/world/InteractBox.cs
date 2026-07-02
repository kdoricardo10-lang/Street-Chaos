using Godot;

namespace StreetChaos
{
    public partial class InteractBox : InteractableObject
    {
        private MeshInstance3D _mesh;
        private bool _isOpened;

        [Export] public ItemData RewardItem { get; set; }

        public override void _Ready()
        {
            InteractionPrompt = "Abrir caixa";
            InteractionDescription = "Uma caixa interativa no cenário.";

            _mesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
            ApplyVisualState();
            base._Ready();
        }

        public override void Interact(PlayerController player)
        {
            if (_isOpened)
                return;

            if (RewardItem != null && !player.AcquireItem(RewardItem))
                return;

            _isOpened = true;
            ApplyVisualState();

            SetCollisionLayerValue(1, false);
            SetCollisionMaskValue(1, false);
            var promptArea = GetNodeOrNull<InteractionPromptArea>("PromptArea");
            promptArea?.HidePrompt();
            promptArea?.SetProcess(false);
            Visible = false;

            QueueFree();
        }

        private void ApplyVisualState()
        {
            if (_mesh == null)
                return;

            if (_mesh.MaterialOverride is not StandardMaterial3D material)
            {
                material = new StandardMaterial3D();
                _mesh.MaterialOverride = material;
            }

            material.AlbedoColor = _isOpened
                ? new Color(0.3f, 0.9f, 0.4f, 1f)
                : new Color(0.75f, 0.55f, 0.25f, 1f);
        }
    }
}
