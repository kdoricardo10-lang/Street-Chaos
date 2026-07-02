using Godot;

namespace StreetChaos
{
    public partial class HUD : Control
    {
        private Node _player;
        private HealthComponent _health;
        private MatchManager _matchManager;

        private ProgressBar _healthBar;
        private Label _healthLabel;
        private ProgressBar _staminaBar;
        private ProgressBar _shieldBar;
        private Label _playerCountLabel;
        private Label _matchTimerLabel;
        private Label _zoneWarningLabel;
        private Label _killFeedLabel;
        private Label _interactPromptLabel;
        private Control _crosshair;
        private ColorRect _damageOverlay;
        private Label _deathMessage;

        private AnimationPlayer _animPlayer;

        private readonly PanelContainer[] _weaponSlots = new PanelContainer[2];
        private readonly Label[] _weaponSlotLabels = new Label[2];
        private readonly Label[] _weaponKeyLabels = new Label[2];
        private readonly Label[] _weaponCooldownLabels = new Label[2];
        private readonly ColorRect[] _weaponFillBars = new ColorRect[2];

        private readonly PanelContainer[] _invSlots = new PanelContainer[4];
        private readonly Label[] _invSlotLabels = new Label[4];
        private readonly Label[] _invKeyLabels = new Label[4];

        private float _damageOverlayFade;
        private Vector2 _screenSize;
        private float _scale = 1f;

        private static readonly float RefW = 1920f;
        private static readonly float RefH = 1080f;

        private float GetSx() => _screenSize.X > 0 ? _screenSize.X / RefW : 1f;
        private float GetSy() => _screenSize.Y > 0 ? _screenSize.Y / RefH : 1f;
        private float GetScaleFactor() => Mathf.Min(GetSx(), GetSy());
        private int FontSize(int baseSize) => Mathf.Max(8, Mathf.RoundToInt(baseSize * _scale));
        private int BorderW(int baseW) => Mathf.Max(1, Mathf.RoundToInt(baseW * _scale));

        private static readonly Color SlotEmptyBg = new Color(0.15f, 0.15f, 0.15f, 0.9f);
        private static readonly Color SlotSelectedGlow = new Color(1f, 0.84f, 0f, 1f);
        private static readonly Color SlotBorder = new Color(0.3f, 0.3f, 0.3f, 1f);
        private static readonly Color WeaponAccent = new Color(0.85f, 0.4f, 0.1f, 1f);
        private static readonly Color HealingAccent = new Color(0.2f, 0.8f, 0.2f, 1f);
        private static readonly Color DamageAccent = new Color(1f, 0.2f, 0.2f, 1f);
        private static readonly Color SpeedAccent = new Color(0.2f, 1f, 0.2f, 1f);
        private static readonly Color DefenseAccent = new Color(0.2f, 0.5f, 1f, 1f);
        private static readonly Color InvisibilityAccent = new Color(0.7f, 0.2f, 1f, 1f);
        private static readonly Color JumpAccent = new Color(0f, 1f, 0.3f, 1f);

        private static Color GetItemAccent(ItemData item)
        {
            if (item == null) return SlotBorder;
            return item switch
            {
                WeaponData => WeaponAccent,
                HealingData => HealingAccent,
                BuffData b when b.BuffType == BuffType.Damage => DamageAccent,
                BuffData b when b.BuffType == BuffType.Speed => SpeedAccent,
                BuffData b when b.BuffType == BuffType.Defense => DefenseAccent,
                BuffData b when b.BuffType == BuffType.Invisibility => InvisibilityAccent,
                BuffData b when b.BuffType == BuffType.Jump => JumpAccent,
                _ => SlotBorder
            };
        }

        public override void _Ready()
        {
            _healthBar = GetNode<ProgressBar>("HealthBar");
            _healthLabel = GetNode<Label>("HealthBar/HealthLabel");
            _healthLabel.AddThemeFontSizeOverride("font_size", FontSize(14));
            _staminaBar = GetNode<ProgressBar>("StaminaBar");
            _shieldBar = GetNode<ProgressBar>("ShieldBar");
            _playerCountLabel = GetNode<Label>("PlayerCount");
            _matchTimerLabel = GetNode<Label>("MatchTimer");
            _zoneWarningLabel = GetNode<Label>("ZoneWarning");
            _killFeedLabel = GetNode<Label>("KillFeed");
            _interactPromptLabel = GetNode<Label>("InteractPrompt");
            _crosshair = GetNode<Control>("Crosshair");
            _damageOverlay = GetNode<ColorRect>("DamageOverlay");
            _deathMessage = GetNode<Label>("DeathMessage");

            _animPlayer = GetNode<AnimationPlayer>("AnimationPlayer");

            EnsureWeaponSlotsUi();
            EnsureInventorySlotsUi();

            Resized += OnResized;
            RecalculateLayout();

            _matchManager = MatchManager.Instance;
            if (_matchManager != null)
            {
                _matchManager.MatchStateChanged += OnMatchStateChanged;
                _matchManager.ZoneUpdated += OnZoneUpdated;
                _matchManager.PlayerCountChanged += OnPlayerCountChanged;
                _matchManager.MatchEnded += OnMatchEnded;
            }

            _damageOverlay.Modulate = new Color(1, 0, 0, 0);
        }

        public void ShowDeathMessage(int respawnSeconds)
        {
            if (_deathMessage != null)
            {
                _deathMessage.Text = $"Jogou mal\nRespawn em {respawnSeconds}s";
                _deathMessage.Visible = true;
            }
        }

        public void HideDeathMessage()
        {
            if (_deathMessage != null)
                _deathMessage.Visible = false;
        }

        public void UpdateRespawnCountdown(int seconds)
        {
            if (_deathMessage != null && _deathMessage.Visible)
                _deathMessage.Text = $"Jogou mal\nRespawn em {seconds}s";
        }

        public void ShowInteractPrompt(string text)
        {
            if (_interactPromptLabel == null) return;

            _interactPromptLabel.Text = text;
            _interactPromptLabel.Visible = true;
        }

        public void HideInteractPrompt()
        {
            if (_interactPromptLabel == null) return;

            _interactPromptLabel.Visible = false;
            _interactPromptLabel.Text = "";
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
            _scale = GetScaleFactor();

            // ShieldBar
            if (_shieldBar != null)
            {
                _shieldBar.OffsetLeft = -150f * sx;
                _shieldBar.OffsetTop = -116f * sy;
                _shieldBar.OffsetRight = 150f * sx;
                _shieldBar.OffsetBottom = -92f * sy;
            }
            // StaminaBar
            if (_staminaBar != null)
            {
                _staminaBar.OffsetLeft = -150f * sx;
                _staminaBar.OffsetTop = -96f * sy;
                _staminaBar.OffsetRight = 150f * sx;
                _staminaBar.OffsetBottom = -72f * sy;
            }
            // HealthBar
            if (_healthBar != null)
            {
                _healthBar.OffsetLeft = -150f * sx;
                _healthBar.OffsetTop = -40f * sy;
                _healthBar.OffsetRight = 150f * sx;
                _healthBar.OffsetBottom = -16f * sy;
            }
            // PlayerCount
            if (_playerCountLabel != null)
            {
                _playerCountLabel.OffsetLeft = 16f * sx;
                _playerCountLabel.OffsetTop = 16f * sy;
                _playerCountLabel.OffsetRight = 216f * sx;
                _playerCountLabel.OffsetBottom = 36f * sy;
            }
            // MatchTimer
            if (_matchTimerLabel != null)
            {
                _matchTimerLabel.OffsetLeft = -60f * sx;
                _matchTimerLabel.OffsetTop = 16f * sy;
                _matchTimerLabel.OffsetRight = 60f * sx;
                _matchTimerLabel.OffsetBottom = 40f * sy;
            }
            // ZoneWarning
            if (_zoneWarningLabel != null)
            {
                _zoneWarningLabel.OffsetLeft = -200f * sx;
                _zoneWarningLabel.OffsetTop = 60f * sy;
                _zoneWarningLabel.OffsetRight = 200f * sx;
                _zoneWarningLabel.OffsetBottom = 90f * sy;
            }
            // KillFeed
            if (_killFeedLabel != null)
            {
                _killFeedLabel.OffsetLeft = 0f;
                _killFeedLabel.OffsetTop = 120f * sy;
                _killFeedLabel.OffsetRight = -16f * sx;
                _killFeedLabel.OffsetBottom = 200f * sy;
            }
            // DeathMessage
            if (_deathMessage != null)
            {
                _deathMessage.OffsetLeft = -200f * sx;
                _deathMessage.OffsetTop = -50f * sy;
                _deathMessage.OffsetRight = 200f * sx;
                _deathMessage.OffsetBottom = 50f * sy;
            }
            // InteractPrompt
            if (_interactPromptLabel != null)
            {
                _interactPromptLabel.OffsetLeft = -120f * sx;
                _interactPromptLabel.OffsetTop = -206f * sy;
                _interactPromptLabel.OffsetRight = 120f * sx;
                _interactPromptLabel.OffsetBottom = -182f * sy;
            }
            // Crosshair
            if (_crosshair != null)
            {
                _crosshair.OffsetLeft = -8f * sx;
                _crosshair.OffsetTop = -8f * sy;
                _crosshair.OffsetRight = 8f * sx;
                _crosshair.OffsetBottom = 8f * sy;
            }

            // Weapon slots
            float[] wLeft = { -266f, 156f };
            float[] wRight = { -156f, 266f };
            for (int i = 0; i < 2; i++)
            {
                if (_weaponSlots[i] == null) continue;
                _weaponSlots[i].OffsetLeft = wLeft[i] * sx;
                _weaponSlots[i].OffsetTop = -96f * sy;
                _weaponSlots[i].OffsetRight = wRight[i] * sx;
                _weaponSlots[i].OffsetBottom = -16f * sy;
            }

            // Fill bars inside weapon slots
            for (int i = 0; i < 2; i++)
            {
                if (_weaponFillBars[i] == null) continue;
                _weaponFillBars[i].OffsetRight = 110f * sx;
                _weaponFillBars[i].OffsetBottom = 80f * sy;
                _weaponFillBars[i].OffsetTop = 80f * sy;
            }

            // Inventory slots (D-pad)
            float[] iLeft = { 110f, 20f, 110f, 200f };
            float[] iRight = { 180f, 90f, 180f, 270f };
            float[] iTop = { -100f, -215f, -330f, -215f };
            float[] iBottom = { -30f, -145f, -260f, -145f };
            for (int i = 0; i < 4; i++)
            {
                if (_invSlots[i] == null) continue;
                _invSlots[i].OffsetLeft = iLeft[i] * sx;
                _invSlots[i].OffsetTop = iTop[i] * sy;
                _invSlots[i].OffsetRight = iRight[i] * sx;
                _invSlots[i].OffsetBottom = iBottom[i] * sy;
            }

            UpdateFontSizes();
        }

        private void UpdateFontSizes()
        {
            if (_healthLabel != null)
                _healthLabel.AddThemeFontSizeOverride("font_size", FontSize(14));

            if (_playerCountLabel != null)
                _playerCountLabel.AddThemeFontSizeOverride("font_size", FontSize(16));
            if (_matchTimerLabel != null)
                _matchTimerLabel.AddThemeFontSizeOverride("font_size", FontSize(16));
            if (_zoneWarningLabel != null)
                _zoneWarningLabel.AddThemeFontSizeOverride("font_size", FontSize(14));
            if (_killFeedLabel != null)
                _killFeedLabel.AddThemeFontSizeOverride("font_size", FontSize(14));
            if (_interactPromptLabel != null)
                _interactPromptLabel.AddThemeFontSizeOverride("font_size", FontSize(13));
            if (_deathMessage != null)
                _deathMessage.AddThemeFontSizeOverride("font_size", FontSize(22));

            for (int i = 0; i < 2; i++)
            {
                if (_weaponSlotLabels[i] != null)
                    _weaponSlotLabels[i].AddThemeFontSizeOverride("font_size", FontSize(13));
                if (_weaponKeyLabels[i] != null)
                    _weaponKeyLabels[i].AddThemeFontSizeOverride("font_size", FontSize(11));
                if (_weaponCooldownLabels[i] != null)
                    _weaponCooldownLabels[i].AddThemeFontSizeOverride("font_size", FontSize(14));
            }
            for (int i = 0; i < 4; i++)
            {
                if (_invSlotLabels[i] != null)
                    _invSlotLabels[i].AddThemeFontSizeOverride("font_size", FontSize(10));
                if (_invKeyLabels[i] != null)
                    _invKeyLabels[i].AddThemeFontSizeOverride("font_size", FontSize(10));
            }
        }

        public void UpdateWeaponSlots(ItemData[] weapons, int selectedSlot, float[] activeTimers = null, float[] cooldowns = null)
        {
            for (int i = 0; i < 2; i++)
            {
                var slot = _weaponSlots[i];
                var label = _weaponSlotLabels[i];
                var keyLabel = _weaponKeyLabels[i];
                var cdLabel = _weaponCooldownLabels[i];
                var fillBar = i < _weaponFillBars.Length ? _weaponFillBars[i] : null;

                if (slot == null || label == null)
                    continue;

                ItemData item = weapons != null && i < weapons.Length ? weapons[i] : null;

                float active = activeTimers != null && i < activeTimers.Length ? activeTimers[i] : 0f;
                float cd = cooldowns != null && i < cooldowns.Length ? cooldowns[i] : 0f;
                bool isActive = active > 0f && item != null;
                bool onCooldown = !isActive && cd > 0f && item != null;

                Color accent = GetItemAccent(item);
                Color rarityColor = item != null ? RarityColors.GetColor(item.Rarity) : accent;
                Color bgColor = isActive ? new Color(0.25f, 0.2f, 0.05f, 0.9f) : (onCooldown ? new Color(0.15f, 0.15f, 0.15f, 0.9f) : SlotEmptyBg);
                Color borderColor = onCooldown ? new Color(0.3f, 0.3f, 0.3f, 1f) : SlotSelectedGlow;
                Color textColor = item != null
                    ? (onCooldown ? new Color(0.4f, 0.4f, 0.4f, 0.7f) : (isActive ? SlotSelectedGlow : rarityColor))
                    : new Color(0.5f, 0.5f, 0.5f, 1);

                var styleBox = new StyleBoxFlat
                {
                    BgColor = bgColor,
                    BorderWidthLeft = BorderW(2),
                    BorderWidthRight = BorderW(2),
                    BorderWidthBottom = BorderW(2),
                    BorderWidthTop = BorderW(3),
                    BorderColor = borderColor,
                    CornerRadiusTopLeft = BorderW(6),
                    CornerRadiusTopRight = BorderW(6),
                    CornerRadiusBottomLeft = BorderW(6),
                    CornerRadiusBottomRight = BorderW(6),
                    ShadowSize = BorderW(4),
                    ShadowOffset = new Vector2(0, BorderW(2)),
                    ShadowColor = new Color(0f, 0f, 0f, 0.3f)
                };

                slot.AddThemeStyleboxOverride("panel", styleBox);

                label.Text = item != null ? item.ItemName.ToUpper() : "";
                label.AddThemeColorOverride("font_color", textColor);
                label.AddThemeFontSizeOverride("font_size", FontSize(13));

                keyLabel.AddThemeColorOverride("font_color",
                    onCooldown ? new Color(0.4f, 0.4f, 0.4f, 0.7f) : (isActive ? SlotSelectedGlow : new Color(0.7f, 0.7f, 0.7f, 1)));
                keyLabel.AddThemeFontSizeOverride("font_size", FontSize(11));

                if (cdLabel != null)
                {
                    if (isActive)
                    {
                        int secs = Mathf.CeilToInt(active);
                        if (secs > 0)
                        {
                            cdLabel.Text = $"{secs}s";
                            cdLabel.Visible = true;
                        }
                        else
                        {
                            cdLabel.Visible = false;
                        }
                        cdLabel.AddThemeColorOverride("font_color", SlotSelectedGlow);
                        cdLabel.AddThemeFontSizeOverride("font_size", FontSize(14));
                    }
                    else if (onCooldown)
                    {
                        int secs = Mathf.CeilToInt(cd);
                        if (secs > 0)
                        {
                            cdLabel.Text = $"{secs}s";
                            cdLabel.Visible = true;
                        }
                        else
                        {
                            cdLabel.Visible = false;
                        }
                        cdLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.2f, 1f));
                        cdLabel.AddThemeFontSizeOverride("font_size", FontSize(14));
                    }
                    else
                    {
                        cdLabel.Visible = false;
                        cdLabel.Text = "";
                    }
                }

                if (fillBar != null)
                {
                    float fillMaxHeight = fillBar.OffsetBottom;
                    if (isActive)
                    {
                        float fill = Mathf.Clamp(active / 30f, 0f, 1f);
                        fillBar.OffsetTop = (1f - fill) * fillMaxHeight;
                        fillBar.OffsetBottom = fillMaxHeight;
                        fillBar.Color = accent * new Color(1f, 1f, 1f, 0.7f);
                        fillBar.Visible = true;
                    }
                    else if (onCooldown && item != null)
                    {
                        float fill = Mathf.Clamp(1f - (cd / 60f), 0f, 1f);
                        fillBar.OffsetTop = (1f - fill) * fillMaxHeight;
                        fillBar.OffsetBottom = fillMaxHeight;
                        fillBar.Color = accent * new Color(1f, 1f, 1f, 0.4f);
                        fillBar.Visible = true;
                    }
                    else
                    {
                        fillBar.Visible = false;
                    }
                }
            }
        }

        public void UpdateInventorySlots(ItemData[] items, int selectedSlot = -1)
        {
            for (int i = 0; i < 4; i++)
            {
                var slot = _invSlots[i];
                var label = _invSlotLabels[i];
                var keyLabel = _invKeyLabels[i];

                if (slot == null || label == null)
                    continue;

                ItemData item = items != null && i < items.Length ? items[i] : null;

                Color rarityColor = item != null ? RarityColors.GetColor(item.Rarity) : SlotBorder;
                bool isSelected = selectedSlot == i;

                var styleBox = new StyleBoxFlat
                {
                    BgColor = isSelected ? new Color(0.25f, 0.2f, 0.05f, 0.9f) : SlotEmptyBg,
                    BorderWidthLeft = BorderW(1),
                    BorderWidthRight = BorderW(1),
                    BorderWidthBottom = BorderW(1),
                    BorderWidthTop = BorderW(isSelected ? 3 : 2),
                    BorderColor = isSelected ? SlotSelectedGlow : rarityColor,
                    CornerRadiusTopLeft = BorderW(4),
                    CornerRadiusTopRight = BorderW(4),
                    CornerRadiusBottomLeft = BorderW(4),
                    CornerRadiusBottomRight = BorderW(4),
                    ShadowSize = BorderW(3),
                    ShadowOffset = new Vector2(0, BorderW(1)),
                    ShadowColor = new Color(0f, 0f, 0f, 0.3f)
                };

                slot.AddThemeStyleboxOverride("panel", styleBox);

                string displayName = item != null ? item.ItemName.ToUpper() : "";
                label.Text = displayName;
                label.AddThemeColorOverride("font_color", item != null
                    ? (isSelected ? SlotSelectedGlow : rarityColor)
                    : new Color(0.35f, 0.35f, 0.35f, 1));
                label.AddThemeFontSizeOverride("font_size", FontSize(10));

                keyLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1));
                keyLabel.AddThemeFontSizeOverride("font_size", FontSize(10));
            }
        }

        public void Setup(Node player)
        {
            _player = player;
            _health = player?.GetNode<HealthComponent>("HealthComponent");

            if (_health != null)
            {
                _health.HealthChanged += OnHealthChanged;
                _health.StaminaChanged += OnStaminaChanged;
                _health.ShieldChanged += OnShieldChanged;
                _health.TookDamage += OnTookDamage;

                OnHealthChanged(_health.CurrentHealth, _health.MaxHealth);
                OnStaminaChanged(_health.CurrentStamina, _health.MaxStamina);
                OnShieldChanged(_health.CurrentShield, _health.MaxShield);
            }

            if (player is PlayerController pc)
                pc.SyncInventoryHud();
        }

        public void SetSpectateTarget(Node target)
        {
            if (_health != null)
            {
                _health.HealthChanged -= OnHealthChanged;
                _health.StaminaChanged -= OnStaminaChanged;
                _health.ShieldChanged -= OnShieldChanged;
                _health.TookDamage -= OnTookDamage;
            }

            _player = target;
            _health = target?.GetNode<HealthComponent>("HealthComponent");

            if (_health != null)
            {
                _health.HealthChanged += OnHealthChanged;
                _health.StaminaChanged += OnStaminaChanged;
                _health.ShieldChanged += OnShieldChanged;
                _health.TookDamage += OnTookDamage;

                OnHealthChanged(_health.CurrentHealth, _health.MaxHealth);
                OnStaminaChanged(_health.CurrentStamina, _health.MaxStamina);
                OnShieldChanged(_health.CurrentShield, _health.MaxShield);
            }
        }

        private void EnsureWeaponSlotsUi()
        {
            if (_weaponSlots[0] != null)
                return;

            float[] slotLeft = { -266f, 156f };
            float[] slotRight = { -156f, 266f };

            for (int i = 0; i < 2; i++)
            {
                var slot = new PanelContainer
                {
                    Name = $"WeaponSlot{i + 1}",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    AnchorLeft = 0.5f,
                    AnchorTop = 1.0f,
                    AnchorRight = 0.5f,
                    AnchorBottom = 1.0f,
                    OffsetLeft = slotLeft[i],
                    OffsetTop = -96f,
                    OffsetRight = slotRight[i],
                    OffsetBottom = -16f,
                    Modulate = new Color(1, 1, 1, 0.95f)
                };

                var fillBar = new ColorRect
                {
                    Name = "FillBar",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Color = new Color(0, 0, 0, 0)
                };
                fillBar.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
                fillBar.OffsetRight = 110f;
                fillBar.OffsetBottom = 80f;
                fillBar.OffsetTop = 80f;
                slot.AddChild(fillBar);

                var vbox = new VBoxContainer
                {
                    Alignment = BoxContainer.AlignmentMode.Center
                };

                var keyLabel = new Label
                {
                    Text = $"[{i + 1}]",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkEnd
                };

                var itemLabel = new Label
                {
                    Text = "",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand,
                    ClipText = true
                };

                var cooldownLabel = new Label
                {
                    Text = "",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand,
                    Visible = false
                };

                vbox.AddChild(keyLabel);
                vbox.AddChild(itemLabel);
                vbox.AddChild(cooldownLabel);
                slot.AddChild(vbox);

                _weaponSlots[i] = slot;
                _weaponSlotLabels[i] = itemLabel;
                _weaponKeyLabels[i] = keyLabel;
                _weaponCooldownLabels[i] = cooldownLabel;
                _weaponFillBars[i] = fillBar;

                AddChild(slot);
            }
        }

        private void EnsureInventorySlotsUi()
        {
            if (_invSlots[0] != null)
                return;

            // D-pad layout on the left side (anchor: bottom-left)
            float[] slotLeft  = { 110f, 20f, 110f, 200f };
            float[] slotRight = { 180f, 90f, 180f, 270f };
            float[] slotTop   = { -100f, -215f, -330f, -215f };
            float[] slotBottom= { -30f, -145f, -260f, -145f };
            string[] keyNames = { "[3]", "[4]", "[5]", "[6]" };

            for (int i = 0; i < 4; i++)
            {
                var slot = new PanelContainer
                {
                    Name = $"InvSlot{i + 1}",
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    AnchorLeft = 0.0f,
                    AnchorTop = 1.0f,
                    AnchorRight = 0.0f,
                    AnchorBottom = 1.0f,
                    OffsetLeft = slotLeft[i],
                    OffsetTop = slotTop[i],
                    OffsetRight = slotRight[i],
                    OffsetBottom = slotBottom[i],
                    Modulate = new Color(1, 1, 1, 0.95f)
                };

                var vbox = new VBoxContainer
                {
                    Alignment = BoxContainer.AlignmentMode.Center
                };

                var keyLabel = new Label
                {
                    Text = keyNames[i],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkEnd
                };

                var itemLabel = new Label
                {
                    Text = "",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    SizeFlagsVertical = Control.SizeFlags.Fill | Control.SizeFlags.Expand,
                    ClipText = true
                };

                vbox.AddChild(keyLabel);
                vbox.AddChild(itemLabel);
                slot.AddChild(vbox);

                _invSlots[i] = slot;
                _invSlotLabels[i] = itemLabel;
                _invKeyLabels[i] = keyLabel;

                AddChild(slot);
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (_healthBar != null)
            {
                _healthBar.MaxValue = max;
                _healthBar.Value = current;
                _healthLabel.Text = $"{current}%";

                float ratio = Mathf.Clamp(current / max, 0f, 1f);
                if (ratio > 0.5f)
                    _healthBar.Modulate = new Color(0.2f, 0.8f, 0.2f, 1f);
                else if (ratio > 0.25f)
                    _healthBar.Modulate = new Color(0.8f, 0.8f, 0.2f, 1f);
                else
                    _healthBar.Modulate = new Color(0.8f, 0.2f, 0.2f, 1f);
            }
        }

        private void OnStaminaChanged(float current, float max)
        {
            if (_staminaBar != null)
            {
                _staminaBar.MaxValue = max;
                _staminaBar.Value = current;
            }
        }

        private void OnShieldChanged(float current, float max)
        {
            if (_shieldBar != null)
            {
                _shieldBar.MaxValue = max;
                _shieldBar.Value = current;
                _shieldBar.Visible = current > 0;
            }
        }

        private void OnTookDamage(float damage, Node attacker)
        {
            _damageOverlay.Modulate = new Color(1, 0, 0, 0.3f);
            _damageOverlayFade = 0.5f;
        }

        private void OnMatchStateChanged(MatchState state)
        {
            if (_matchTimerLabel != null)
                _matchTimerLabel.Text = $"Match: {state}";
        }

        private void OnZoneUpdated(Vector3 center, float radius, float targetRadius)
        {
            if (_matchTimerLabel != null)
            {
                float dist = _player != null
                    ? (_player as Node3D)?.GlobalPosition.DistanceTo(center) ?? 0
                    : 0;
                bool inside = dist <= radius;

                if (_zoneWarningLabel != null)
                {
                    _zoneWarningLabel.Visible = !inside;
                    _zoneWarningLabel.Text = inside ? "" : "FORA DA ZONA SEGURA!";
                }
            }
        }

        private void OnPlayerCountChanged(int count)
        {
            if (_playerCountLabel != null)
                _playerCountLabel.Text = $"Players: {count}";
        }

        private void OnMatchEnded(long winnerId)
        {
            if (_matchTimerLabel != null)
                _matchTimerLabel.Text = "MATCH ENDED!";
            if (_killFeedLabel != null)
                _killFeedLabel.Text = _player != null && (long)_player.GetInstanceId() == winnerId
                    ? "YOU WIN!" : "YOU LOST!";
        }

        public override void _Process(double delta)
        {
            if (_damageOverlayFade > 0)
            {
                _damageOverlayFade -= (float)delta;
                var c = _damageOverlay.Modulate;
                _damageOverlay.Modulate = new Color(c.R, c.G, c.B,
                    Mathf.Max(0, _damageOverlayFade * 0.6f));
            }

            if (_matchManager != null && _matchTimerLabel != null)
            {
                float time = _matchManager.MatchTime;
                int mins = (int)(time / 60);
                int secs = (int)(time % 60);
                _matchTimerLabel.Text = $"{mins:D2}:{secs:D2}";
            }
        }
    }
}
