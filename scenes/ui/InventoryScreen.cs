using Godot;

namespace StreetChaos
{
    public partial class InventoryScreen : Control
    {
        private static readonly string CharacterModelPath =
            "res://.godot/imported/AnimationLibrary_Godot_Standard.glb-701c3948743f38c5d83d9dbe5e82fe9e.scn";

        private PlayerController _player;
        private ColorRect _backdrop;
        private InventorySlot[] _weaponSlots = new InventorySlot[2];
        private InventorySlot[] _invSlots = new InventorySlot[4];
        private Node3D _modelRoot;
        private float _modelRotation;
        private bool _isDragging;
        private float _lastMouseX;
        private SubViewportContainer _viewportContainer;
        private SubViewport _viewport;
        private Vector2 _screenSize;
        private float _scale = 1f;

        private static readonly float RefW = 1920f;
        private static readonly float RefH = 1080f;

        private static readonly float[] InvSlotLeft = { 110f, 20f, 110f, 200f };
        private static readonly float[] InvSlotRight = { 180f, 90f, 180f, 270f };
        private static readonly float[] InvSlotTop = { -100f, -215f, -330f, -215f };
        private static readonly float[] InvSlotBottom = { -30f, -145f, -260f, -145f };
        private static readonly string[] InvSlotKeys = { "3", "4", "5", "6" };

        private static readonly float[] WeaponSlotLeft = { -266f, 156f };
        private static readonly float[] WeaponSlotRight = { -156f, 266f };

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            _player = GetTree().GetFirstNodeInGroup("players") as PlayerController;

            CreateBackdrop();
            CreatePlayerViewport();
            CreateInventorySlots();
            CreateWeaponSlots();
            SyncFromPlayer();

            Resized += OnResized;
            RecalculateLayout();
            CallDeferred(nameof(RecalculateLayout));
            ProcessMode = ProcessModeEnum.Always;
        }

        private void CreateBackdrop()
        {
            _backdrop = new ColorRect();
            _backdrop.Color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
            _backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _backdrop.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_backdrop);
        }

        private void CreatePlayerViewport()
        {
            _viewportContainer = new SubViewportContainer();
            _viewportContainer.AnchorLeft = 0.5f;
            _viewportContainer.AnchorTop = 0.5f;
            _viewportContainer.AnchorRight = 0.5f;
            _viewportContainer.AnchorBottom = 0.5f;
            _viewportContainer.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_viewportContainer);

            _viewport = new SubViewport();
            _viewport.TransparentBg = true;
            _viewport.World3D = new World3D();
            _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            _viewportContainer.AddChild(_viewport);

            var dragArea = new Control();
            dragArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            dragArea.MouseFilter = MouseFilterEnum.Pass;
            dragArea.GuiInput += OnViewportGuiInput;
            _viewportContainer.AddChild(dragArea);

            var camera = new Camera3D();
            camera.Position = new Vector3(0, 1.2f, 2.8f);
            camera.Fov = 60f;
            camera.Near = 0.1f;
            camera.Far = 10f;
            camera.Current = true;
            _viewport.AddChild(camera);
            camera.LookAt(new Vector3(0, 0.9f, 0));

            var ambient = new WorldEnvironment();
            ambient.Environment = new Godot.Environment();
            ambient.Environment.AmbientLightColor = new Color(0.3f, 0.3f, 0.35f);
            ambient.Environment.AmbientLightSource = Godot.Environment.AmbientSource.Color;
            _viewport.AddChild(ambient);

            var keyLight = new DirectionalLight3D();
            keyLight.Position = new Vector3(-3, 4, 2);
            keyLight.LightColor = new Color(1, 0.95f, 0.85f);
            keyLight.LightEnergy = 1.5f;
            _viewport.AddChild(keyLight);
            keyLight.LookAt(Vector3.Zero);

            var fillLight = new DirectionalLight3D();
            fillLight.Position = new Vector3(2, 1, -2);
            fillLight.LightColor = new Color(0.5f, 0.6f, 0.8f);
            fillLight.LightEnergy = 0.6f;
            _viewport.AddChild(fillLight);
            fillLight.LookAt(Vector3.Zero);

            var rimLight = new DirectionalLight3D();
            rimLight.Position = new Vector3(0, -1, -3);
            rimLight.LightColor = new Color(0.3f, 0.3f, 0.5f);
            rimLight.LightEnergy = 0.4f;
            _viewport.AddChild(rimLight);
            rimLight.LookAt(Vector3.Zero);

            var charScene = GD.Load<PackedScene>(CharacterModelPath);
            if (charScene != null)
            {
                _modelRoot = charScene.Instantiate<Node3D>();
                _modelRoot.Position = new Vector3(0, 0, 0);
                _viewport.AddChild(_modelRoot);

                var animPlayer = FindAnimationPlayer(_modelRoot);
                if (animPlayer != null)
                {
                    animPlayer.RootNode = animPlayer.GetPathTo(_modelRoot);
                    if (animPlayer.HasAnimation("Idle"))
                    {
                        var anim = animPlayer.GetAnimation("Idle");
                        anim.LoopMode = Animation.LoopModeEnum.Linear;
                        animPlayer.Play("Idle");
                    }
                }
            }
        }

        private static AnimationPlayer FindAnimationPlayer(Node node)
        {
            if (node is AnimationPlayer player)
                return player;
            foreach (var child in node.GetChildren())
            {
                var result = FindAnimationPlayer(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void OnViewportGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseBtn)
            {
                if (mouseBtn.ButtonIndex == MouseButton.Left)
                {
                    _isDragging = mouseBtn.Pressed;
                    _lastMouseX = mouseBtn.Position.X;
                }
            }

            if (@event is InputEventMouseMotion mouseMotion && _isDragging)
            {
                float dx = mouseMotion.Relative.X;
                _modelRotation -= dx * 0.005f;
                if (_modelRoot != null)
                    _modelRoot.Rotation = new Vector3(0, _modelRotation, 0);
            }
        }

        private void CreateInventorySlots()
        {
            for (int i = 0; i < 4; i++)
            {
                var slot = new InventorySlot();
                slot.Setup("inventory", i, InvSlotKeys[i]);
                slot.AnchorLeft = 0.0f;
                slot.AnchorTop = 1.0f;
                slot.AnchorRight = 0.0f;
                slot.AnchorBottom = 1.0f;
                slot.OffsetLeft = InvSlotLeft[i];
                slot.OffsetTop = InvSlotTop[i];
                slot.OffsetRight = InvSlotRight[i];
                slot.OffsetBottom = InvSlotBottom[i];
                AddChild(slot);
                _invSlots[i] = slot;
            }
        }

        private void CreateWeaponSlots()
        {
            for (int i = 0; i < 2; i++)
            {
                var slot = new InventorySlot();
                slot.Setup("weapon", i, $"{i + 1}");
                slot.AnchorLeft = 0.5f;
                slot.AnchorTop = 1.0f;
                slot.AnchorRight = 0.5f;
                slot.AnchorBottom = 1.0f;
                slot.OffsetLeft = WeaponSlotLeft[i];
                slot.OffsetTop = -96f;
                slot.OffsetRight = WeaponSlotRight[i];
                slot.OffsetBottom = -16f;
                AddChild(slot);
                _weaponSlots[i] = slot;
            }
        }

        private void CreateHint()
        {
        }

        private void OnResized()
        {
            RecalculateLayout();
        }

        private void RecalculateLayout()
        {
            _screenSize = GetViewportRect().Size;
            if (_screenSize.X <= 0 || _screenSize.Y <= 0) return;

            float sx = _screenSize.X / RefW;
            float sy = _screenSize.Y / RefH;
            _scale = Mathf.Min(sx, sy);

            if (_viewportContainer != null)
            {
                _viewportContainer.OffsetLeft = -300f * sx;
                _viewportContainer.OffsetTop = -420f * sy;
                _viewportContainer.OffsetRight = 300f * sx;
                _viewportContainer.OffsetBottom = 500f * sy;
            }

            if (_viewport != null)
            {
                _viewport.Size = new Vector2I(
                    (int)(600f * sx),
                    (int)(920f * sy));
            }

            float[][] invSlots = {
                new[] { 110f, 180f, -100f, -30f },
                new[] { 20f, 90f, -215f, -145f },
                new[] { 110f, 180f, -330f, -260f },
                new[] { 200f, 270f, -215f, -145f },
            };
            for (int i = 0; i < 4 && i < _invSlots.Length; i++)
            {
                if (_invSlots[i] == null) continue;
                _invSlots[i].OffsetLeft = invSlots[i][0] * sx;
                _invSlots[i].OffsetTop = invSlots[i][2] * sy;
                _invSlots[i].OffsetRight = invSlots[i][1] * sx;
                _invSlots[i].OffsetBottom = invSlots[i][3] * sy;
            }

            for (int i = 0; i < 2 && i < _weaponSlots.Length; i++)
            {
                if (_weaponSlots[i] == null) continue;
                _weaponSlots[i].OffsetLeft = WeaponSlotLeft[i] * sx;
                _weaponSlots[i].OffsetRight = WeaponSlotRight[i] * sx;
                _weaponSlots[i].OffsetTop = -96f * sy;
                _weaponSlots[i].OffsetBottom = -16f * sy;
            }

            foreach (var slot in _invSlots)
                if (slot != null)
                    slot.SetScale(_scale);
            foreach (var slot in _weaponSlots)
                if (slot != null)
                    slot.SetScale(_scale);
        }

        public override void _Process(double delta)
        {
            if (Input.IsActionJustPressed("ui_cancel"))
                Close();
        }

        public void SyncFromPlayer()
        {
            if (_player == null) return;

            for (int i = 0; i < 2; i++)
            {
                _weaponSlots[i]?.UpdateItem(_player.GetWeaponSlot(i));
                if (_weaponSlots[i] != null)
                    _weaponSlots[i].IsSelected = _player.GetSelectedWeaponSlot() == i;
            }

            for (int i = 0; i < 4; i++)
            {
                _invSlots[i]?.UpdateItem(_player.GetInventorySlot(i));
                if (_invSlots[i] != null)
                    _invSlots[i].IsSelected = _player.GetSelectedInventorySlot() == i;
            }
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            var dict = data.AsGodotDictionary();
            return dict.ContainsKey("item") && dict["item"].Obj != null;
        }

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            var dict = data.AsGodotDictionary();
            string slotType = dict["slot_type"].AsString();
            int slotIndex = dict["slot_index"].AsInt32();
            _player?.DropItemAtSlot(slotType, slotIndex);
        }

        private void Close()
        {
            if (_player != null)
            {
                _player.CloseInventory();
                QueueFree();
            }
        }
    }
}
