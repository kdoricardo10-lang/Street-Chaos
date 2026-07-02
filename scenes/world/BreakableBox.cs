using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    public partial class BreakableBox : StaticBody3D, InteractionPromptTarget
    {
        [ExportGroup("Lixeira")]
        [Export] public float DetectionRadius { get; set; } = 2.5f;
        [Export] public float HoldDuration { get; set; } = 10f;
        [Export] public int ItemCount { get; set; } = 3;
        [Export] public PackedScene PickupItemScene { get; set; }

        private bool _wasLooted;
        private float _holdProgress;

        private readonly List<MeshInstance3D> _meshes = new();
        private readonly List<StandardMaterial3D> _materials = new();
        private Node3D _modelRoot;
        private Area3D _detectionArea;
        private InteractionPromptArea _promptArea;
        private readonly List<PlayerController> _nearbyPlayers = new();
        private PlayerController _lootingPlayer;

        private Vector3 _basePos;
        private float _shakeTimer;
        private float _refreshTimer;

        public string InteractionPrompt => _wasLooted ? "Lixeira (vazia)" : "Lixeira";

        public string InteractionDescription => _wasLooted
            ? "Já foi saqueada"
            : _lootingPlayer != null
                ? $"Saqueando... {Mathf.RoundToInt(_holdProgress / HoldDuration * 100)}%"
                : $"Segure [F] por {HoldDuration}s para saquear";

        private static readonly string[] CommonItemPaths =
        {
            "res://resources/items/bandagem.tres",
            "res://resources/items/cano_ferro.tres",
            "res://resources/items/taco_baseball_comum.tres",
        };

        public override void _Ready()
        {
            _basePos = Position;

            _modelRoot = GetNodeOrNull<Node3D>("TrashCanModel");
            if (_modelRoot != null)
            {
                FindMeshInstances(_modelRoot);
                ApplyDefaultMaterials();
            }

            SetupDetectionArea();
            SetupPromptArea();

            AddToGroup("breakables");
        }

        private void FindMeshInstances(Node node)
        {
            if (node is MeshInstance3D mi)
                _meshes.Add(mi);
            foreach (var child in node.GetChildren())
                FindMeshInstances(child);
        }

        private void ApplyDefaultMaterials()
        {
            _materials.Clear();
            foreach (var mi in _meshes)
            {
                var mat = new StandardMaterial3D();
                mat.AlbedoColor = new Color(0.35f, 0.35f, 0.35f, 1);
                mat.Metallic = 0.7f;
                mat.Roughness = 0.4f;
                mi.MaterialOverride = mat;
                _materials.Add(mat);
            }
        }

        private void SetupDetectionArea()
        {
            _detectionArea = new Area3D();
            _detectionArea.Name = "DetectionArea";
            var shape = new CollisionShape3D();
            shape.Shape = new SphereShape3D { Radius = DetectionRadius };
            _detectionArea.AddChild(shape);
            _detectionArea.CollisionLayer = 0;
            _detectionArea.CollisionMask = 1;
            _detectionArea.Monitoring = true;
            _detectionArea.Monitorable = false;
            _detectionArea.BodyEntered += OnBodyEntered;
            _detectionArea.BodyExited += OnBodyExited;
            AddChild(_detectionArea);
        }

        private void SetupPromptArea()
        {
            _promptArea = new InteractionPromptArea
            {
                Name = "PromptArea",
                Radius = DetectionRadius
            };
            AddChild(_promptArea);
        }

        private void OnBodyEntered(Node3D body)
        {
            if (body is PlayerController player && !_nearbyPlayers.Contains(player))
                _nearbyPlayers.Add(player);
        }

        private void OnBodyExited(Node3D body)
        {
            if (body is PlayerController player)
            {
                _nearbyPlayers.Remove(player);
                if (_lootingPlayer == player)
                    CancelLoot();
            }
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            if (_wasLooted) return;

            if (_lootingPlayer != null)
            {
                bool stillNearby = _nearbyPlayers.Contains(_lootingPlayer);
                bool holdingF = Input.IsActionPressed("interact");
                bool alive = _lootingPlayer.IsInsideTree();

                if (!stillNearby || !holdingF || !alive)
                {
                    CancelLoot();
                    return;
                }

                _holdProgress += dt;
                UpdateLootProgressVisual();
                RefreshPromptPeriodically(dt);

                if (_holdProgress >= HoldDuration)
                    CompleteLoot();

                return;
            }

            TryStartLoot();
        }

        private void RefreshPromptPeriodically(float dt)
        {
            _refreshTimer -= dt;
            if (_refreshTimer <= 0)
            {
                _refreshTimer = 0.2f;
                _promptArea?.RefreshPrompt();
            }
        }

        private void TryStartLoot()
        {
            foreach (var player in _nearbyPlayers)
            {
                if (player == null || !player.IsInsideTree()) continue;

                if (!Input.IsActionJustPressed("interact")) continue;

                _holdProgress = 0f;
                _lootingPlayer = player;
                _shakeTimer = 0f;
                break;
            }
        }

        private void CancelLoot()
        {
            _holdProgress = 0f;
            _lootingPlayer = null;
            _shakeTimer = 0f;
            ResetPosition();
            ApplyDefaultMaterials();
            _promptArea?.RefreshPrompt();
        }

        private void CompleteLoot()
        {
            SpawnItems(ItemCount);

            _wasLooted = true;
            _lootingPlayer = null;
            _holdProgress = 0f;

            foreach (var mi in _meshes)
            {
                var lootMat = new StandardMaterial3D();
                lootMat.AlbedoColor = new Color(0.15f, 0.15f, 0.15f, 1);
                lootMat.Metallic = 0.3f;
                lootMat.Roughness = 0.8f;
                mi.MaterialOverride = lootMat;
            }


            if (_detectionArea != null)
                _detectionArea.Monitoring = false;

            _promptArea?.RefreshPrompt();
        }

        private void UpdateLootProgressVisual()
        {
            float t = HoldDuration > 0 ? _holdProgress / HoldDuration : 0;

            Color baseColor = new Color(0.35f, 0.35f, 0.35f);
            Color progressColor = new Color(0.6f, 0.8f, 0.2f);
            Color color = baseColor.Lerp(progressColor, t);
            foreach (var mat in _materials)
                mat.AlbedoColor = color;

            _shakeTimer += 0.1f;
            float shakeIntensity = 0.01f + t * 0.03f;
            float sx = Mathf.Sin(_shakeTimer * 30f) * shakeIntensity;
            float sz = Mathf.Cos(_shakeTimer * 25f) * shakeIntensity;
            Position = _basePos + new Vector3(sx, 0, sz);
        }

        private void ResetPosition()
        {
            Position = _basePos;
            Scale = Vector3.One;
        }

        private void SpawnItems(int count)
        {
            var pickupScene = PickupItemScene ?? GD.Load<PackedScene>("res://pickups/items/PickupItem.tscn");
            if (pickupScene == null) return;

            var rand = new System.Random();

            for (int i = 0; i < count; i++)
            {
                string itemPath = CommonItemPaths[GD.Randi() % CommonItemPaths.Length];
                var itemResource = GD.Load<ItemData>(itemPath);
                if (itemResource == null) continue;

                float angle = (float)(i * 2.094 + rand.NextDouble() * 0.8);
                float radius = (float)(1.2 + rand.NextDouble() * 0.8);
                Vector3 finalPos = GlobalPosition + new Vector3(
                    Mathf.Cos(angle) * radius, 0.5f, Mathf.Sin(angle) * radius);

                var pickup = pickupScene.Instantiate<PickupItem>();
                pickup.GlobalPosition = finalPos;
                pickup.Scale = Vector3.Zero;
                pickup.Monitoring = false;
                pickup.Monitorable = false;
                var currentScene = GetTree().CurrentScene;
                if (currentScene != null)
                    currentScene.AddChild(pickup);
                else
                    AddChild(pickup);
                pickup.RemoveFromGroup("interactables");
                pickup.AssignItemData(itemResource);

                int index = i;
                var appearTimer = GetTree().CreateTimer(index * 0.2f);
                appearTimer.Timeout += () =>
                {
                    if (!IsInstanceValid(pickup)) return;
                    var pop = CreateTween();
                    pop.TweenProperty(pickup, "scale", Vector3.One, 0.2f)
                       .SetEase(Tween.EaseType.Out)
                       .SetTrans(Tween.TransitionType.Back);
                };

                var unlockTimer = GetTree().CreateTimer(2.0f);
                unlockTimer.Timeout += () =>
                {
                    if (!IsInstanceValid(pickup)) return;
                    pickup.Monitoring = true;
                    pickup.Monitorable = true;
                    pickup.AddToGroup("interactables");
                };
            }
        }
    }
}
