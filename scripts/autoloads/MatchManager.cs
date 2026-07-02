using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    public partial class MatchManager : Node
    {
        public static MatchManager Instance { get; private set; }

        [Signal] public delegate void MatchStateChangedEventHandler(MatchState newState);
        [Signal] public delegate void ZoneUpdatedEventHandler(Vector3 center, float radius, float targetRadius);
        [Signal] public delegate void PlayerCountChangedEventHandler(int count);
        [Signal] public delegate void MatchEndedEventHandler(long winnerId);

        [Export] public int StartingPlayers { get; set; } = 40;
        [Export] public float MatchStartDelay { get; set; } = 10f;
        [Export] public float ZoneCloseInterval { get; set; } = 60f;
        [Export] public float ZoneCloseDuration { get; set; } = 30f;
        [Export] public float InitialZoneRadius { get; set; } = 100f;
        [Export] public float MinZoneRadius { get; set; } = 5f;
        [Export] public int FinalCircleCount { get; set; } = 3;

        public MatchState CurrentMatchState { get; private set; } = MatchState.Waiting;
        public SafeZone CurrentZone { get; private set; }
        public int AlivePlayerCount { get; private set; }
        public float MatchTime { get; private set; }

        private int _currentCircle;
        private float _zoneTimer;
        private bool _isZoneClosing;
        private Vector3 _mapCenter;
        private float _startTimer;

        private readonly Dictionary<long, Node> _allPlayers = new();

        public override void _Ready()
        {
            Instance = this;
            _mapCenter = Vector3.Zero;
            CurrentZone = new SafeZone
            {
                Center = _mapCenter,
                Radius = InitialZoneRadius,
                TargetRadius = InitialZoneRadius,
                ShrinkSpeed = 0
            };
        }

        public override void _Process(double delta)
        {
            if (!Multiplayer.IsServer()) return;
            float dt = (float)delta;

            switch (CurrentMatchState)
            {
                case MatchState.Waiting:
                    break;

                case MatchState.Starting:
                    _startTimer -= dt;
                    if (_startTimer <= 0)
                        StartMatch();
                    break;

                case MatchState.InProgress:
                case MatchState.ZoneClosing:
                    MatchTime += dt;
                    UpdateZone(dt);
                    CheckWinCondition();
                    break;

                case MatchState.Finished:
                    break;
            }
        }

        public void BeginMatch()
        {
            if (!Multiplayer.IsServer()) return;
            CurrentMatchState = MatchState.Starting;
            _startTimer = MatchStartDelay;
            _currentCircle = 0;
            MatchTime = 0;
            EmitSignal(SignalName.MatchStateChanged, (int)CurrentMatchState);
            Rpc(nameof(RpcSyncMatchState), (int)CurrentMatchState, MatchTime, AlivePlayerCount);
        }

        private void StartMatch()
        {
            CurrentMatchState = MatchState.InProgress;
            _zoneTimer = ZoneCloseInterval;
            _currentCircle = 0;
            EmitSignal(SignalName.MatchStateChanged, (int)CurrentMatchState);
            Rpc(nameof(RpcSyncMatchState), (int)CurrentMatchState, MatchTime, AlivePlayerCount);

            var lootManager = LootManager.Instance;
            if (lootManager != null)
                lootManager.SpawnLoot();
        }

        private void UpdateZone(float delta)
        {
            _zoneTimer -= delta;

            if (_zoneTimer <= 0 && !_isZoneClosing && _currentCircle < FinalCircleCount)
            {
                _isZoneClosing = true;
                CurrentMatchState = MatchState.ZoneClosing;
                EmitSignal(SignalName.MatchStateChanged, (int)CurrentMatchState);
                Rpc(nameof(RpcSyncMatchState), (int)CurrentMatchState, MatchTime, AlivePlayerCount);

                _currentCircle++;
                float targetRadius = Mathf.Max(MinZoneRadius, CurrentZone.Radius * 0.5f);

                CurrentZone = new SafeZone
                {
                    Center = _mapCenter + new Vector3(
                        GD.Randf() * 40f - 20f, 0, GD.Randf() * 40f - 20f),
                    Radius = CurrentZone.Radius,
                    TargetRadius = targetRadius,
                    ShrinkSpeed = (CurrentZone.Radius - targetRadius) / ZoneCloseDuration
                };

                EmitSignal(SignalName.ZoneUpdated, CurrentZone.Center,
                    CurrentZone.Radius, CurrentZone.TargetRadius);
                Rpc(nameof(RpcSyncZone), CurrentZone.Center, CurrentZone.Radius, CurrentZone.TargetRadius);
            }

            if (_isZoneClosing)
            {
                float newRadius = Mathf.Max(CurrentZone.TargetRadius,
                    CurrentZone.Radius - CurrentZone.ShrinkSpeed * (float)delta);
                CurrentZone = new SafeZone
                {
                    Center = CurrentZone.Center,
                    Radius = newRadius,
                    TargetRadius = CurrentZone.TargetRadius,
                    ShrinkSpeed = CurrentZone.ShrinkSpeed
                };
                EmitSignal(SignalName.ZoneUpdated, CurrentZone.Center,
                    CurrentZone.Radius, CurrentZone.TargetRadius);

                if (newRadius <= CurrentZone.TargetRadius)
                {
                    _isZoneClosing = false;
                    if (_currentCircle >= FinalCircleCount)
                    {
                        CurrentMatchState = MatchState.FinalCircle;
                        EmitSignal(SignalName.MatchStateChanged, (int)CurrentMatchState);
                        Rpc(nameof(RpcSyncMatchState), (int)CurrentMatchState, MatchTime, AlivePlayerCount);
                    }
                    else
                    {
                        CurrentMatchState = MatchState.InProgress;
                        _zoneTimer = ZoneCloseInterval;
                        EmitSignal(SignalName.MatchStateChanged, (int)CurrentMatchState);
                        Rpc(nameof(RpcSyncMatchState), (int)CurrentMatchState, MatchTime, AlivePlayerCount);
                    }
                }

                Rpc(nameof(RpcSyncZone), CurrentZone.Center, CurrentZone.Radius, CurrentZone.TargetRadius);
            }

            DamagePlayersOutsideZone();
        }

        private void DamagePlayersOutsideZone()
        {
            float baseDamage = 2f + (_currentCircle * 2f);
            float dt = (float)GetProcessDeltaTime();

            foreach (var kvp in _allPlayers)
            {
                long peerId = kvp.Key;
                var player = kvp.Value as Node3D;
                if (!IsInstanceValid(player)) continue;

                float dist = player.GlobalPosition.DistanceTo(CurrentZone.Center);
                if (dist > CurrentZone.Radius)
                {
                    float damage = baseDamage * dt;
                    var health = player.GetNode<HealthComponent>("HealthComponent");
                    if (health == null) continue;

                    health.ServerTakeDamage(damage);
                }
            }
        }

        public void RegisterPlayer(long peerId, Node player)
        {
            if (!_allPlayers.ContainsKey(peerId))
                _allPlayers[peerId] = player;

            AlivePlayerCount = _allPlayers.Count;
            EmitSignal(SignalName.PlayerCountChanged, AlivePlayerCount);
            if (Multiplayer.IsServer())
                Rpc(nameof(RpcSyncPlayerCount), AlivePlayerCount);
        }

        public void UnregisterPlayer(long peerId)
        {
            _allPlayers.Remove(peerId);
            AlivePlayerCount = _allPlayers.Count;
            EmitSignal(SignalName.PlayerCountChanged, AlivePlayerCount);
            if (Multiplayer.IsServer())
                Rpc(nameof(RpcSyncPlayerCount), AlivePlayerCount);
        }

        private void CheckWinCondition()
        {
            int alive = 0;
            Node winner = null;

            foreach (var kvp in _allPlayers)
            {
                var player = kvp.Value;
                if (!IsInstanceValid(player)) continue;
                var health = player.GetNode<HealthComponent>("HealthComponent");
                if (health != null && health.IsAlive())
                {
                    alive++;
                    winner = player;
                }
            }

            AlivePlayerCount = alive;
            EmitSignal(SignalName.PlayerCountChanged, alive);
            Rpc(nameof(RpcSyncPlayerCount), alive);

            if (alive <= 1 && CurrentMatchState != MatchState.Waiting
                && CurrentMatchState != MatchState.Finished)
            {
                EndMatch(winner);
            }
        }

        private void EndMatch(Node winner)
        {
            CurrentMatchState = MatchState.Finished;
            long winnerId = winner != null ? (long)winner.GetInstanceId() : -1;
            EmitSignal(SignalName.MatchEnded, winnerId);
            Rpc(nameof(RpcSyncMatchEnded), winnerId);
        }

        public Node GetPlayerByPeerId(long peerId)
        {
            _allPlayers.TryGetValue(peerId, out var player);
            return player;
        }

        public bool IsInsideZone(Vector3 position)
        {
            float dist = position.DistanceTo(CurrentZone.Center);
            return dist <= CurrentZone.Radius;
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        private void RpcSyncMatchState(int state, float matchTime, int aliveCount)
        {
            CurrentMatchState = (MatchState)state;
            MatchTime = matchTime;
            AlivePlayerCount = aliveCount;
            EmitSignal(SignalName.MatchStateChanged, state);
            EmitSignal(SignalName.PlayerCountChanged, aliveCount);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        private void RpcSyncZone(Vector3 center, float radius, float targetRadius)
        {
            CurrentZone = new SafeZone
            {
                Center = center,
                Radius = radius,
                TargetRadius = targetRadius,
                ShrinkSpeed = 0
            };
            EmitSignal(SignalName.ZoneUpdated, center, radius, targetRadius);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        private void RpcSyncPlayerCount(int count)
        {
            AlivePlayerCount = count;
            EmitSignal(SignalName.PlayerCountChanged, count);
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        private void RpcSyncMatchEnded(long winnerId)
        {
            CurrentMatchState = MatchState.Finished;
            EmitSignal(SignalName.MatchEnded, winnerId);
        }
    }
}
