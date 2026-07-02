using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    public partial class World : Node3D
    {
        public static World Instance { get; private set; }

        [Export] public PackedScene PlayerScene { get; set; }
        [Export] public Node3D PlayerSpawnPoint { get; set; }
        [Export] public FightingStyleData DefaultFightingStyle { get; set; }
        [Export] public PackedScene HUDScene { get; set; }

        private readonly Dictionary<long, Node> _playerMap = new();
        private readonly List<NpcController> _npcs = new();
        private readonly Dictionary<int, PickupItem> _pickupMap = new();
        private int _nextPickupId = 1;
        private bool _matchStarted;
        private float _lobbyTimer = -1f;

        public override void _Ready()
        {
            Instance = this;
            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                gameManager.PlayerScene = PlayerScene;
                gameManager.HUDScene = HUDScene;
                if (DefaultFightingStyle == null)
                    DefaultFightingStyle = BaseStyles.CreateStreetBrawler();
                gameManager.DefaultFightingStyle = DefaultFightingStyle;
            }

            var net = NetworkManager.Instance;
            net.ServerStarted += OnHostReady;
            net.ClientConnected += OnClientReady;
            net.PeerConnected += OnPeerConnected;
            net.PeerDisconnected += OnPeerDisconnected;

            if (net.IsDedicatedServer)
            {
                GD.Print("Servidor dedicado — auto-hosting...");
                net.HostGame();
            }
            else if (!net.IsNetworkConnected)
            {
                GD.Print("Auto-hosting (modo offline)...");
                net.HostGame();
                GD.Print("Pressione F2 para CONECTAR a outro servidor");
            }
            else
            {
                GD.Print($"Já conectado como {(net.IsServer ? "host" : "cliente")}. Pulando auto-host.");

                if (net.IsServer)
                {
                    OnHostReady();
                }
            }

            SpawnNpcs();
        }

        public override void _Process(double delta)
        {
            float dt = (float)delta;

            if (!_matchStarted)
            {
                if (_lobbyTimer > 0)
                {
                    _lobbyTimer -= dt;
                    if (_lobbyTimer <= 0)
                        StartMatch();
                }
                return;
            }
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is InputEventKey { Pressed: true, Keycode: Key.F1 })
            {
                if (!NetworkManager.Instance.IsNetworkConnected)
                    NetworkManager.Instance.HostGame();
            }
            else if (@event is InputEventKey { Pressed: true, Keycode: Key.F2 })
            {
                NetworkManager.Instance.JoinGame();
            }
        }

        private void OnHostReady()
        {
            var net = NetworkManager.Instance;
            if (net.IsDedicatedServer)
            {
                GD.Print("Servidor dedicado pronto. Aguardando jogadores...");
            }
            else
            {
                GD.Print("Host pronto. Spawnando jogador local...");
                long sid = Multiplayer.GetUniqueId();
                SpawnPlayer(sid, GetSpawnPos());
                _matchStarted = true;
                MatchManager.Instance?.BeginMatch();
            }
        }

        private void OnClientReady()
        {
            GD.Print("Conectado ao servidor. Aguardando spawn...");
            // Clear locally-hosted players and NPCs
            foreach (var kvp in _playerMap)
            {
                if (IsInstanceValid(kvp.Value))
                    kvp.Value.QueueFree();
            }
            _playerMap.Clear();

            foreach (var kvp in _pickupMap)
            {
                if (IsInstanceValid(kvp.Value))
                    kvp.Value.QueueFree();
            }
            _pickupMap.Clear();
        }

        private void OnPeerConnected(long peerId)
        {
            GD.Print($"Peer conectado: {peerId}");
            if (!NetworkManager.Instance.IsServer) return;

            // Envia todos os players existentes para o novo peer
            foreach (var kvp in _playerMap)
            {
                var p = kvp.Value as PlayerController;
                if (p == null) continue;
                RpcId(peerId, nameof(CreatePlayer), kvp.Key,
                    p.GlobalPosition, FightingStyleToIndex(DefaultFightingStyle));
            }

            // Cria player para o novo peer
            SpawnPlayer(peerId, GetSpawnPos());

            // Lobby: aguarda mais jogadores antes de iniciar
            if (!_matchStarted && NetworkManager.Instance.IsDedicatedServer)
            {
                if (_lobbyTimer < 0)
                    _lobbyTimer = 8f; // 8s para outros jogadores entrarem
                GD.Print($"Jogadores no lobby: {_playerMap.Count}. Iniciando em {_lobbyTimer:F1}s...");
            }
        }

        private void OnPeerDisconnected(long peerId)
        {
            GD.Print($"Peer desconectou: {peerId}");
            if (!NetworkManager.Instance.IsServer) return;
            RemovePlayer(peerId);
        }

        private void StartMatch()
        {
            if (_matchStarted) return;
            _matchStarted = true;
            GD.Print($"Partida iniciada com {_playerMap.Count} jogadores!");
            MatchManager.Instance?.BeginMatch();
        }

        private void SpawnPlayer(long peerId, Vector3 position)
        {
            var player = PlayerScene.Instantiate<PlayerController>();
            player.Name = $"Player_{peerId}";
            AddChild(player);
            player.GlobalPosition = position;
            player.SetMultiplayerAuthority((int)peerId);

            var style = DefaultFightingStyle ?? BaseStyles.CreateStreetBrawler();
            player.GetNode<CombatComponent>("CombatComponent")?.SetFightingStyle(style);

            _playerMap[peerId] = player;
            MatchManager.Instance?.RegisterPlayer(peerId, player);

            if (player.IsMultiplayerAuthority())
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.SetCurrentPlayer(player);
                    gm.SetupHUD();
                }
            }

            if (NetworkManager.Instance.IsServer)
            {
                Rpc(nameof(CreatePlayer), peerId, position, FightingStyleToIndex(style));
            }
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        private void CreatePlayer(long peerId, Vector3 position, int styleIndex)
        {
            var player = PlayerScene.Instantiate<PlayerController>();
            player.Name = $"Player_{peerId}";
            AddChild(player);
            player.GlobalPosition = position;
            player.SetMultiplayerAuthority((int)peerId);

            var style = IndexToFightingStyle(styleIndex);
            player.GetNode<CombatComponent>("CombatComponent")?.SetFightingStyle(style);

            _playerMap[peerId] = player;
            MatchManager.Instance?.RegisterPlayer(peerId, player);

            if (player.IsMultiplayerAuthority())
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.SetCurrentPlayer(player);
                    gm.SetupHUD();
                }
            }
        }

        private void RemovePlayer(long peerId)
        {
            if (_playerMap.TryGetValue(peerId, out var player))
            {
                MatchManager.Instance?.UnregisterPlayer(peerId);
                player.QueueFree();
                _playerMap.Remove(peerId);
            }
        }

        private void SpawnNpcs()
        {
            Vector3 basePos = GetSpawnPos();
            float offset = 4f;
            var positions = new[]
            {
                basePos + new Vector3(offset, 0, 0),
                basePos + new Vector3(-offset, 0, 0),
                basePos + new Vector3(0, 0, offset),
            };

            foreach (var pos in positions)
            {
                var npc = new NpcController();
                npc.Name = $"Npc_{_npcs.Count}";
                AddChild(npc);
                npc.GlobalPosition = pos;
                _npcs.Add(npc);
            }
            GD.Print($"Spawned {_npcs.Count} NPCs");

            // Register loot spawn markers from the city block
            RegisterLootSpawnPoints();
        }

        private void RegisterLootSpawnPoints()
        {
            var lootManager = LootManager.Instance;
            if (lootManager == null) return;

            var markers = new System.Collections.Generic.List<Marker3D>();
            FindLootSpawnMarkers(this, markers);

            foreach (var m in markers)
            {
                lootManager.RegisterSpawnPoint(m);
            }
            GD.Print($"Registered {markers.Count} loot spawn points");
        }

        private static void FindLootSpawnMarkers(Node parent, System.Collections.Generic.List<Marker3D> results)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is Marker3D marker && child.Name.ToString().StartsWith("LootSpawn_"))
                {
                    results.Add(marker);
                }
                if (child.GetChildCount() > 0)
                {
                    FindLootSpawnMarkers(child, results);
                }
            }
        }

        private Vector3 GetSpawnPos()
        {
            Vector3 basePos = PlayerSpawnPoint?.GlobalPosition ?? Vector3.Zero;
            return basePos + new Vector3(
                GD.Randf() * 8f - 4f, 0, GD.Randf() * 8f - 4f);
        }

        private int FightingStyleToIndex(FightingStyleData style)
        {
            return style?.ResourcePath?.GetHashCode() ?? 0;
        }

        private FightingStyleData IndexToFightingStyle(int index)
        {
            return DefaultFightingStyle ?? BaseStyles.CreateStreetBrawler();
        }

        // â”€â”€ Loot / Pickup Network Sync â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public void ServerSpawnPickup(Vector3 position, ItemData item)
        {
            if (!NetworkManager.Instance.IsServer) return;

            int netId = _nextPickupId++;
            Rpc(nameof(RpcSpawnPickup), netId, position, item?.ResourcePath ?? "");
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        private void RpcSpawnPickup(int netId, Vector3 position, string itemPath)
        {
            var pickupScene = GD.Load<PackedScene>("res://pickups/items/PickupItem.tscn");
            if (pickupScene == null) return;

            var pickup = pickupScene.Instantiate<PickupItem>();
            pickup.Name = $"Pickup_{netId}";
            pickup.GlobalPosition = position;
            pickup.NetworkId = netId;

            if (!string.IsNullOrEmpty(itemPath))
            {
                var item = GD.Load<ItemData>(itemPath);
                if (item != null)
                    pickup.AssignItemData(item);
            }

            AddChild(pickup);
            _pickupMap[netId] = pickup;
        }

        public void ServerCollectPickup(int pickupNetId, int playerPeerId)
        {
            if (!NetworkManager.Instance.IsServer) return;

            if (!_pickupMap.TryGetValue(pickupNetId, out var pickup)) return;
            if (!IsInstanceValid(pickup)) return;

            var player = MatchManager.Instance?.GetPlayerByPeerId(playerPeerId);
            if (player == null || !IsInstanceValid(player)) return;

            var pc = player as PlayerController;
            if (pc == null) return;

            if (pc.AcquireItem(pickup.ItemData))
            {
                pickup.QueueFree();
                _pickupMap.Remove(pickupNetId);
                Rpc(nameof(RpcRemovePickup), pickupNetId);
            }
        }

        [Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false)]
        private void RpcRemovePickup(int netId)
        {
            if (_pickupMap.TryGetValue(netId, out var pickup))
            {
                if (IsInstanceValid(pickup))
                    pickup.QueueFree();
                _pickupMap.Remove(netId);
            }
        }
    }
}



