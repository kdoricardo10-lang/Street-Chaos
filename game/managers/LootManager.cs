using Godot;
using System;
using System.Collections.Generic;

namespace StreetChaos
{
    public partial class LootManager : Node
    {
        public static LootManager Instance { get; private set; }

        [Export] public PackedScene[] WeaponPickupPrefabs { get; set; }
        [Export] public PackedScene[] BuffPickupPrefabs { get; set; }
        [Export] public int TotalSpawnsPerMatch { get; set; } = 100;
        [Export] public float SpawnRadius { get; set; } = 80f;

        private readonly List<Marker3D> _spawnPoints = new();
        private readonly List<Node> _activePickups = new();
        private RandomNumberGenerator _rng = new();

        public override void _Ready()
        {
            Instance = this;
        }

        public void RegisterSpawnPoint(Marker3D point)
        {
            if (!_spawnPoints.Contains(point))
                _spawnPoints.Add(point);
        }

        public void SpawnLoot()
        {
            ClearLoot();

            int count = Mathf.Min(TotalSpawnsPerMatch, _spawnPoints.Count);
            var shuffled = new Godot.Collections.Array<Marker3D>(_spawnPoints);
            shuffled.Shuffle();

            for (int i = 0; i < count && i < shuffled.Count; i++)
            {
                SpawnAtPoint(shuffled[i]);
            }
        }

        private void SpawnAtPoint(Marker3D point)
        {
            float roll = _rng.Randf();
            PackedScene prefab;

            if (roll < 0.4f)
            {
                if (BuffPickupPrefabs == null || BuffPickupPrefabs.Length == 0) return;
                prefab = BuffPickupPrefabs[_rng.RandiRange(0, BuffPickupPrefabs.Length - 1)];
            }
            else if (roll < 0.7f)
            {
                if (WeaponPickupPrefabs == null || WeaponPickupPrefabs.Length == 0) return;
                prefab = WeaponPickupPrefabs[_rng.RandiRange(0, WeaponPickupPrefabs.Length - 1)];
            }
            else
            {
                return;
            }

            if (prefab == null) return;

            // Try network sync first
            PickupItem pickupFromPrefab = null;
            if (prefab.CanInstantiate())
                pickupFromPrefab = prefab.Instantiate<PickupItem>();

            if (NetworkManager.Instance.IsServer && World.Instance != null)
            {
                if (pickupFromPrefab != null)
                {
                    World.Instance.ServerSpawnPickup(point.GlobalPosition, pickupFromPrefab.ItemData);
                    pickupFromPrefab.QueueFree();
                }
                return;
            }

            // Fallback: local spawn
            var instance = prefab.Instantiate<Node3D>();
            instance.GlobalPosition = point.GlobalPosition;
            AddChild(instance);
            _activePickups.Add(instance);
        }

        public void ClearLoot()
        {
            foreach (var pickup in _activePickups)
            {
                if (IsInstanceValid(pickup))
                    pickup.QueueFree();
            }
            _activePickups.Clear();
        }
    }
}
