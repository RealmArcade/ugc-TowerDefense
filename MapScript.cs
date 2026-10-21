namespace Realm.Maps;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Realm.MapAPI;

public class CustomMap : IWasmModule
{
    private sealed record WaveConfig(
        string[] Composition,
        int TotalCount,
        float SpawnInterval,
        float InterWaveDelay,
        MiniBossInfo? MiniBoss = null,
        bool IsBoss = false);

    private sealed record MiniBossInfo(string BannerKey, Vector3 Color);

    private const int TotalWaves = 15;
    private const float BasePassiveGoldPerSecond = 8.0f;
    private const float InitialGold = 300.0f;
    private const float SpawnHeight = 3.0f;
    private const float SpawnRingMinRadius = 14.0f;
    private const float SpawnRingMaxRadius = 17.0f;

    private const float ShopRotationInterval = 40.0f;
    private const float ShopRerollCost = 100.0f;

    private const float BaseHeroHp = 1200.0f;
    private const float BaseHeroDamage = 55.0f;
    private const float BaseHeroRange = 38.0f;
    private const float BaseHeroArmor = 5.0f;
    private const float MultiShotSplashRatio = 0.70f;

    private enum PerkTier
    {
        Normal = 45,
        Raro = 35,
        Epico = 15,
        Maldito = 5
    }

    private sealed class PerkDefinition
    {
        public string Id { get; }
        public string NameKey { get; }
        public string DescKey { get; }
        public PerkTier Tier { get; }
        public bool IsUnique { get; }
        public int MaxStacks { get; }
        public Vector3 Color { get; }
        public string IconPath { get; }

        public PerkDefinition(string id, string nameKey, string descKey, PerkTier tier, Vector3 color, string iconPath, bool isUnique = false, int maxStacks = 5)
        {
            Id = id;
            NameKey = nameKey;
            DescKey = descKey;
            Tier = tier;
            Color = color;
            IconPath = iconPath;
            IsUnique = isUnique;
            MaxStacks = isUnique ? 1 : maxStacks;
        }
    }

    private static readonly PerkDefinition[] PerkPool =
    {
        new("blood_pact", "PERK_BLOOD_PACT_NAME", "PERK_BLOOD_PACT_DESC", PerkTier.Maldito, new Vector3(0.9f, 0.1f, 0.1f), "res://Assets/icons/abilities/death_aura.rtex", isUnique: true),
        new("crystal_monolith", "PERK_CRYSTAL_MONOLITH_NAME", "PERK_CRYSTAL_MONOLITH_DESC", PerkTier.Maldito, new Vector3(0.8f, 0.1f, 0.9f), "res://Assets/icons/abilities/magic_star.rtex", isUnique: true),
        new("unbound_greed", "PERK_UNBOUND_GREED_NAME", "PERK_UNBOUND_GREED_DESC", PerkTier.Maldito, new Vector3(1.0f, 0.6f, 0.0f), "res://Assets/icons/abilities/golden_fist.rtex", isUnique: true),
        new("unstable_vortex", "PERK_UNSTABLE_VORTEX_NAME", "PERK_UNSTABLE_VORTEX_DESC", PerkTier.Maldito, new Vector3(0.7f, 0.2f, 0.8f), "res://Assets/icons/abilities/purple_energy_ring.rtex", isUnique: true),
        new("demon_deal", "PERK_DEMON_DEAL_NAME", "PERK_DEMON_DEAL_DESC", PerkTier.Maldito, new Vector3(0.95f, 0.15f, 0.15f), "res://Assets/icons/abilities/skull_arrow.rtex", isUnique: true),
        new("blind_fury", "PERK_BLIND_FURY_NAME", "PERK_BLIND_FURY_DESC", PerkTier.Maldito, new Vector3(1.0f, 0.3f, 0.2f), "res://Assets/icons/abilities/powerful_fist.rtex", isUnique: true),

        new("chain_lightning", "PERK_CHAIN_LIGHTNING_NAME", "PERK_CHAIN_LIGHTNING_DESC", PerkTier.Epico, new Vector3(0.3f, 0.7f, 1.0f), "res://Assets/icons/abilities/storm_lightning.rtex", isUnique: true),
        new("explosive_arrows", "PERK_EXPLOSIVE_ARROWS_NAME", "PERK_EXPLOSIVE_ARROWS_DESC", PerkTier.Epico, new Vector3(1.0f, 0.5f, 0.1f), "res://Assets/icons/abilities/volcanic_earthshot.rtex", isUnique: true),
        new("frost_aura", "PERK_FROST_AURA_NAME", "PERK_FROST_AURA_DESC", PerkTier.Epico, new Vector3(0.4f, 0.9f, 1.0f), "res://Assets/icons/abilities/ice_spirit_spell.rtex", isUnique: true),
        new("divine_thorns", "PERK_DIVINE_THORNS_NAME", "PERK_DIVINE_THORNS_DESC", PerkTier.Epico, new Vector3(0.9f, 0.9f, 0.3f), "res://Assets/icons/abilities/holy_cross.rtex", isUnique: true),
        new("sniper_stance", "PERK_SNIPER_STANCE_NAME", "PERK_SNIPER_STANCE_DESC", PerkTier.Epico, new Vector3(0.2f, 0.8f, 0.4f), "res://Assets/icons/abilities/red_target_icon.rtex", isUnique: true),
        new("midas_touch", "PERK_MIDAS_TOUCH_NAME", "PERK_MIDAS_TOUCH_DESC", PerkTier.Epico, new Vector3(1.0f, 0.85f, 0.1f), "res://Assets/icons/abilities/golden_hammers.rtex", isUnique: true),

        new("lifesteal", "PERK_LIFESTEAL_NAME", "PERK_LIFESTEAL_DESC", PerkTier.Raro, new Vector3(0.9f, 0.2f, 0.4f), "res://Assets/icons/abilities/bat_heart_gem.rtex", isUnique: false, maxStacks: 5),
        new("reinforced_multishot", "PERK_REINFORCED_MULTISHOT_NAME", "PERK_REINFORCED_MULTISHOT_DESC", PerkTier.Raro, new Vector3(0.4f, 0.8f, 0.9f), "res://Assets/icons/abilities/star_projectile.rtex", isUnique: false, maxStacks: 5),
        new("royal_tribute", "PERK_ROYAL_TRIBUTE_NAME", "PERK_ROYAL_TRIBUTE_DESC", PerkTier.Raro, new Vector3(1.0f, 0.8f, 0.2f), "res://Assets/icons/abilities/winged_star_shield.rtex", isUnique: false, maxStacks: 5),
        new("piercing_shot", "PERK_PIERCING_SHOT_NAME", "PERK_PIERCING_SHOT_DESC", PerkTier.Raro, new Vector3(0.5f, 0.7f, 1.0f), "res://Assets/icons/abilities/dagger_down.rtex", isUnique: false, maxStacks: 5),
        new("sharp_precision", "PERK_SHARP_PRECISION_NAME", "PERK_SHARP_PRECISION_DESC", PerkTier.Raro, new Vector3(1.0f, 0.4f, 0.6f), "res://Assets/icons/abilities/magic_fist.rtex", isUnique: false, maxStacks: 5),

        new("arrow_sharpening", "PERK_ARROW_SHARPENING_NAME", "PERK_ARROW_SHARPENING_DESC", PerkTier.Normal, new Vector3(0.9f, 0.9f, 0.9f), "res://Assets/icons/abilities/magic_upgrade_arrow.rtex", isUnique: false, maxStacks: 5),
        new("reinforced_plates", "PERK_REINFORCED_PLATES_NAME", "PERK_REINFORCED_PLATES_DESC", PerkTier.Normal, new Vector3(0.8f, 0.8f, 0.85f), "res://Assets/icons/abilities/shield_rank_1.rtex", isUnique: false, maxStacks: 5),
        new("hawk_eye", "PERK_HAWK_EYE_NAME", "PERK_HAWK_EYE_DESC", PerkTier.Normal, new Vector3(0.7f, 1.0f, 0.7f), "res://Assets/icons/abilities/electric_eye.rtex", isUnique: false, maxStacks: 5),
        new("vigor_potion", "PERK_VIGOR_POTION_NAME", "PERK_VIGOR_POTION_DESC", PerkTier.Normal, new Vector3(0.6f, 1.0f, 0.6f), "res://Assets/icons/abilities/magic_healing_light.rtex", isUnique: false, maxStacks: 5)
    };

    private static readonly WaveConfig[] Waves =
    {
        new(new[] { "unit/zombie_soldier" }, TotalCount: 8, SpawnInterval: 0.35f, InterWaveDelay: 8.0f),
        new(new[] { "unit/zombie_soldier" }, TotalCount: 12, SpawnInterval: 0.35f, InterWaveDelay: 6.0f),
        new(new[] { "unit/zombie_soldier", "unit/forest_goblin" }, TotalCount: 12, SpawnInterval: 0.30f, InterWaveDelay: 6.0f),
        new(new[] { "unit/forest_goblin", "unit/zombie_soldier", "unit/forest_goblin" }, TotalCount: 16, SpawnInterval: 0.28f, InterWaveDelay: 6.0f),
        new(new[] { "unit/giant_rock_golem", "unit/zombie_soldier", "unit/forest_goblin" }, TotalCount: 15, SpawnInterval: 0.35f, InterWaveDelay: 7.0f,
            MiniBoss: new MiniBossInfo("MINI_BOSS_1", new Vector3(1f, 0.6f, 0.1f))),
        new(new[] { "unit/cyber_dragon", "unit/zombie_soldier" }, TotalCount: 14, SpawnInterval: 0.35f, InterWaveDelay: 6.0f),
        new(new[] { "unit/zombie_warrior", "unit/cyber_dragon" }, TotalCount: 16, SpawnInterval: 0.30f, InterWaveDelay: 6.0f),
        new(new[] { "unit/flame_drake", "unit/forest_goblin", "unit/zombie_warrior" }, TotalCount: 22, SpawnInterval: 0.30f, InterWaveDelay: 6.0f),
        new(new[] { "unit/dark_iron_ogre", "unit/cyber_dragon", "unit/flame_drake" }, TotalCount: 18, SpawnInterval: 0.30f, InterWaveDelay: 6.0f),
        new(new[] { "unit/mech_spider_monster", "unit/dark_iron_ogre", "unit/forest_goblin" }, TotalCount: 20, SpawnInterval: 0.30f, InterWaveDelay: 7.0f,
            MiniBoss: new MiniBossInfo("MINI_BOSS_2", new Vector3(1f, 0.3f, 0.1f))),
        new(new[] { "unit/zombie_warrior", "unit/flame_drake", "unit/cyber_dragon" }, TotalCount: 26, SpawnInterval: 0.30f, InterWaveDelay: 6.0f),
        new(new[] { "unit/dark_iron_ogre", "unit/forest_goblin", "unit/cyber_dragon" }, TotalCount: 28, SpawnInterval: 0.30f, InterWaveDelay: 6.0f),
        new(new[] { "unit/dark_iron_ogre", "unit/flame_drake", "unit/zombie_warrior" }, TotalCount: 32, SpawnInterval: 0.30f, InterWaveDelay: 6.0f),
        new(new[] { "unit/dark_iron_ogre", "unit/flame_drake", "unit/forest_goblin", "unit/zombie_warrior" }, TotalCount: 42, SpawnInterval: 0.22f, InterWaveDelay: 6.0f),
        new(new[] { "unit/dragon_titan_boss" }, TotalCount: 1, SpawnInterval: 1.00f, InterWaveDelay: 8.0f, IsBoss: true)
    };

    private sealed class PlayerState
    {
        public int PlayerIndex { get; }
        public UnitId Hero = UnitId.Invalid;
        public Vector3 QuadrantCenter { get; set; }
        public bool IsDefeated { get; set; }

        public readonly Dictionary<string, int> PerkStacks = new(32);
        public readonly PerkDefinition?[] CurrentDraft = new PerkDefinition?[3];
        public readonly bool[] SlotSold = new bool[3];
        public float ShopRotationTimer = 40.0f;
        public bool ShopWarningNotified;
        public int PerksPicked;
        public int RerollCount;
        public float FrostAuraTimer;
        public float HeroAuraTimer;
        public float PassiveGoldAccumulator;
        public float LifestealTextTimer;
        public float TotalPlayTime;
        public readonly Dictionary<int, float> ThornsCooldowns = new();

        public bool HasBloodPact;
        public bool HasCrystalMonolith;
        public bool HasUnboundGreed;
        public bool HasUnstableVortex;
        public bool HasBlindFury;
        public bool HasChainLightning;
        public bool HasExplosiveArrows;
        public bool HasFrostAura;
        public bool HasDivineThorns;
        public bool HasSniperStance;
        public bool HasMidasTouch;
        public bool HasPiercingShot;
        public int DemonDealExtraWavesRemaining;

        public int CurrentWave;
        public int WaveBeingSpawned;
        public int RemainingToSpawn;
        public int SpawnedInWave;
        public int AliveInWave;
        public float SpawnTimer;
        public float SpawnInterval;
        public bool Spawning;
        public int TotalKills;
        public float TotalDamageDealt;
        public float TotalGoldEarned;
        public float CurrentIncomePerSecond = BasePassiveGoldPerSecond;

        public UnitId RaidBossInstance = UnitId.Invalid;
        public bool BossEnraged;
        public float BossEscortTimer;
        public float BossIndicatorTimer;
        public bool BossDefeated;

        public PlayerState(int playerIndex)
        {
            PlayerIndex = playerIndex;
            QuadrantCenter = Coordinates.GetQuadrantCenter(playerIndex);
        }

        public int GetPerkStackCount(string perkId)
        {
            return PerkStacks.TryGetValue(perkId, out int count) ? count : 0;
        }
    }

    private IGameAPI _api = null!;
    private readonly Dictionary<int, PlayerState> _players = new();
    private readonly Dictionary<int, int> _unitQuadrantMap = new();
    private bool _gameOver;
    private int _globalCurrentWave;
    private bool _globalWaveSpawning;
    private float _globalInterWaveTimer;
    private float _globalWaveDuration;
    private const float MaxWaveDurationSeconds = 75.0f;
    private float _economyScanTimer;
    private const float WatchdogScanInterval = 2.0f;
    private float _watchdogScanTimer;
    private const string FallbackPerkIcon = "res://Assets/icons/abilities/magic_catalyst.rtex";
    private const int EnemyProxyPlayerOffset = 12;

    private static int GetEnemyPlayerIndex(int playerIndex) => playerIndex + EnemyProxyPlayerOffset;

    private bool IsEnemy(UnitId unit, int playerIndex) => unit.IsValid && !_api.ArePlayersAllied(unit.Player, playerIndex);

    private bool AreEnemies(UnitId unitA, UnitId unitB) => unitA.IsValid && unitB.IsValid && !_api.ArePlayersAllied(unitA.Player, unitB.Player);

    public void Initialize(IGameAPI api)
    {
        _api = api;
        _gameOver = false;
        _globalCurrentWave = 0;
        _globalWaveSpawning = false;
        _globalInterWaveTimer = 0f;
        _globalWaveDuration = 0f;
        _economyScanTimer = 0f;
        _watchdogScanTimer = 0f;
        _players.Clear();
        _unitQuadrantMap.Clear();
        _api.SetSummaryTableHeaders("PLAYER", "DAMAGE", "INCOME", "SCORE");

        const int MaxSurvivors = 8;
        var participants = _api.GetActivePlayerIndices(EnemyProxyPlayerOffset)
            .Where(i => i > 0)
            .ToList();

        if (participants.Count == 0)
        {
            participants.Add(1);
        }

        if (participants.Count > MaxSurvivors)
        {
            foreach (int slot in participants)
            {
                _api.ShowPlayerFeedbackText(slot, _api.Translate("TOO_MANY_PLAYERS", slot), new Vector3(1f, 0.3f, 0.3f));
            }
            return;
        }

        var activePlayerIndices = participants;
        int activeCount = activePlayerIndices.Count;

        foreach (var pIdx in activePlayerIndices)
        {
            _api.SetPlayerTeam(pIdx, 0);
            int enemySlot = GetEnemyPlayerIndex(pIdx);
            _api.SetPlayerActive(enemySlot, true);
            _api.SetPlayerComputerControlled(enemySlot, true);
            _api.SetPlayerTeam(enemySlot, 1);
            foreach (var otherPlayer in activePlayerIndices)
            {
                _api.SetPlayersAllied(otherPlayer, enemySlot, false);
            }
        }

        for (int i = 0; i < activePlayerIndices.Count; i++)
        {
            for (int j = i + 1; j < activePlayerIndices.Count; j++)
            {
                _api.SetPlayersAllied(activePlayerIndices[i], activePlayerIndices[j], true);
            }
        }

        for (int slotIdx = 0; slotIdx < activePlayerIndices.Count; slotIdx++)
        {
            int pIdx = activePlayerIndices[slotIdx];
            var pState = new PlayerState(pIdx);
            int quadIndex = (activeCount == 2 && slotIdx == 1) ? 4 : (slotIdx % 8);
            pState.QuadrantCenter = Coordinates.GetQuadrantCenter(quadIndex);
            _players[pIdx] = pState;

            _api.SetPlayerGold(pIdx, InitialGold);
            SpawnSurvivorHero(pState);

            pState.SlotSold[0] = false;
            pState.SlotSold[1] = false;
            pState.SlotSold[2] = false;
            pState.ShopRotationTimer = ShopRotationInterval;
            pState.ShopWarningNotified = false;
            TriggerNewShopRotation(pState, resetTimer: true, isAutomatic: true);
        }

        ShowGlobalIntermission(1);

        foreach (var pState in _players.Values)
        {
            _api.PanPlayerCameraTo(pState.PlayerIndex, pState.QuadrantCenter, 0.0f);
        }

        _api.OnUnitDied += OnUnitDied;
        _api.OnUnitDamaged += OnUnitDamaged;
        _api.OnPlayerChatMessage += OnPlayerChatMessage;
        _api.OnSpellCast += OnSpellCast;

        _api.SetLeaderboardVisible("TOWER SURVIVORS", true);
        UpdateLeaderboardDisplay();

        int firstPlayer = activePlayerIndices[0];
        _api.BroadcastMessage(_api.Translate("WELCOME_MESSAGE", firstPlayer));
    }

    public void Update(IGameAPI api, float delta)
    {
        if (_gameOver) return;

        bool anyAlive = false;
        foreach (var pState in _players.Values)
        {
            if (pState.IsDefeated) continue;

            CheckDefeatCondition(pState);
            if (pState.IsDefeated) continue;

            anyAlive = true;

            // 1. Advance timers first (single decrement per player)
            pState.ShopRotationTimer -= delta;

            // 2. Evaluate timer thresholds
            UpdateShopRotationTimer(pState);

            if (_globalWaveSpawning && pState.RemainingToSpawn > 0)
            {
                UpdateSpawning(pState, delta);
            }

            // 3. Subsystem execution with defensive isolation
            try
            {
                UpdateHeroTickEffects(pState, delta);
            }
            catch (Exception ex)
            {
                _api.BroadcastMessage($"[GUEST ERROR HeroTick] {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                UpdatePassiveIncome(pState, delta);
            }
            catch (Exception ex)
            {
                _api.BroadcastMessage($"[GUEST ERROR Income] {ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                UpdateBossEncounterIfActive(pState, delta);
            }
            catch (Exception ex)
            {
                _api.BroadcastMessage($"[GUEST ERROR Boss] {ex.GetType().Name}: {ex.Message}");
            }
        }

        UpdateGlobalWaveStateMachine(delta);

        if (!anyAlive && _players.Count > 0)
        {
            _gameOver = true;
            _api.StopCountdownTimer();
            _api.ShowFeedbackText(_api.Translate("HERO_DEFEATED", 0), new Vector3(1f, 0.2f, 0.2f));
            _api.TriggerDefeat();
            return;
        }

        try
        {
            UpdateEconomyAndUI(delta);
        }
        catch (Exception ex)
        {
            _api.BroadcastMessage($"[GUEST ERROR EconomyUI] {ex.GetType().Name}: {ex.Message}");
        }

        _watchdogScanTimer -= delta;
        if (_watchdogScanTimer <= 0f)
        {
            _watchdogScanTimer = WatchdogScanInterval;
            try
            {
                UpdateEnemyAntiSoftlockWatchdog();
            }
            catch (Exception ex)
            {
                _api.BroadcastMessage($"[GUEST ERROR Watchdog] {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private void UpdateEnemyAntiSoftlockWatchdog()
    {
        if (_unitQuadrantMap.Count == 0) return;

        foreach (var unit in _api.GetAllUnits())
        {
            if (!unit.IsValid || unit.IsDead) continue;
            if (!_unitQuadrantMap.TryGetValue(unit.UniqueId, out int playerIndex)) continue;
            if (!IsEnemy(unit, playerIndex)) continue;
            if (!_players.TryGetValue(playerIndex, out var pState)) continue;
            if (pState.IsDefeated) continue;

            float dist = Vector3.Distance(new Vector3(unit.Position.X, 0f, unit.Position.Z), new Vector3(pState.QuadrantCenter.X, 0f, pState.QuadrantCenter.Z));
            if (dist > 41.0f)
            {
                Vector3 dirFromCenter = unit.Position - pState.QuadrantCenter;
                dirFromCenter.Y = 0f;
                if (dirFromCenter.LengthSquared() > 0.001f)
                {
                    dirFromCenter = Vector3.Normalize(dirFromCenter);
                }
                else
                {
                    dirFromCenter = new Vector3(0f, 0f, 1f);
                }

                Vector3 safePos = new Vector3(pState.QuadrantCenter.X + (dirFromCenter.X * 24.0f), 3.0f, pState.QuadrantCenter.Z + (dirFromCenter.Z * 24.0f));
                unit.Teleport(safePos);
                unit.AttackMove(pState.QuadrantCenter);
            }
        }
    }

    private void SpawnSurvivorHero(PlayerState pState)
    {
        UnitId existingHero = UnitId.Invalid;
        foreach (var unit in _api.GetAllUnits())
        {
            if ((unit.TemplateID == "unit/survivor_hero" || unit.TemplateID == "survivor_hero") && !unit.IsDead)
            {
                if (Vector3.Distance(unit.Position, pState.QuadrantCenter) < 15f)
                {
                    existingHero = unit;
                    break;
                }
            }
        }

        if (existingHero.IsValid)
        {
            pState.Hero = existingHero;
            _api.SetUnitOwner(pState.Hero, pState.PlayerIndex);
        }
        else
        {
            pState.Hero = _api.SpawnUnitForPlayer("unit/survivor_hero", new Vector2(pState.QuadrantCenter.X, pState.QuadrantCenter.Z), pState.PlayerIndex, false);
        }

        if (!pState.Hero.IsValid) return;

        var hero = pState.Hero;
        hero.Speed = 0f;

        _api.SetUnitColor(pState.Hero, new Vector3(1f, 1f, 1f));
        RecalculateHeroStats(pState);

        _api.SelectPlayerUnit(pState.PlayerIndex, pState.Hero);
    }

    private void CheckDefeatCondition(PlayerState pState)
    {
        if (pState.IsDefeated) return;
        if (pState.Hero.IsValid && !pState.Hero.IsDead && pState.Hero.Health > 0f) return;

        pState.IsDefeated = true;
        string defeatMsg = _players.Count <= 1
            ? _api.Translate("HERO_DEFEATED", pState.PlayerIndex)
            : $"P{pState.PlayerIndex}: {_api.Translate("HERO_DEFEATED", pState.PlayerIndex)}";
        _api.SendMessageToPlayer(pState.PlayerIndex, defeatMsg);
        _api.BroadcastMessage(defeatMsg);

        CleanupDefeatedQuadrant(pState);
    }

    private void CleanupDefeatedQuadrant(PlayerState pState)
    {
        var toKill = new List<UnitId>();
        foreach (var unit in _api.GetAllUnits())
        {
            if (unit.IsValid && !unit.IsDead && _unitQuadrantMap.TryGetValue(unit.UniqueId, out int owner) && owner == pState.PlayerIndex)
            {
                toKill.Add(unit);
            }
        }

        foreach (var unit in toKill)
        {
            _api.KillUnit(unit, executeDespawnShader: false, playDeathAnimation: false);
        }

        pState.AliveInWave = 0;
        pState.RemainingToSpawn = 0;
        pState.Spawning = false;
        pState.RaidBossInstance = UnitId.Invalid;

        var remainingIds = new List<int>();
        foreach (var pair in _unitQuadrantMap)
        {
            if (pair.Value == pState.PlayerIndex)
            {
                remainingIds.Add(pair.Key);
            }
        }

        foreach (int id in remainingIds)
        {
            _unitQuadrantMap.Remove(id);
        }
    }

    private void UpdateHeroTickEffects(PlayerState pState, float delta)
    {
        var hero = pState.Hero;
        if (!hero.IsValid || hero.IsDead) return;


        pState.TotalPlayTime += delta;
        if (pState.LifestealTextTimer > 0f) pState.LifestealTextTimer -= delta;

        if (pState.HasBloodPact)
        {
            float drain = MathF.Max(1.0f, hero.Health * 0.02f * delta);
            hero.Health = MathF.Max(1.0f, hero.Health - drain);
        }

        if (pState.HasFrostAura)
        {
            pState.FrostAuraTimer -= delta;
            if (pState.FrostAuraTimer <= 0f)
            {
                pState.FrostAuraTimer = 4.0f;
                _api.SpawnVisualEffect("holylight", hero.Position, 2.5f);
                _api.CreateFloatingText("¡ESCARCHA!", hero.Position + new Vector3(0, 2.5f, 0), new Vector3(0.4f, 0.9f, 1.0f), 0.9f);
                foreach (var enemy in _api.GetUnitsInRadius(hero.Position, 20f))
                {
                    if (enemy.IsValid && IsEnemy(enemy, pState.PlayerIndex) && !enemy.IsDead && enemy.Health > 0f)
                    {
                        enemy.Speed = MathF.Max(1.5f, enemy.Speed * 0.60f);
                    }
                }
            }
        }

        if (pState.HasDivineThorns)
        {
            pState.HeroAuraTimer -= delta;
            if (pState.HeroAuraTimer <= 0f)
            {
                pState.HeroAuraTimer = 2.5f;
                _api.SpawnVisualEffect("holylight", hero.Position + new Vector3(0f, 1f, 0f), 1.2f);
            }
        }
    }

    private void UpdatePassiveIncome(PlayerState pState, float delta)
    {
        float incomeDelta = delta * pState.CurrentIncomePerSecond;
        pState.PassiveGoldAccumulator += incomeDelta;
        if (pState.PassiveGoldAccumulator >= 1.0f)
        {
            float toAward = MathF.Floor(pState.PassiveGoldAccumulator);
            pState.PassiveGoldAccumulator -= toAward;
            _api.AdjustPlayerGold(pState.PlayerIndex, toAward);
            pState.TotalGoldEarned += toAward;
        }
    }

    private void UpdateEconomyAndUI(float delta)
    {
        _economyScanTimer -= delta;
        if (_economyScanTimer > 0f) return;

        _economyScanTimer = 1.0f;
        foreach (var pState in _players.Values)
        {
            int royalTributeStacks = pState.GetPerkStackCount("royal_tribute");
            pState.CurrentIncomePerSecond = BasePassiveGoldPerSecond + (royalTributeStacks * 10.0f);
        }
        UpdateLeaderboardDisplay();
    }

    private void UpdateShopRotationTimer(PlayerState pState)
    {

        if (pState.ShopRotationTimer <= 5.0f && !pState.ShopWarningNotified)
        {
            pState.ShopWarningNotified = true;
            _api.ShowPlayerFeedbackText(pState.PlayerIndex, _api.Translate("SHOP_ROTATING_SOON", pState.PlayerIndex), new Vector3(1f, 0.75f, 0.2f));
            if (pState.Hero.IsValid)
            {
                _api.CreateFloatingText(_api.Translate("SHOP_ROTATING_SOON", pState.PlayerIndex), pState.Hero.Position + new Vector3(0, 2.5f, 0), new Vector3(1f, 0.75f, 0.2f), 1.2f);
            }
        }

        if (pState.ShopRotationTimer <= 0f)
        {
            TriggerNewShopRotation(pState, resetTimer: true, isAutomatic: true);
        }
    }

    private float GetPerkCost(PerkDefinition perk) => perk.Tier switch
    {
        PerkTier.Normal => 100f,
        PerkTier.Raro => 200f,
        PerkTier.Epico => 350f,
        PerkTier.Maldito => 100f,
        _ => 100f
    };

    private void SendPlayerShopSummary(PlayerState pState)
    {
        string p0 = pState.CurrentDraft[0] != null ? $"{_api.Translate(pState.CurrentDraft[0]!.NameKey, pState.PlayerIndex)} ({GetPerkCost(pState.CurrentDraft[0]!):F0}g)" : "[Agotado]";
        string p1 = pState.CurrentDraft[1] != null ? $"{_api.Translate(pState.CurrentDraft[1]!.NameKey, pState.PlayerIndex)} ({GetPerkCost(pState.CurrentDraft[1]!):F0}g)" : "[Agotado]";
        string p2 = pState.CurrentDraft[2] != null ? $"{_api.Translate(pState.CurrentDraft[2]!.NameKey, pState.PlayerIndex)} ({GetPerkCost(pState.CurrentDraft[2]!):F0}g)" : "[Agotado]";
        float rerollCost = pState.RerollCount == 0 ? 0f : ShopRerollCost;
        string msg = $"[Tienda P{pState.PlayerIndex}] 1: {p0} | 2: {p1} | 3: {p2} (Reroll: {rerollCost:F0}g)";
        _api.SendMessageToPlayer(pState.PlayerIndex, msg);
    }

    private void TriggerNewShopRotation(PlayerState pState, bool resetTimer = true, bool isAutomatic = false)
    {
        if (resetTimer)
        {
            pState.ShopRotationTimer = ShopRotationInterval;
            pState.ShopWarningNotified = false;
        }

        if (isAutomatic)
        {
            pState.RerollCount = 0;
        }

        pState.SlotSold[0] = false;
        pState.SlotSold[1] = false;
        pState.SlotSold[2] = false;

        pState.CurrentDraft[0] = null;
        pState.CurrentDraft[1] = null;
        pState.CurrentDraft[2] = null;

        for (int slot = 0; slot < 3; slot++)
        {
            pState.CurrentDraft[slot] = RollSinglePerk(pState);
        }

        RegisterPlayerShopAbilities(pState);

        if (_players.Count <= 1)
        {
            _api.PlayPlayerClickSound(pState.PlayerIndex);
            _api.ShowFeedbackText(_api.Translate("SHOP_READY", pState.PlayerIndex), new Vector3(1f, 0.9f, 0.2f));
        }
        else
        {
            SendPlayerShopSummary(pState);
            _api.SendMessageToPlayer(pState.PlayerIndex, _api.Translate("SHOP_READY", pState.PlayerIndex));
            _api.PlayPlayerClickSound(pState.PlayerIndex);
            _api.ShowPlayerFeedbackText(pState.PlayerIndex, _api.Translate("SHOP_READY", pState.PlayerIndex), new Vector3(1f, 0.9f, 0.2f));
        }

        if (pState.Hero.IsValid)
        {
            _api.CreateFloatingText(_api.Translate("SHOP_FLOATING", pState.PlayerIndex), pState.Hero.Position + new Vector3(0, 2.8f, 0), new Vector3(1f, 0.9f, 0.2f), 1.5f);
        }

        UpdateLeaderboardDisplay();
    }

    private void RegisterPlayerShopAbilities(PlayerState pState)
    {
        for (int slot = 0; slot < 3; slot++)
        {
            var perk = pState.CurrentDraft[slot];
            if (perk == null) continue;

            float cost = GetPerkCost(perk);
            string key = slot switch { 0 => "Q", 1 => "W", _ => "E" };
            string abilityId = $"perk_choose_{slot + 1}";
            string translatedName = _api.Translate(perk.NameKey, pState.PlayerIndex);
            string translatedDesc = _api.Translate(perk.DescKey, pState.PlayerIndex);
            string name = $"[{key}] {translatedName} ({cost:F0}g)";
            string tooltip = $"[{key}] {translatedName} — Cost: {cost:F0}g\n{translatedDesc}";
            string icon = string.IsNullOrEmpty(perk.IconPath) ? FallbackPerkIcon : perk.IconPath;

            _api.RegisterPlayerAbility(
                pState.PlayerIndex,
                abilityId,
                name,
                tooltip,
                icon,
                isInstant: true);
        }

        float rerollCost = pState.RerollCount == 0 ? 0f : ShopRerollCost;
        string rerollName = pState.RerollCount == 0
            ? _api.Translate("REROLL_FREE", pState.PlayerIndex)
            : string.Format(_api.Translate("REROLL_COST", pState.PlayerIndex), rerollCost);
        string rerollTooltip = string.Format(_api.Translate("REROLL_TOOLTIP", pState.PlayerIndex), rerollCost);

        _api.RegisterPlayerAbility(
            pState.PlayerIndex,
            "perk_reroll",
            rerollName,
            rerollTooltip,
            "res://Assets/icons/abilities/magic_catalyst.rtex",
            isInstant: true);

        string meteorName = _api.Translate("METEOR_SPELL_NAME", pState.PlayerIndex);
        string meteorTooltip = _api.Translate("METEOR_SPELL_TOOLTIP", pState.PlayerIndex);

        _api.RegisterPlayerAbility(
            pState.PlayerIndex,
            "hero_meteor_spell",
            meteorName,
            meteorTooltip,
            "res://Assets/icons/abilities/meteor_strike.rtex",
            isInstant: false);
    }

    private PerkDefinition RollSinglePerk(PlayerState pState)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int roll = _api.RandomInt(0, 99);
            PerkTier targetTier = roll switch
            {
                < 45 => PerkTier.Normal,
                < 80 => PerkTier.Raro,
                < 95 => PerkTier.Epico,
                _ => PerkTier.Maldito
            };

            var candidates = PerkPool.Where(p => p.Tier == targetTier && CanPickPerk(pState, p) && !pState.CurrentDraft.Contains(p)).ToArray();
            if (candidates.Length > 0)
            {
                int index = _api.RandomInt(0, candidates.Length - 1);
                return candidates[index];
            }
        }

        var anyAvailable = PerkPool.Where(p => CanPickPerk(pState, p) && !pState.CurrentDraft.Contains(p)).ToArray();
        if (anyAvailable.Length > 0)
        {
            return anyAvailable[_api.RandomInt(0, anyAvailable.Length - 1)];
        }

        return PerkPool[0];
    }

    private bool CanPickPerk(PlayerState pState, PerkDefinition perk)
    {
        int stacks = pState.GetPerkStackCount(perk.Id);
        return stacks < perk.MaxStacks;
    }

    private void TryBuyPerk(PlayerState pState, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 3) return;
        if (pState.SlotSold[slotIndex])
        {
            _api.SendMessageToPlayer(pState.PlayerIndex, _api.Translate("PERK_SLOT_EMPTY", pState.PlayerIndex));
            if (pState.Hero.IsValid)
            {
                _api.CreateFloatingText("! [SOLD]", pState.Hero.Position + new Vector3(0, 2f, 0), new Vector3(1f, 0.5f, 0.2f), 0.9f);
            }
            return;
        }

        var perk = pState.CurrentDraft[slotIndex];
        if (perk == null) return;

        if (!CanPickPerk(pState, perk))
        {
            _api.SendMessageToPlayer(pState.PlayerIndex, _api.Translate("PERK_MAX_STACKS", pState.PlayerIndex));
            if (pState.Hero.IsValid)
            {
                _api.CreateFloatingText("! [MAX]", pState.Hero.Position + new Vector3(0, 2f, 0), new Vector3(1f, 0.4f, 0.2f), 0.9f);
            }
            return;
        }

        float cost = GetPerkCost(perk);
        float currentGold = _api.GetPlayer(pState.PlayerIndex).Gold;
        if (currentGold < cost)
        {
            string notEnough = string.Format(_api.Translate("PERK_NOT_ENOUGH_GOLD", pState.PlayerIndex), cost, currentGold);
            _api.SendMessageToPlayer(pState.PlayerIndex, notEnough);
            if (pState.Hero.IsValid)
            {
                _api.CreateFloatingText($"! -{(cost - currentGold):F0}g", pState.Hero.Position + new Vector3(0, 2f, 0), new Vector3(1f, 0.2f, 0.2f), 0.9f);
            }
            return;
        }

        _api.AdjustPlayerGold(pState.PlayerIndex, -cost);
        pState.SlotSold[slotIndex] = true;

        int current = pState.GetPerkStackCount(perk.Id);
        pState.PerkStacks[perk.Id] = current + 1;
        pState.PerksPicked++;

        if (_players.Count <= 1)
        {
            string soldLabel = _api.Translate("PERK_SLOT_SOLD_LABEL", pState.PlayerIndex);
            string boughtId = $"perk_choose_{slotIndex + 1}";
            string perkName = _api.Translate(perk.NameKey, pState.PlayerIndex);
            _api.RegisterAbility(boughtId, $"{soldLabel} {perkName}", $"{soldLabel} {perkName} (x{pState.PerkStacks[perk.Id]})", perk.IconPath, isInstant: true);
        }

                float prevMaxHp = pState.Hero.IsValid ? pState.Hero.MaxHealth : 0f;
        float prevDamage = pState.Hero.IsValid ? pState.Hero.Damage : 0f;
        float prevArmor = pState.Hero.IsValid ? pState.Hero.Armor : 0f;
        float prevRange = pState.Hero.IsValid ? pState.Hero.Range : 0f;

        ApplyPerkAcquisition(pState, perk);
        RecalculateHeroStats(pState);

        float dHp = pState.Hero.IsValid ? pState.Hero.MaxHealth - prevMaxHp : 0f;
        float dDmg = pState.Hero.IsValid ? pState.Hero.Damage - prevDamage : 0f;
        float dArm = pState.Hero.IsValid ? pState.Hero.Armor - prevArmor : 0f;
        float dRng = pState.Hero.IsValid ? pState.Hero.Range - prevRange : 0f;

        string deltaMsg = "";
        if (dHp > 0.5f) deltaMsg = $"HP max {pState.Hero.MaxHealth:F0} (+{dHp:F0})";
        else if (dHp < -0.5f) deltaMsg = $"HP max {pState.Hero.MaxHealth:F0} ({dHp:F0})";
        else if (dDmg > 0.5f) deltaMsg = $"ATK {pState.Hero.Damage:F0} (+{dDmg:F0})";
        else if (dArm > 0.5f) deltaMsg = $"ARM {pState.Hero.Armor:F0} (+{dArm:F0})";
        else if (dArm < -0.5f) deltaMsg = $"ARM {pState.Hero.Armor:F0} ({dArm:F0})";
        else if (dRng > 0.5f) deltaMsg = $"RNG {pState.Hero.Range:F0} (+{dRng:F0})";
        else if (dRng < -0.5f) deltaMsg = $"RNG {pState.Hero.Range:F0} ({dRng:F0})";
        else
        {
            int stacks = pState.GetPerkStackCount(perk.Id);
            deltaMsg = perk.Id switch
            {
                "sharp_precision" => $"CRIT {(stacks * 20):F0}% (+20%)",
                "lifesteal" => $"LS {(stacks * 15):F0}% (+15%)",
                "reinforced_multishot" => $"Multi x{stacks}",
                "royal_tribute" => $"+{(stacks * 10):F0}g/s",
                "piercing_shot" => $"Pierce {(stacks * 70):F0}%",
                _ => "Efecto activo"
            };
        }

        string localizedPerkName = _api.Translate(perk.NameKey, pState.PlayerIndex);
        int currentRank = pState.GetPerkStackCount(perk.Id);
        string contractMsg = $"[P{pState.PlayerIndex}] {localizedPerkName} x{currentRank} comprado. {deltaMsg}.";
        _api.SendMessageToPlayer(pState.PlayerIndex, contractMsg);
        _api.PlayPlayerClickSound(pState.PlayerIndex);
        if (_players.Count > 1)
        {
            _api.BroadcastMessage(contractMsg);
        }

        if (pState.Hero.IsValid)
        {
            _api.CreateFloatingText($"+{localizedPerkName} (x{pState.PerkStacks[perk.Id]})", pState.Hero.Position + new Vector3(0, 2.5f, 0), perk.Color, 1.4f);
            _api.SpawnVisualEffect("holylight", pState.Hero.Position + new Vector3(0, 1.2f, 0), 1.3f);
        }

        UpdateLeaderboardDisplay();

        if (pState.SlotSold[0] && pState.SlotSold[1] && pState.SlotSold[2])
        {
            TriggerNewShopRotation(pState, resetTimer: true, isAutomatic: true);
        }
    }

    private void ApplyPerkAcquisition(PlayerState pState, PerkDefinition perk)
    {
        switch (perk.Id)
        {
            case "blood_pact":
                pState.HasBloodPact = true;
                break;
            case "crystal_monolith":
                pState.HasCrystalMonolith = true;
                break;
            case "unbound_greed":
                pState.HasUnboundGreed = true;
                break;
            case "unstable_vortex":
                pState.HasUnstableVortex = true;
                break;
            case "demon_deal":
                pState.DemonDealExtraWavesRemaining = 2;
                _api.AdjustPlayerGold(pState.PlayerIndex, 1000f);
                pState.TotalGoldEarned += 1000f;
                if (pState.Hero.IsValid) { var h = pState.Hero; h.Health = h.MaxHealth; }
                _api.ShowFeedbackText(_api.Translate("DEMON_DEAL_TRIGGER", pState.PlayerIndex), new Vector3(0.95f, 0.15f, 0.15f));
                break;
            case "blind_fury":
                pState.HasBlindFury = true;
                break;
            case "chain_lightning":
                pState.HasChainLightning = true;
                break;
            case "explosive_arrows":
                pState.HasExplosiveArrows = true;
                break;
            case "frost_aura":
                pState.HasFrostAura = true;
                break;
            case "divine_thorns":
                pState.HasDivineThorns = true;
                break;
            case "sniper_stance":
                pState.HasSniperStance = true;
                break;
            case "midas_touch":
                pState.HasMidasTouch = true;
                break;
            case "piercing_shot":
                pState.HasPiercingShot = true;
                break;
            case "vigor_potion":
                if (pState.Hero.IsValid) { var h = pState.Hero; h.Health = MathF.Min(h.MaxHealth, h.Health + 200f); }
                break;
        }
    }

    private void TryRerollShop(PlayerState pState)
    {
        float cost = pState.RerollCount == 0 ? 0f : ShopRerollCost;
        if (cost > 0f && _api.GetPlayer(pState.PlayerIndex).Gold < cost)
        {
            string notEnough = string.Format(_api.Translate("PERK_NOT_ENOUGH_GOLD", pState.PlayerIndex), cost, _api.GetPlayer(pState.PlayerIndex).Gold);
            _api.SendMessageToPlayer(pState.PlayerIndex, notEnough);
            if (pState.Hero.IsValid)
            {
                _api.CreateFloatingText($"! -{(cost - _api.GetPlayer(pState.PlayerIndex).Gold):F0}g", pState.Hero.Position + new Vector3(0, 2f, 0), new Vector3(1f, 0.2f, 0.2f), 0.9f);
            }
            return;
        }

        if (cost > 0f)
        {
            _api.AdjustPlayerGold(pState.PlayerIndex, -cost);
        }

        pState.RerollCount++;
        string rerollMsg = string.Format(_api.Translate("REROLL_SUCCESS", pState.PlayerIndex), ShopRerollCost);
        _api.SendMessageToPlayer(pState.PlayerIndex, rerollMsg);
        _api.PlayPlayerClickSound(pState.PlayerIndex);
        if (_players.Count > 1)
        {
            _api.BroadcastMessage($"P{pState.PlayerIndex}: Reroll de tienda completado.");
        }

        TriggerNewShopRotation(pState, resetTimer: false, isAutomatic: false);
    }

    private string GetHeroProjectileId(PlayerState pState)
    {
        if (pState.HasExplosiveArrows) return "weapon/fire_shard";
        if (pState.HasFrostAura) return "weapon/frost_shard";
        return "weapon/survivor_arrow";
    }

    private void RecalculateHeroStats(PlayerState pState)
    {
        var hero = pState.Hero;
        if (!hero.IsValid || hero.IsDead) return;

        int vigorStacks = pState.GetPerkStackCount("vigor_potion");
        float bonusMaxHp = vigorStacks * 350.0f;
        float hpMultiplier = pState.HasCrystalMonolith ? 0.60f : 1.0f;
        float prevMaxHp = hero.MaxHealth;
        float calculatedMaxHp = MathF.Max(150.0f, (BaseHeroHp + bonusMaxHp) * hpMultiplier);

        hero.MaxHealth = calculatedMaxHp;
        if (calculatedMaxHp > prevMaxHp && prevMaxHp > 0f)
        {
            hero.Health += (calculatedMaxHp - prevMaxHp);
        }
        hero.Health = MathF.Min(hero.Health, hero.MaxHealth);

        int arrowSharpenStacks = pState.GetPerkStackCount("arrow_sharpening");
        float flatBonusDamage = arrowSharpenStacks * 12.0f;
        float damageMult = pState.HasBloodPact ? 2.0f : 1.0f;
        hero.Damage = (BaseHeroDamage + flatBonusDamage) * damageMult;

        int hawkEyeStacks = pState.GetPerkStackCount("hawk_eye");
        float flatBonusRange = (hawkEyeStacks * 6.0f) + (pState.HasCrystalMonolith ? 15.0f : 0f) + (pState.HasSniperStance ? 18.0f : 0f);
        float rangeMult = pState.HasBlindFury ? 0.65f : 1.0f;
        hero.Range = MathF.Max(15.0f, (BaseHeroRange + flatBonusRange) * rangeMult);

        if (pState.HasUnstableVortex)
        {
            hero.Armor = 0f;
        }
        else
        {
            int plateStacks = pState.GetPerkStackCount("reinforced_plates");
            hero.Armor = BaseHeroArmor + (plateStacks * 4.0f) + (pState.HasDivineThorns ? 15.0f : 0f);
        }
    }

    private void UpdateGlobalWaveStateMachine(float delta)
    {
        if (_globalWaveSpawning)
        {
            _globalWaveDuration += delta;

            bool allCleared = true;
            bool anyUndefeated = false;

            foreach (var p in _players.Values)
            {
                if (p.IsDefeated) continue;
                anyUndefeated = true;

                if (p.RemainingToSpawn > 0 || p.AliveInWave > 0)
                {
                    allCleared = false;
                    break;
                }
            }

            if (!anyUndefeated) return;

            if (_globalCurrentWave == 15)
            {
                bool bossAlive = false;
                foreach (var p in _players.Values)
                {
                    if (!p.IsDefeated && !p.BossDefeated && p.RaidBossInstance.IsValid)
                    {
                        bossAlive = true;
                        break;
                    }
                }
                if (bossAlive) return;
            }

            if (allCleared || _globalWaveDuration >= MaxWaveDurationSeconds)
            {
                EndGlobalWave();
            }
        }
        else
        {
            _globalInterWaveTimer -= delta;
            if (_globalInterWaveTimer <= 0f)
            {
                BeginGlobalWave(_globalCurrentWave + 1);
            }
        }
    }

    private void BeginGlobalWave(int wave)
    {
        if (wave > TotalWaves)
        {
            EndGameWithVictory();
            return;
        }

        _globalCurrentWave = wave;
        _globalWaveSpawning = true;
        _globalWaveDuration = 0f;
        _api.StopCountdownTimer();

        var config = Waves[wave - 1];

        if (config.IsBoss)
        {
            foreach (var pState in _players.Values)
            {
                if (!pState.IsDefeated) TriggerRaidBossCinematic(pState);
            }
        }
        else if (config.MiniBoss is { } miniBoss)
        {
            string banner = _api.Translate(miniBoss.BannerKey, 0);
            _api.ShowFeedbackText(banner, miniBoss.Color);
        }
        else
        {
            string waveMsg = string.Format(_api.Translate("WAVE_INCOMING", 0), wave);
            _api.BroadcastMessage(waveMsg);
        }

        foreach (var pState in _players.Values)
        {
            if (pState.IsDefeated) continue;

            pState.CurrentWave = wave;
            pState.WaveBeingSpawned = wave;
            pState.Spawning = true;

            int extraCount = 0;
            if (pState.DemonDealExtraWavesRemaining > 0)
            {
                extraCount = (int)MathF.Ceiling(config.TotalCount * 0.35f);
                pState.DemonDealExtraWavesRemaining--;
            }

            pState.RemainingToSpawn = config.TotalCount + extraCount;
            pState.SpawnedInWave = 0;
            pState.AliveInWave = pState.RemainingToSpawn;
            pState.SpawnInterval = config.SpawnInterval;
            pState.SpawnTimer = 0f;

            _api.PlayPlayerWarningSound(pState.PlayerIndex);
        }
    }

    private void EndGlobalWave()
    {
        _globalWaveSpawning = false;

        foreach (var p in _players.Values)
        {
            p.Spawning = false;
        }

        PublishSummaryTableForWave(_globalCurrentWave);

        if (_globalCurrentWave >= TotalWaves)
        {
            EndGameWithVictory();
        }
        else
        {
            ShowGlobalIntermission(_globalCurrentWave + 1);
        }
    }

    private void ShowGlobalIntermission(int nextWave)
    {
        if (nextWave <= TotalWaves)
        {
            _globalWaveSpawning = false;
            float delay = Waves[nextWave - 1].InterWaveDelay;
            _globalInterWaveTimer = delay;
            string label = string.Format(_api.Translate("WAVE_INTERMISSION", 0), nextWave);
            _api.StartCountdownTimer(delay, label);
        }
    }

    private void UpdateSpawning(PlayerState pState, float delta)
    {
        pState.SpawnTimer -= delta;
        if (pState.SpawnTimer > 0f) return;

        pState.SpawnTimer = pState.SpawnInterval;
        SpawnNextEnemyInWave(pState);
    }

    private void EndGameWithVictory()
    {
        _gameOver = true;
        _api.StopCountdownTimer();
        _api.ShowFeedbackText(_api.Translate("VICTORIA_TOTAL", 0), new Vector3(1f, 0.9f, 0.2f));
        _api.TriggerVictory();
    }

    private void SpawnNextEnemyInWave(PlayerState pState)
    {
        var config = Waves[pState.WaveBeingSpawned - 1];
        string unitType = config.Composition[pState.SpawnedInWave % config.Composition.Length];

        int enemySlot = GetEnemyPlayerIndex(pState.PlayerIndex);
        UnitId unit = UnitId.Invalid;
        if (unitType == "unit/dragon_titan_boss" || unitType == "dragon_titan_boss")
        {
            var bossPos = pState.QuadrantCenter + new Vector3(0f, 0f, -10.0f);
            unit = _api.SpawnUnitForPlayer(unitType, new Vector2(bossPos.X, bossPos.Z), enemySlot, true);
            if (unit.IsValid)
            {
                pState.RaidBossInstance = unit;
                unit.Scale = 5.0f;
            }
        }
        else
        {
            var spawnPos = Coordinates.GetRandomSpawnPointOnRing(_api, pState.QuadrantCenter, SpawnRingMinRadius, SpawnRingMaxRadius, SpawnHeight);
            unit = _api.SpawnUnitForPlayer(unitType, new Vector2(spawnPos.X, spawnPos.Z), enemySlot, true);
        }

        if (unit.IsValid)
        {
            _unitQuadrantMap[unit.UniqueId] = pState.PlayerIndex;

            float hpScale = 1.0f + (pState.PerksPicked * 0.05f) + (pState.WaveBeingSpawned * 0.03f) + (pState.HasUnboundGreed ? 0.15f : 0f);
            unit.MaxHealth *= hpScale;
            unit.Health = unit.MaxHealth;

            unit.Speed = MathF.Max(unit.Speed, 6.5f);
            if (pState.HasUnboundGreed)
            {
                unit.Speed *= 1.20f;
            }

            unit.AttackMove(pState.QuadrantCenter);
            pState.SpawnedInWave++;
            pState.RemainingToSpawn--;
        }
    }

    private void PublishSummaryTableForWave(int waveNumber)
    {
        string title = string.Format(_api.Translate("WAVE_SUMMARY_TITLE", 0), waveNumber);
        _api.ClearSummaryTable();
        foreach (var p in _players.Values.OrderBy(x => x.PlayerIndex))
        {
            int score = (int)(p.TotalDamageDealt * 0.1f + p.TotalGoldEarned * 0.5f + p.TotalKills * 50);
            string pName = $"Player {p.PlayerIndex}";
            string dmgStr = $"{p.TotalDamageDealt:F0}";
            string incStr = $"{p.TotalGoldEarned:F0}g";
            string scoreStr = $"{score}";
            _api.SetSummaryTableRow(pName, dmgStr, incStr, scoreStr);
        }
        _api.ShowSummaryTable(title, true);
    }

    private void UpdateBossEncounterIfActive(PlayerState pState, float delta)
    {
        if (pState.WaveBeingSpawned != 15) return;
        var boss = pState.RaidBossInstance;
        if (!boss.IsValid || boss.IsDead) return;

        UpdateBossTargetIndicator(pState, boss, delta);
        UpdateBossEnragePhase(pState, boss);
        UpdateBossEscortTimer(pState, delta);
    }

    private void UpdateBossTargetIndicator(PlayerState pState, UnitId boss, float delta)
    {
        pState.BossIndicatorTimer -= delta;
        if (pState.BossIndicatorTimer > 0f) return;

        pState.BossIndicatorTimer = 2.0f;
        _api.SpawnTargetIndicator(boss.Position, new Vector3(1f, 0.1f, 0.1f));
    }

    private void UpdateBossEnragePhase(PlayerState pState, UnitId boss)
    {
        if (pState.BossEnraged || boss.Health > boss.MaxHealth * 0.5f) return;

        pState.BossEnraged = true;
        boss.Speed = 8.25f;
        _api.SetUnitColor(boss, new Vector3(1f, 0.2f, 0.2f));
        _api.ShowFeedbackText(_api.Translate("RAID_BOSS_ENRAGE", pState.PlayerIndex), new Vector3(1f, 0.15f, 0.15f));
        _api.PlayPlayerWarningSound(pState.PlayerIndex);
        _api.ShakeCamera(3.0f, 1.5f);
    }

    private void UpdateBossEscortTimer(PlayerState pState, float delta)
    {
        pState.BossEscortTimer -= delta;
        if (pState.BossEscortTimer > 0f) return;

        pState.BossEscortTimer = pState.BossEnraged ? 8.0f : 12.0f;
        SpawnBossEscortWave(pState);
    }

    private void TriggerRaidBossCinematic(PlayerState pState)
    {
        pState.BossEscortTimer = 6.0f;
        pState.BossIndicatorTimer = 0f;

        _api.ShowFeedbackText(_api.Translate("RAID_BOSS_ALERT", pState.PlayerIndex), new Vector3(1f, 0.85f, 0.1f));
        _api.PlayPlayerWarningSound(pState.PlayerIndex);
        _api.ShakeCamera(2.5f, 2.0f);
        var bossPos = pState.QuadrantCenter + new Vector3(0f, 0f, -10.0f);
        _api.PanPlayerCameraTo(pState.PlayerIndex, bossPos, 1.5f);
        _api.PingPlayerMinimap(pState.PlayerIndex, bossPos);
    }

    private void SpawnBossEscortWave(PlayerState pState)
    {
        for (int s = 0; s < 8; s++) SpawnSingleEscort(pState, "unit/zombie_soldier");
        for (int g = 0; g < 4; g++) SpawnSingleEscort(pState, "unit/forest_goblin");
        for (int o = 0; o < (pState.BossEnraged ? 2 : 0); o++) SpawnSingleEscort(pState, "unit/dark_iron_ogre");

        _api.BroadcastMessage(_api.Translate("RAID_BOSS_ESCORT", pState.PlayerIndex));
    }

    private void SpawnSingleEscort(PlayerState pState, string unitType)
    {
        var spawnPos = Coordinates.GetRandomSpawnPointOnRing(_api, pState.QuadrantCenter, SpawnRingMinRadius, SpawnRingMaxRadius, SpawnHeight);
        int enemySlot = GetEnemyPlayerIndex(pState.PlayerIndex);
        var unit = _api.SpawnUnitForPlayer(unitType, new Vector2(spawnPos.X, spawnPos.Z), enemySlot, true);
        if (unit.IsValid)
        {
            _unitQuadrantMap[unit.UniqueId] = pState.PlayerIndex;

            unit.AttackMove(pState.QuadrantCenter);
            pState.AliveInWave++;
        }
    }

    private void OnUnitDamaged(UnitId victim, UnitId attacker, float damage)
    {
        if (_gameOver || !victim.IsValid || !attacker.IsValid || victim.IsDead) return;

        if (AreEnemies(victim, attacker) && (attacker.TemplateID == "unit/survivor_hero" || attacker.TemplateID == "survivor_hero"))
        {
            if (_players.TryGetValue(attacker.Player, out var pState)
                && pState.Hero.IsValid
                && pState.Hero.UniqueId == attacker.UniqueId)
            {
                pState.TotalDamageDealt += damage;
                ApplyHeroAttackProcs(pState, victim, damage);
            }
        }

        if (AreEnemies(victim, attacker) && (victim.TemplateID == "unit/survivor_hero" || victim.TemplateID == "survivor_hero"))
        {
            if (_players.TryGetValue(victim.Player, out var pState)
                && pState.Hero.IsValid
                && pState.Hero.UniqueId == victim.UniqueId)
            {
                if (pState.HasDivineThorns && !attacker.IsDead && attacker.Health > 0f)
                {
                    float reflectDamage = damage * 0.50f;
                    pState.TotalDamageDealt += reflectDamage;
                    DealBonusDamage(attacker, reflectDamage);
                    _api.SpawnVisualEffect("lightning", attacker.Position, 0.6f);
                    if (!pState.ThornsCooldowns.TryGetValue(attacker.UniqueId, out float nextAllowed) || pState.TotalPlayTime >= nextAllowed)
                    {
                        pState.ThornsCooldowns[attacker.UniqueId] = pState.TotalPlayTime + 0.5f;
                        string thornsText = string.Format(_api.Translate("THORNS_FLOATING", pState.PlayerIndex), reflectDamage.ToString("F0"));
                        _api.CreateFloatingText(thornsText, attacker.Position + new Vector3(0, 1.8f, 0), new Vector3(0.9f, 0.9f, 0.2f), 0.7f);
                    }
                }
            }
        }
    }

    private void DealBonusDamage(UnitId victim, float amount)
    {
        victim.Health -= amount;
        if (victim.Health <= 0f)
        {
            _api.KillUnit(victim);
        }
    }

    private void ApplyHeroAttackProcs(PlayerState pState, UnitId victim, float damage)
    {
        var hero = pState.Hero;
        if (!hero.IsValid) return;

        int sharpStacks = pState.GetPerkStackCount("sharp_precision");
        float critChance = sharpStacks * 0.20f;
        if (critChance > 0f && _api.RandomFloat(0f, 1f) < critChance)
        {
            float critMult = pState.HasCrystalMonolith ? 4.5f : 2.0f;
            float extraCritDamage = damage * (critMult - 1.0f);
            pState.TotalDamageDealt += extraCritDamage;
            DealBonusDamage(victim, extraCritDamage);
            string critText = string.Format(_api.Translate("CRIT_FLOATING", pState.PlayerIndex), critMult.ToString("F1"));
            _api.CreateFloatingText(critText, victim.Position + new Vector3(0, 2.2f, 0), new Vector3(1f, 0.2f, 0.2f), 0.9f);
        }

        if (pState.HasSniperStance)
        {
            float dist = Vector3.Distance(hero.Position, victim.Position);
            if (dist >= 25.0f)
            {
                float bonusSnipe = damage * 0.40f;
                pState.TotalDamageDealt += bonusSnipe;
                DealBonusDamage(victim, bonusSnipe);
                _api.CreateFloatingText(_api.Translate("SNIPER_FLOATING", pState.PlayerIndex), victim.Position + new Vector3(0, 2.0f, 0), new Vector3(0.2f, 0.9f, 0.4f), 0.8f);
            }
        }

        int lifestealStacks = pState.GetPerkStackCount("lifesteal");
        if (lifestealStacks > 0)
        {
            float healAmount = damage * (lifestealStacks * 0.15f);
            hero.Health = MathF.Min(hero.MaxHealth, hero.Health + healAmount);
            if (pState.LifestealTextTimer <= 0f)
            {
                pState.LifestealTextTimer = 0.5f;
                string lsText = string.Format(_api.Translate("LIFESTEAL_FLOATING", pState.PlayerIndex), healAmount.ToString("F0"));
                _api.CreateFloatingText(lsText, hero.Position + new Vector3(0, 1.8f, 0), new Vector3(0.2f, 1f, 0.4f), 0.6f);
            }
        }

        if (pState.HasChainLightning && _api.RandomFloat(0f, 1f) < 0.30f)
        {
            ApplyChainLightning(pState, victim);
        }

        if (pState.HasExplosiveArrows)
        {
            ApplyExplosiveArrows(pState, victim, damage);
        }

        if (pState.HasPiercingShot)
        {
            ApplyPiercingShot(pState, victim, damage);
        }

        if (pState.HasUnstableVortex)
        {
            ApplyUnstableVortex(pState, victim);
        }

        if (pState.HasBlindFury)
        {
            FireFuryArrow(pState, victim);
        }

        int reinforcedStacks = pState.GetPerkStackCount("reinforced_multishot");
        if (reinforcedStacks > 0)
        {
            ApplyMultiShot(pState, victim, damage, reinforcedStacks);
        }
    }

    private void ApplyChainLightning(PlayerState pState, UnitId primary)
    {
        _api.SpawnVisualEffect("lightning", primary.Position, 1.5f);
        _api.CreateFloatingText(_api.Translate("LIGHTNING_FLOATING", pState.PlayerIndex), primary.Position + new Vector3(0, 2f, 0), new Vector3(0.3f, 0.7f, 1.0f), 0.8f);

        int jumps = 0;
        foreach (var nearby in _api.GetUnitsInRadius(primary.Position, 14f))
        {
            if (!nearby.IsValid || !IsEnemy(nearby, pState.PlayerIndex) || nearby.UniqueId == primary.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;
            if (jumps >= 4) break;

            pState.TotalDamageDealt += 75f;
            DealBonusDamage(nearby, 75f);
            _api.SpawnVisualEffect("lightning", nearby.Position, 1.0f);
            _api.SpawnProjectile("weapon/survivor_arrow", primary.Position, nearby.Position, 50f);
            jumps++;
        }
    }

    private void ApplyExplosiveArrows(PlayerState pState, UnitId primary, float damage)
    {
        _api.SpawnVisualEffect("fireblast", primary.Position, 1.4f);
        if (pState.Hero.IsValid)
        {
            _api.SpawnProjectile("weapon/fire_shard", pState.Hero.Position, primary.Position, 40f);
        }
        float splashDamage = damage * 0.50f;

        foreach (var nearby in _api.GetUnitsInRadius(primary.Position, 5f))
        {
            if (!nearby.IsValid || !IsEnemy(nearby, pState.PlayerIndex) || nearby.UniqueId == primary.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;

            pState.TotalDamageDealt += splashDamage;
            DealBonusDamage(nearby, splashDamage);
        }
    }

    private void ApplyPiercingShot(PlayerState pState, UnitId primary, float damage)
    {
        float pierceDamage = damage * 0.70f;
        UnitId behind = UnitId.Invalid;
        foreach (var u in _api.GetUnitsInRadius(primary.Position, 8f))
        {
            if (u.IsValid && IsEnemy(u, pState.PlayerIndex) && u.UniqueId != primary.UniqueId && !u.IsDead && u.Health > 0f)
            {
                behind = u;
                break;
            }
        }

        if (behind.IsValid)
        {
            pState.TotalDamageDealt += pierceDamage;
            DealBonusDamage(behind, pierceDamage);
            _api.SpawnProjectile(GetHeroProjectileId(pState), primary.Position, behind.Position, 55f);
            _api.CreateFloatingText(_api.Translate("PIERCING_FLOATING", pState.PlayerIndex), behind.Position + new Vector3(0, 1.8f, 0), new Vector3(0.5f, 0.7f, 1.0f), 0.7f);
        }
    }

    private void ApplyUnstableVortex(PlayerState pState, UnitId primary)
    {
        if (!pState.Hero.IsValid) return;
        int shotCount = 0;
        foreach (var nearby in _api.GetUnitsInRadius(primary.Position, 15f))
        {
            if (!nearby.IsValid || !IsEnemy(nearby, pState.PlayerIndex) || nearby.UniqueId == primary.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;
            if (shotCount >= 2) break;

            float bonusDmg = pState.Hero.Damage * 0.60f;
            pState.TotalDamageDealt += bonusDmg;
            DealBonusDamage(nearby, bonusDmg);
            _api.SpawnProjectile(GetHeroProjectileId(pState), pState.Hero.Position, nearby.Position, 36f);
            shotCount++;
        }
    }

    private void FireFuryArrow(PlayerState pState, UnitId victim)
    {
        if (!pState.Hero.IsValid) return;
        UnitId secondary = UnitId.Invalid;
        foreach (var u in _api.GetUnitsInRadius(victim.Position, 10f))
        {
            if (u.IsValid && IsEnemy(u, pState.PlayerIndex) && u.UniqueId != victim.UniqueId && !u.IsDead && u.Health > 0f)
            {
                secondary = u;
                break;
            }
        }

        if (!secondary.IsValid) return;

        pState.TotalDamageDealt += pState.Hero.Damage;
        DealBonusDamage(secondary, pState.Hero.Damage);
        _api.SpawnProjectile(GetHeroProjectileId(pState), pState.Hero.Position, secondary.Position, 48f);
        _api.CreateFloatingText(_api.Translate("FURY_FLOATING", pState.PlayerIndex), secondary.Position + new Vector3(0f, 2f, 0f), new Vector3(1f, 0.4f, 0.1f), 0.8f);
    }

    private void ApplyMultiShot(PlayerState pState, UnitId victim, float damage, int targetLimit)
    {
        if (!pState.Hero.IsValid) return;
        float splash = damage * MultiShotSplashRatio;
        int hits = 0;

        foreach (var nearby in _api.GetUnitsInRadius(victim.Position, 7f))
        {
            if (!nearby.IsValid || !IsEnemy(nearby, pState.PlayerIndex) || nearby.UniqueId == victim.UniqueId || nearby.IsDead || nearby.Health <= 0f)
                continue;
            if (hits >= targetLimit) break;

            pState.TotalDamageDealt += splash;
            DealBonusDamage(nearby, splash);
            _api.SpawnProjectile(GetHeroProjectileId(pState), pState.Hero.Position, nearby.Position, 42f);
            hits++;
        }
    }

    private float GetUnitBounty(string unitId) => unitId switch
    {
        "unit/dragon_titan_boss" or "dragon_titan_boss" => 1000.0f,
        "unit/mech_spider_monster" or "mech_spider_monster" => 250.0f,
        "unit/giant_rock_golem" or "giant_rock_golem" => 150.0f,
        "unit/dark_iron_ogre" or "dark_iron_ogre" => 20.0f,
        "unit/cyber_dragon" or "cyber_dragon" => 20.0f,
        "unit/flame_drake" or "flame_drake" => 20.0f,
        "unit/zombie_warrior" or "zombie_warrior" => 15.0f,
        "unit/forest_goblin" or "forest_goblin" => 10.0f,
        "unit/zombie_soldier" or "zombie_soldier" => 10.0f,
        _ => 10.0f
    };

    private void OnUnitDied(UnitId unit, UnitId killer)
    {
        if (_gameOver || !unit.IsValid) return;

        int waveOwnerIndex = 0;
        if (_unitQuadrantMap.TryGetValue(unit.UniqueId, out int mappedIdx))
        {
            waveOwnerIndex = mappedIdx;
            _unitQuadrantMap.Remove(unit.UniqueId);
        }

        if (!IsEnemy(unit, waveOwnerIndex)) return;

        if (!_players.TryGetValue(waveOwnerIndex, out var waveOwner))
        {
            waveOwner = _players.Values.FirstOrDefault();
            if (waveOwner == null) return;
        }

        if (waveOwner.IsDefeated)
            return;

        foreach (var p in _players.Values)
        {
            p.ThornsCooldowns.Remove(unit.UniqueId);
        }

        int creditIndex = killer.IsValid && killer.Player > 0 && _players.ContainsKey(killer.Player)
            ? killer.Player
            : waveOwner.PlayerIndex;

        var creditPlayer = _players[creditIndex];

        waveOwner.AliveInWave = Math.Max(0, waveOwner.AliveInWave - 1);
        if (waveOwner.AliveInWave == 0 && waveOwner.RemainingToSpawn == 0)
        {
            waveOwner.Spawning = false;
        }
        creditPlayer.TotalKills++;

        float bounty = GetUnitBounty(unit.TemplateID);
        if (creditPlayer.HasUnboundGreed)
        {
            bounty *= 2.50f;
        }

        if (creditPlayer.HasMidasTouch && unit.TemplateID is not ("unit/dragon_titan_boss" or "dragon_titan_boss") && _api.RandomFloat(0f, 1f) < 0.15f)
        {
            bounty += 40.0f;
            _api.CreateFloatingText(_api.Translate("MIDAS_FLOATING", creditPlayer.PlayerIndex), unit.Position + new Vector3(0, 2.3f, 0), new Vector3(1f, 0.85f, 0.1f), 1.3f);
        }

        _api.AdjustPlayerGold(creditPlayer.PlayerIndex, bounty);
        creditPlayer.TotalGoldEarned += bounty;
        _api.CreateFloatingText($"+{bounty:F0}g", unit.Position + new Vector3(0, 1.8f, 0), new Vector3(1f, 0.85f, 0.1f), 1.2f);

        if (unit.TemplateID is "unit/dragon_titan_boss" or "dragon_titan_boss")
        {
            waveOwner.BossDefeated = true;
            bool allBossesDead = _players.Values.All(p => p.IsDefeated || p.BossDefeated);
            if (allBossesDead)
            {
                _gameOver = true;
                _api.StopCountdownTimer();
                _api.ShowFeedbackText(_api.Translate("RAID_BOSS_DEFEATED", waveOwner.PlayerIndex), new Vector3(1f, 0.9f, 0.2f));
                foreach (var p in _players.Values)
                {
                    _api.PlayPlayerClickSound(p.PlayerIndex);
                }
                _api.TriggerVictory();
            }
        }
    }
    private PlayerState? ResolvePlayer(UnitId caster, UnitId selected = default, string? abilityId = null, Vector3? targetPosition = null)
    {
        if (caster.IsValid)
        {
            var byHero = _players.Values.FirstOrDefault(p => p.Hero.IsValid && p.Hero.UniqueId == caster.UniqueId);
            if (byHero != null) return byHero;
            if (_players.TryGetValue(caster.Player, out var pByUnit)) return pByUnit;
        }

        if (selected.IsValid)
        {
            var byHero = _players.Values.FirstOrDefault(p => p.Hero.IsValid && p.Hero.UniqueId == selected.UniqueId);
            if (byHero != null) return byHero;
            if (_players.TryGetValue(selected.Player, out var pBySel)) return pBySel;
        }

        if (abilityId == "hero_meteor_spell" && targetPosition.HasValue && targetPosition.Value != Vector3.Zero)
        {
            return _players.Values
                .OrderBy(p => Vector3.DistanceSquared(new Vector3(targetPosition.Value.X, 0, targetPosition.Value.Z), new Vector3(p.QuadrantCenter.X, 0, p.QuadrantCenter.Z)))
                .FirstOrDefault();
        }

        return null;
    }

    private void SendPlayerPerksSummary(PlayerState pState)
    {
        if (pState.PerkStacks.Count == 0)
        {
            _api.SendMessageToPlayer(pState.PlayerIndex, $"[Perks P{pState.PlayerIndex}] {_api.Translate("PERKS_EMPTY", pState.PlayerIndex)}");
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append($"[Perks P{pState.PlayerIndex}] ");
        bool first = true;
        for (int i = 0; i < PerkPool.Length; i++)
        {
            var perk = PerkPool[i];
            if (pState.PerkStacks.TryGetValue(perk.Id, out int stacks) && stacks > 0)
            {
                if (!first) sb.Append(" | ");
                first = false;

                string perkName = _api.Translate(perk.NameKey, pState.PlayerIndex);
                string bonusDesc = GetPerkSummaryEffect(perk.Id, stacks);
                sb.Append($"x{stacks} {perkName} ({bonusDesc})");
            }
        }

        if (first)
        {
            _api.SendMessageToPlayer(pState.PlayerIndex, $"[Perks P{pState.PlayerIndex}] {_api.Translate("PERKS_EMPTY", pState.PlayerIndex)}");
        }
        else
        {
            _api.SendMessageToPlayer(pState.PlayerIndex, sb.ToString());
        }
    }

    private string GetPerkSummaryEffect(string perkId, int stacks) => perkId switch
    {
        "vigor_potion" => $"+{stacks * 350} HP",
        "arrow_sharpening" => $"+{stacks * 12} ATK",
        "reinforced_plates" => $"+{stacks * 4} ARM",
        "hawk_eye" => $"+{stacks * 6} RNG",
        "lifesteal" => $"+{stacks * 15}% LS",
        "reinforced_multishot" => $"+{stacks} Multi",
        "royal_tribute" => $"+{stacks * 10}g/s",
        "piercing_shot" => $"+{stacks * 70}% Pierce",
        "sharp_precision" => $"+{stacks * 20}% CRIT",
        "blood_pact" => "+100% ATK, -2% HP/s",
        "crystal_monolith" => "+250% CRIT DMG, +15 RNG, -40% HP",
        "unbound_greed" => "+150% Gold, +20% Mob Spd/HP",
        "unstable_vortex" => "2 Split Shots, 0 ARM",
        "demon_deal" => "+1000g, +35% Mobs",
        "blind_fury" => "Double Shot, -35% RNG",
        "chain_lightning" => "Rayo (30%)",
        "explosive_arrows" => "Explosivo (50%)",
        "frost_aura" => "Escarcha (40%)",
        "divine_thorns" => "+15 ARM, Espinas (50%)",
        "sniper_stance" => "+18 RNG, Asedio (+40%)",
        "midas_touch" => "Midas (15%)",
        _ => "Activo"
    };

    private void SendPlayerStatsSummary(PlayerState pState)
    {
        var hero = pState.Hero;
        if (!hero.IsValid)
        {
            _api.SendMessageToPlayer(pState.PlayerIndex, $"[Stats P{pState.PlayerIndex}] {_api.Translate("HERO_DEFEATED", pState.PlayerIndex)}");
            return;
        }

        string hpStr = $"HP {hero.Health:F0}/{hero.MaxHealth:F0}";
        float bonusAtk = hero.Damage - BaseHeroDamage;
        string atkStr = bonusAtk > 0 ? $"ATK {hero.Damage:F0} (+{bonusAtk:F0})" : (bonusAtk < 0 ? $"ATK {hero.Damage:F0} ({bonusAtk:F0})" : $"ATK {hero.Damage:F0}");
        string rngStr = $"RNG {hero.Range:F0}";
        float armPct = hero.Armor > 0f ? (hero.Armor / (hero.Armor + 20.0f)) * 100.0f : 0f;
        string armStr = $"ARM {hero.Armor:F0} ({armPct:F0}%)";
        float critChance = pState.GetPerkStackCount("sharp_precision") * 20.0f;
        string critStr = $"CRIT {critChance:F0}%";
        float lsPct = pState.GetPerkStackCount("lifesteal") * 15.0f;
        string lsStr = $"LS {lsPct:F0}%";

        string statsMsg = $"[Stats P{pState.PlayerIndex}] {hpStr} | {atkStr} | {rngStr} | {armStr} | {critStr} | {lsStr}";
        _api.SendMessageToPlayer(pState.PlayerIndex, statsMsg);
    }
    private void OnPlayerChatMessage(string message, UnitId selected)
    {
        if (_gameOver) return;

        PlayerState? pState = null;
        if (selected.IsValid && _players.TryGetValue(selected.Player, out var pByUnit))
        {
            pState = pByUnit;
        }
        else
        {
            pState = ResolvePlayer(caster: UnitId.Invalid, selected: selected);
        }

        if (pState == null && _players.Count == 1)
        {
            pState = _players.Values.FirstOrDefault();
        }

        if (pState == null || pState.IsDefeated) return;

        string clean = message.Trim().ToLowerInvariant();
        switch (clean)
        {
            case "-1":
            case "1":
                TryBuyPerk(pState, 0);
                break;
            case "-2":
            case "2":
                TryBuyPerk(pState, 1);
                break;
            case "-3":
            case "3":
                TryBuyPerk(pState, 2);
                break;
            case "-reroll":
            case "reroll":
            case "-r":
                TryRerollShop(pState);
                break;
            case "-shop":
            case "shop":
            case "-tienda":
            case "tienda":
                SendPlayerShopSummary(pState);
                break;
                        case "-help":
            case "help":
            case "-ayuda":
            case "ayuda":
                _api.SendMessageToPlayer(pState.PlayerIndex, _api.Translate("HELP_MESSAGE", pState.PlayerIndex));
                break;
            case "-perks":
            case "perks":
            case "-mejoras":
            case "mejoras":
            case "-build":
            case "build":
                SendPlayerPerksSummary(pState);
                break;
            case "-stats":
            case "stats":
            case "-estadisticas":
            case "estadisticas":
            case "-heroe":
            case "heroe":
                SendPlayerStatsSummary(pState);
                break;
            case "-next":
            case "next":
            case "-skip":
            case "skip":
            case "-listo":
            case "listo":
                TrySkipIntermission(pState);
                break;
        }
    }

    private void TrySkipIntermission(PlayerState pState)
    {
        if (!_globalWaveSpawning && _globalInterWaveTimer > 0f)
        {
            _globalInterWaveTimer = 0f;
            _api.StopCountdownTimer();
            string msg = string.Format(_api.Translate("WAVE_SKIPPED", pState.PlayerIndex), pState.PlayerIndex);
            _api.BroadcastMessage(msg);
        }
    }

    private void OnSpellCast(UnitId caster, string abilityId, Vector3 targetPosition)
    {
        if (_gameOver) return;

        var pState = ResolvePlayer(caster, selected: UnitId.Invalid, abilityId: abilityId, targetPosition: targetPosition);
        if (pState == null || pState.IsDefeated) return;

        switch (abilityId)
        {
            case "perk_choose_1":
                TryBuyPerk(pState, 0);
                break;
            case "perk_choose_2":
                TryBuyPerk(pState, 1);
                break;
            case "perk_choose_3":
                TryBuyPerk(pState, 2);
                break;
            case "perk_reroll":
                TryRerollShop(pState);
                break;
            case "hero_meteor_spell":
                if (pState.Hero.IsValid)
                {
                    _api.SpawnProjectile("weapon/boss_meteor", pState.Hero.Position, targetPosition, 26f);
                    _api.SpawnVisualEffect("fireblast", targetPosition, 2.5f);
                    _api.CreateFloatingText(_api.Translate("METEOR_FLOATING", pState.PlayerIndex), targetPosition + new Vector3(0, 2f, 0), new Vector3(1f, 0.4f, 0.1f), 1.2f);
                    foreach (var enemy in _api.GetUnitsInRadius(targetPosition, 4.5f))
                    {
                        if (enemy.IsValid && IsEnemy(enemy, pState.PlayerIndex) && !enemy.IsDead && enemy.Health > 0f)
                        {
                            pState.TotalDamageDealt += 120f;
                            DealBonusDamage(enemy, 120f);
                        }
                    }
                }
                break;
        }
    }

    private void UpdateLeaderboardDisplay()
    {
        if (_players.Count <= 1)
        {
            var p0 = _players.Values.FirstOrDefault();
            if (p0 == null) return;

            _api.SetLeaderboardValue(_api.Translate("LB_WAVE", p0.PlayerIndex), $"{_globalCurrentWave} / {TotalWaves}");
            _api.SetLeaderboardValue(_api.Translate("LB_GOLD", p0.PlayerIndex), $"{(int)_api.GetPlayer(p0.PlayerIndex).Gold} (+{(int)p0.CurrentIncomePerSecond}/s)");
            _api.SetLeaderboardValue(_api.Translate("LB_SHOP", p0.PlayerIndex), $"{p0.ShopRotationTimer:F0}s / 40s | Perks: {p0.PerksPicked}");
            _api.SetLeaderboardValue(_api.Translate("LB_KILLS", p0.PlayerIndex), $"{p0.TotalKills}");
            _api.SetLeaderboardValue(_api.Translate("LB_ENEMIES", p0.PlayerIndex), $"{Math.Max(0, p0.AliveInWave)}");
        }
        else
        {
            _api.SetLeaderboardValue(_api.Translate("LB_WAVE", 0), $"{_globalCurrentWave} / {TotalWaves}");
            foreach (var p in _players.Values.OrderBy(x => x.PlayerIndex))
            {
                string playerName = _api.GetPlayerName(p.PlayerIndex);
                string prefix = !string.IsNullOrWhiteSpace(playerName) && playerName != "Default" && !playerName.StartsWith("Player") ? playerName : $"P{p.PlayerIndex}";
                string status = p.IsDefeated
                    ? $"({_api.Translate("LB_FALLEN", p.PlayerIndex)})"
                    : (p.RemainingToSpawn == 0 && p.AliveInWave == 0
                        ? "CLEAR"
                        : $"{p.AliveInWave} left");
                _api.SetLeaderboardValue($"{prefix} {_api.Translate("LB_GOLD", p.PlayerIndex)}", $"{(int)_api.GetPlayer(p.PlayerIndex).Gold} (+{(int)p.CurrentIncomePerSecond}/s)");
                _api.SetLeaderboardValue($"{prefix} {_api.Translate("LB_STATUS", p.PlayerIndex)}", $"{status} | Kills: {p.TotalKills}");
            }
        }
    }
}