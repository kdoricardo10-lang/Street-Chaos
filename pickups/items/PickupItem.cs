using Godot;

namespace StreetChaos
{
	public partial class PickupItem : Area3D, InteractionPromptTarget
	{
		[Export] public string ItemResourcePath { get; set; } = "";
		public ItemData ItemData { get; private set; }
		public int NetworkId { get; set; }
		[Export] public float RotationSpeed { get; set; } = 90f;
		[Export] public float BobHeight { get; set; } = 0.2f;
		[Export] public float BobSpeed { get; set; } = 2f;
		[Export] public float RespawnTime { get; set; } = 30f;

		public bool IsConsumable => ItemData?.IsConsumable == true;
		public string InteractionPrompt => ItemData?.ItemName ?? string.Empty;

		public string InteractionDescription => ItemData?.Description ?? string.Empty;

		private Vector3 _basePosition;
		private float _bobTimer;
		private bool _isCollected;
		private InteractionPromptArea _promptArea;

		private static ItemData CreateFallbackItem(string nodeName)
		{
			if (nodeName.Contains("Medkit"))
				return new HealingData { ItemName = "Kit de Cura", HealAmount = 40, IsConsumable = true };
			if (nodeName.Contains("Pipe"))
				return new WeaponData { ItemName = "Cano de Ferro", DamageBonus = 12 };
			if (nodeName.Contains("Bat"))
				return new WeaponData { ItemName = "Taco de Baseball", DamageBonus = 10 };
			if (nodeName.Contains("Damage"))
				return new BuffData { ItemName = "Dano", BuffType = BuffType.Damage, BuffValue = 0.2f };
			if (nodeName.Contains("Speed"))
				return new BuffData { ItemName = "Velocidade", BuffType = BuffType.Speed, BuffValue = 0.3f };
			if (nodeName.Contains("Defense"))
				return new BuffData { ItemName = "Defesa", BuffType = BuffType.Defense, BuffValue = 0.3f };
			return new ItemData { ItemName = nodeName };
		}

		public override void _Ready()
		{
			AddToGroup("interactables");
			_basePosition = Position;
			BodyEntered += OnBodyEntered;
			if (!string.IsNullOrEmpty(ItemResourcePath) && ItemData == null)
			{
				var loaded = GD.Load(ItemResourcePath);
				if (loaded != null)
					ItemData = loaded as ItemData;
			}

			if (ItemData == null)
				ItemData = CreateFallbackItem(Name);

			if (GetNodeOrNull<InteractionPromptArea>("PromptArea") == null)
			{
				_promptArea = new InteractionPromptArea
				{
					Name = "PromptArea",
					Radius = 1.5f
				};
				AddChild(_promptArea);
			}
			else
			{
				_promptArea = GetNodeOrNull<InteractionPromptArea>("PromptArea");
			}

			if (ItemData != null && ItemData.WorldModel != null)
			{
				var model = ItemData.WorldModel.Instantiate<Node3D>();
				AddChild(model);
				var defaultMesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
				if (defaultMesh != null)
					defaultMesh.Visible = false;
			}
		}

		public override void _Process(double delta)
		{
			if (_isCollected) return;

			_bobTimer += (float)delta * BobSpeed;
			float bob = Mathf.Sin(_bobTimer) * BobHeight;
			Position = new Vector3(_basePosition.X, _basePosition.Y + bob, _basePosition.Z);
			RotateY(Mathf.DegToRad(RotationSpeed * (float)delta));
		}

		public void AssignItemData(ItemData data)
		{
			ItemData = data;
			RotationSpeed = 90f;
			BobHeight = 0.2f;
			BobSpeed = 2f;
			_basePosition = Position;

			var defaultMesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
			if (defaultMesh != null)
				defaultMesh.Visible = ItemData?.WorldModel == null;

			if (ItemData?.WorldModel != null)
			{
				var model = ItemData.WorldModel.Instantiate<Node3D>();
				AddChild(model);
			}
		}

		public void Collect(PlayerController player)
		{
			if (_isCollected) return;
			OnCollected(player);
		}

		private void OnBodyEntered(Node3D body)
		{
			if (_isCollected) return;
		}

		protected virtual void OnCollected(PlayerController player)
		{
			_isCollected = true;
			Visible = false;
			Monitoring = false;
			if (_promptArea != null)
			{
				_promptArea.HidePrompt();
				_promptArea.Monitoring = false;
			}

			bool isNetworked = NetworkManager.Instance?.IsNetworkConnected == true && !NetworkManager.Instance.IsServer;

			if (isNetworked)
			{
				// Client: send pickup request to server
				player.Rpc(nameof(PlayerController.RequestPickupServer), NetworkId);
				QueueFree();
				return;
			}

			if (player.AcquireItem(ItemData))
			{
				QueueFree();
				return;
			}

			_isCollected = false;
			Visible = true;
			Monitoring = true;
			if (_promptArea != null)
			{
				_promptArea.Monitoring = true;
				_promptArea.RefreshPrompt();
			}
		}
	}
}
