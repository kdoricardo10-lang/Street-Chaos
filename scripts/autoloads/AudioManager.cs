using Godot;
using System.Collections.Generic;

namespace StreetChaos
{
    /// <summary>
    /// AudioManager handles all audio playback in the game.
    /// Provides simple methods to play SFX and music from resource paths.
    /// Audio assets should be placed in res://assets/audio/sfx/ and res://assets/audio/music/
    /// </summary>
    public partial class AudioManager : Node
    {
        public static AudioManager Instance { get; private set; }

        private AudioStreamPlayer _musicPlayer;
        private AudioStreamPlayer _ambiencePlayer;
        private readonly List<AudioStreamPlayer> _sfxPlayers = new();
        private int _sfxPlayerIndex;

        // Default audio bus names (configure in Audio tab)
        public const string BusMaster = "Master";
        public const string BusMusic = "Music";
        public const string BusSFX = "SFX";
        public const string BusAmbience = "Ambience";

        // Volume levels (linear 0-1, converted to dB internally)
        public float MasterVolume
        {
            get => DbToLinear(AudioServer.GetBusVolumeDb(GetBusIndex(BusMaster)));
            set => AudioServer.SetBusVolumeDb(GetBusIndex(BusMaster), LinearToDb(value));
        }
        public float MusicVolume
        {
            get => DbToLinear(AudioServer.GetBusVolumeDb(GetBusIndex(BusMusic)));
            set => AudioServer.SetBusVolumeDb(GetBusIndex(BusMusic), LinearToDb(value));
        }
        public float SFXVolume
        {
            get => DbToLinear(AudioServer.GetBusVolumeDb(GetBusIndex(BusSFX)));
            set => AudioServer.SetBusVolumeDb(GetBusIndex(BusSFX), LinearToDb(value));
        }
        public float AmbienceVolume
        {
            get => DbToLinear(AudioServer.GetBusVolumeDb(GetBusIndex(BusAmbience)));
            set => AudioServer.SetBusVolumeDb(GetBusIndex(BusAmbience), LinearToDb(value));
        }

        public override void _Ready()
        {
            Instance = this;
            SetupAudioBuses();
            CreateMusicPlayer();
            CreateAmbiencePlayer();
            CreateSFXPool();
        }

        private void SetupAudioBuses()
        {
            // Ensure default audio buses exist
            EnsureBus(BusMaster, 0); // Master is always index 0
            int musicIdx = EnsureBus(BusMusic, -1);
            int sfxIdx = EnsureBus(BusSFX, -1);
            int ambIdx = EnsureBus(BusAmbience, -1);

            // Route SFX -> Master, Music -> Master, Ambience -> Master
            if (sfxIdx > 0) AudioServer.SetBusSend(sfxIdx, BusMaster);
            if (musicIdx > 0) AudioServer.SetBusSend(musicIdx, BusMaster);
            if (ambIdx > 0) AudioServer.SetBusSend(ambIdx, BusMaster);

            GD.Print($"Audio buses ready: Master={GetBusIndex(BusMaster)}, Music={GetBusIndex(BusMusic)}, SFX={GetBusIndex(BusSFX)}, Ambience={GetBusIndex(BusAmbience)}");
        }

        private static int EnsureBus(string name, int preferredIndex)
        {
            int idx = GetBusIndex(name);
            if (idx >= 0) return idx;

            if (preferredIndex >= 0 && preferredIndex < AudioServer.BusCount)
            {
                AudioServer.SetBusName(preferredIndex, name);
                return preferredIndex;
            }

            AudioServer.AddBus();
            int newIdx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(newIdx, name);
            return newIdx;
        }

        private static int GetBusIndex(string name)
        {
            for (int i = 0; i < AudioServer.BusCount; i++)
            {
                if (AudioServer.GetBusName(i) == name)
                    return i;
            }
            return -1;
        }

        private void CreateMusicPlayer()
        {
            _musicPlayer = new AudioStreamPlayer
            {
                Name = "MusicPlayer",
                Bus = BusMusic
            };
            AddChild(_musicPlayer);
        }

        private void CreateAmbiencePlayer()
        {
            _ambiencePlayer = new AudioStreamPlayer
            {
                Name = "AmbiencePlayer",
                Bus = BusAmbience
            };
            AddChild(_ambiencePlayer);
        }

        private void CreateSFXPool(int poolSize = 8)
        {
            for (int i = 0; i < poolSize; i++)
            {
                var player = new AudioStreamPlayer
                {
                    Name = $"SFXPlayer_{i}",
                    Bus = BusSFX
                };
                AddChild(player);
                _sfxPlayers.Add(player);
            }
        }

        // ── Music ────────────────────────────────────────────────────────

        public void PlayMusic(string resourcePath, float volumeDb = 0f, bool loop = true)
        {
            var stream = LoadStream(resourcePath);
            if (stream == null) return;
            PlayMusic(stream, volumeDb, loop);
        }

        public void PlayMusic(AudioStream stream, float volumeDb = 0f, bool loop = true)
        {
            if (stream == null) return;

            if (stream is AudioStreamOggVorbis ogg)
                ogg.Loop = loop;

            _musicPlayer.Stream = stream;
            _musicPlayer.VolumeDb = volumeDb;
            _musicPlayer.Play();
        }

        public void StopMusic(float fadeTime = 0f)
        {
            if (fadeTime <= 0f)
            {
                _musicPlayer.Stop();
                return;
            }

            var tween = CreateTween();
            tween.TweenProperty(_musicPlayer, "volume_db", -80f, fadeTime);
            tween.TweenCallback(Callable.From(() => _musicPlayer.Stop()));
        }

        public bool IsMusicPlaying => _musicPlayer.Playing;

        // ── Ambience ─────────────────────────────────────────────────────

        public void PlayAmbience(string resourcePath, float volumeDb = -10f, bool loop = true)
        {
            var stream = LoadStream(resourcePath);
            if (stream == null) return;
            PlayAmbience(stream, volumeDb, loop);
        }

        public void PlayAmbience(AudioStream stream, float volumeDb = -10f, bool loop = true)
        {
            if (stream == null) return;

            if (stream is AudioStreamOggVorbis ogg)
                ogg.Loop = loop;

            _ambiencePlayer.Stream = stream;
            _ambiencePlayer.VolumeDb = volumeDb;
            _ambiencePlayer.Play();
        }

        public void StopAmbience()
        {
            _ambiencePlayer.Stop();
        }

        // ── SFX ──────────────────────────────────────────────────────────

        public void PlaySFX(string resourcePath, float volumeDb = 0f, float pitchScale = 1f)
        {
            var stream = LoadStream(resourcePath);
            if (stream == null) return;
            PlaySFX(stream, volumeDb, pitchScale);
        }

        public void PlaySFX(AudioStream stream, float volumeDb = 0f, float pitchScale = 1f)
        {
            if (stream == null) return;

            var player = GetNextSFXPlayer();
            player.Stream = stream;
            player.VolumeDb = volumeDb;
            player.PitchScale = pitchScale;
            player.Play();
        }

        public void PlaySFXAtPosition(string resourcePath, Vector3 position, float volumeDb = 0f)
        {
            var stream = LoadStream(resourcePath);
            if (stream == null) return;

            // Use a positional 3D player for world-space sounds
            var player = new AudioStreamPlayer3D
            {
                Stream = stream,
                VolumeDb = volumeDb,
                MaxDistance = 50f,
                AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseSquareDistance,
                GlobalPosition = position
            };
            AddChild(player);
            player.Play();

            // Auto-cleanup when finished
            player.Finished += () =>
            {
                if (IsInstanceValid(player))
                    player.QueueFree();
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private AudioStreamPlayer GetNextSFXPlayer()
        {
            var player = _sfxPlayers[_sfxPlayerIndex];
            _sfxPlayerIndex = (_sfxPlayerIndex + 1) % _sfxPlayers.Count;
            return player;
        }

        private static AudioStream LoadStream(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            var stream = GD.Load<AudioStream>(resourcePath);
            if (stream == null)
                GD.PrintErr($"AudioManager: Failed to load audio stream: {resourcePath}");
            return stream;
        }

        private static float LinearToDb(float linear)
        {
            return linear <= 0.001f ? -80f : Mathf.Log(linear) * 10f;
        }

        private static float DbToLinear(float db)
        {
            return db <= -80f ? 0f : Mathf.Exp(db / 10f);
        }
    }
}
