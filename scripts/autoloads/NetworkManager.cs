using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    public partial class NetworkManager : Node
    {
        public static NetworkManager Instance { get; private set; }

        public const int DefaultPort = 12345;
        public const int MaxPlayers = 40;

        public bool IsServer { get; private set; }
        public bool IsNetworkConnected { get; private set; }
        public bool IsDedicatedServer { get; private set; }

        [Signal] public delegate void ServerStartedEventHandler();
        [Signal] public delegate void ClientConnectedEventHandler();
        [Signal] public delegate void PeerConnectedEventHandler(long peerId);
        [Signal] public delegate void PeerDisconnectedEventHandler(long peerId);
        [Signal] public delegate void ConnectionFailedEventHandler();

        private readonly List<long> _peers = new();
        public IReadOnlyList<long> Peers => _peers;
        public int PeerCount => _peers.Count;

        public override void _Ready()
        {
            Instance = this;
            DetectDedicatedServer();
        }

        private void DetectDedicatedServer()
        {
            var args = OS.GetCmdlineArgs();
            foreach (var arg in args)
            {
                string lower = arg.ToLower();
                if (lower == "--server" || lower == "-server" || lower == "--dedicated-server")
                {
                    IsDedicatedServer = true;
                    GD.Print("=== SERVIDOR DEDICADO ===");
                    break;
                }
            }
        }

        public void HostGame(int port = DefaultPort)
        {
            var peer = new ENetMultiplayerPeer();
            Error err = peer.CreateServer(port, MaxPlayers);
            if (err != Error.Ok)
            {
                GD.PrintErr($"Falha ao criar servidor: {err}");
                EmitSignal(SignalName.ConnectionFailed);
                return;
            }

            Multiplayer.MultiplayerPeer = peer;
            IsServer = true;
            IsNetworkConnected = true;
            _peers.Clear();

            Multiplayer.PeerConnected += OnPeerConnected;
            Multiplayer.PeerDisconnected += OnPeerDisconnected;

            EmitSignal(SignalName.ServerStarted);
            GD.Print($"Servidor rodando na porta {port} (max {MaxPlayers} jogadores)");
        }

        public void JoinGame(string address = "127.0.0.1", int port = DefaultPort)
        {
            var peer = new ENetMultiplayerPeer();
            Error err = peer.CreateClient(address, port);
            if (err != Error.Ok)
            {
                GD.PrintErr($"Falha ao conectar: {err}");
                EmitSignal(SignalName.ConnectionFailed);
                return;
            }

            Multiplayer.MultiplayerPeer = peer;
            IsServer = false;
            IsNetworkConnected = true;

            Multiplayer.PeerConnected += OnPeerConnected;
            Multiplayer.PeerDisconnected += OnPeerDisconnected;
            Multiplayer.ConnectedToServer += OnConnectedToServer;
            Multiplayer.ConnectionFailed += OnConnectionFailed;

            GD.Print($"Conectando a {address}:{port}...");
        }

        private void OnConnectedToServer()
        {
            GD.Print("Conectado ao servidor!");
            EmitSignal(SignalName.ClientConnected);
        }

        private void OnConnectionFailed()
        {
            GD.PrintErr("Conexão falhou!");
            IsNetworkConnected = false;
            EmitSignal(SignalName.ConnectionFailed);
        }

        private void OnPeerConnected(long peerId)
        {
            if (!_peers.Contains(peerId))
                _peers.Add(peerId);
            GD.Print($"Peer conectado: {peerId} ({_peers.Count} online)");
            EmitSignal(SignalName.PeerConnected, peerId);
        }

        private void OnPeerDisconnected(long peerId)
        {
            _peers.Remove(peerId);
            GD.Print($"Peer desconectou: {peerId} ({_peers.Count} online)");
            EmitSignal(SignalName.PeerDisconnected, peerId);
        }

        public void Disconnect()
        {
            Multiplayer.MultiplayerPeer = null;
            IsNetworkConnected = false;
            IsServer = false;
            _peers.Clear();
        }
    }
}
