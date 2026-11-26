using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using AnoMech.Scenarios.Umad.P1TeleTrouncing;

namespace AnoMech.Scenarios.Umad.P1GravenImage1;

using Constants = UmadConstants;

public sealed class UmadP1GravenImage1Scenario : IScenario
{
    public string Name => "Graven Image 1 (0-78s)";
    public IPhase Phase => UmadZone.P1;
    public float BgmSecondsAtStart => 0f;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => false;
    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP1GravenImage1Ai()];
    public object SettingsOverrides => settingsWindow.Overrides;

    private SimWorld world = null!;
    private SimParty party = null!;
    private UmadP1GravenImage1State state = null!;
    private SimEnemy? kefka;
    private readonly List<SimEnemy> statueHelpers = [];
    private SimEventObject? statuePropUpper;
    private SimEventObject? statuePropLower;
    private SimEventObject? colossusProp;
    private readonly List<Vector3> towerPositions = [];
    private readonly UmadP1GravenImage1SettingsWindow settingsWindow = new();

    internal static readonly Vector3 UpperStatuePos = new(0f, 18.5f, -44f);
    internal static readonly Vector3 LowerStatuePos = new(0f, 0f, -35f);

    private const float TowerSoakRadius = 4.0f;

    private static readonly (uint State, uint Bitmask) EObjAnimAppear = (0x0001, 0x0002);
    private static readonly (uint State, uint Bitmask) EObjAnimTetherFire = (0x0010, 0x0020);
    private static readonly (uint State, uint Bitmask) EObjAnimDespawn = (0x0004, 0x0008);

    // Cone headings in FFXIV/SimCast convention (south=0, +pi/4=SE, +3pi/4=SW, -3pi/4=NW, -pi/4=NE).
    private static readonly float[] IcePairA = [-MathF.PI / 4f, 3f * MathF.PI / 4f];
    private static readonly float[] IcePairB = [-3f * MathF.PI / 4f, MathF.PI / 4f];

    public UmadP1GravenImage1State? LastState { get; private set; }

    public void DrawSettings() => settingsWindow.Draw();

    public void RunInstanceEvents(SimWorld instanceWorld)
    {
    }

    private void At(float fightTime, Action action)
        => world.Events.Add(state.CountdownSeconds + fightTime, action);

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        state = new UmadP1GravenImage1State(world.Rng, settingsWindow.Overrides);
        LastState = state;

        CleanupObjects();

        world.Events.Add(0f, SpawnInitialActors);
        ScheduleCountdownLogs();

        foreach (var at in new[] { 4.48f, 7.50f, 22.80f, 61.80f, 74.20f })
            At(at, () => kefka?.Cast(UmadActions.AutoAttack, party.Get(PartyRole.MainTank), animationVariation: 1));

        // Tankbuster 1: 恶狠狠毁荡 (2-hit).
        At(11.60f, () =>
        {
            if (party.Get(PartyRole.MainTank) is not { } mt) return;
            mt.AttachLockonVfx(Constants.LockonId.TankBuster);
            kefka?.Cast(UmadActions.ViciousDestruction1, mt);
        });
        At(19.80f, () =>
        {
            if (party.Get(PartyRole.MainTank) is { } mt)
                kefka?.Cast(UmadActions.ViciousDestruction2, mt);
        });

        At(26.00f, () => kefka?.Cast(Constants.ActionId.KefkaPoof, animationLock: 1.1f));

        // Graven Image 1.
        At(27.42f, () =>
        {
            kefka?.Cast(Constants.ActionId.GravenImage, animationLock: 2.1f);
            colossusProp?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
            statuePropUpper?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
            statuePropLower?.PlayBeat(EObjAnimAppear.State, EObjAnimAppear.Bitmask);
        });
        At(31.17f, TetherWaveBulletGroup);

        // Mystery Magic 1: Blizzard III Blowout + Flagrant Fire III.
        At(33.49f, () =>
        {
            kefka?.AttachLockonVfx(state.Ice1IsLie ? Constants.LockonId.ColdFalse : Constants.LockonId.ColdTrue);
            kefka?.AttachLockonVfx(state.Fire1IsLie ? Constants.LockonId.FireFalse : Constants.LockonId.FireTrue);
            AttachFire1Markers();
        });
        At(33.57f, () =>
        {
            kefka?.Cast(Constants.ActionId.MysteryMagic, animationLock: 3.1f);
            SpawnIceCones(state.Ice1DiagonalB, state.Ice1IsLie);
        });

        // Native Knockback row 72 (0x48) on WaveBullet — no pre-knockback Bind/CarryTo.
        At(36.25f, ResolveWaveBulletKnockback);
        At(39.37f, ResolveFire1);

        // Double-trouble Trap + Wave Cannon Lasers + 4 Towers.
        At(42.75f, () => kefka?.Cast(Constants.ActionId.DoubleTroubleTrapCast, animationLock: 2.1f));
        At(43.55f, () => statuePropLower?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask));
        At(43.60f, ResolveWaveCannonLasers);
        At(44.22f, SpawnExplosionTowers);
        At(45.73f, ApplyTrap1Debuffs);
        At(47.20f, JudgeExplosionTowers);

        // Mystery Magic 2: Blizzard III Blowout + Thrumming Thunder III, overlapped with Trap 1 stack.
        At(49.74f, () =>
        {
            kefka?.AttachLockonVfx(state.Ice2IsLie ? Constants.LockonId.ColdFalse : Constants.LockonId.ColdTrue);
            kefka?.AttachLockonVfx(state.ThunderIsLie ? Constants.LockonId.LightningFalse : Constants.LockonId.LightningTrue);
        });
        At(49.83f, () =>
        {
            kefka?.Cast(Constants.ActionId.MysteryMagic, animationLock: 3.1f);
            SpawnIceCones(state.Ice2DiagonalB, state.Ice2IsLie);
            SpawnThunderLines();
        });
        At(50.85f, ResolveTrap1Stacks);

        // Raidwide + 3x Overdrive.
        At(58.91f, () => kefka?.Cast(UmadActions.LightOfJudgmentP1));
        foreach (var at in new[] { 67.06f, 69.15f, 71.24f })
            At(at, () =>
            {
                var target = party.Get(PartyRole.OffTank) ?? party.Get(PartyRole.MainTank);
                if (target != null) kefka?.Cast(UmadActions.Overdrive, target);
            });

        // Empty tail event at 77.00s so ScenarioSession finishes cleanly at 78.00s.
        At(77.00f, () => { });

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP1GravenImage1State>)AiStrats[idx]).Run(state, world);
    }

    public void Tick(float delta, float elapsed)
    {
    }

    private void ScheduleCountdownLogs()
    {
        var total = (int)MathF.Round(state.CountdownSeconds);
        if (total <= 0) return;
        for (var rem = total; rem >= 1; rem--)
        {
            if (rem == total || rem <= 5 || rem % 5 == 0)
            {
                var captured = rem;
                world.Events.Add(total - captured, () =>
                {
                    Plugin.ChatGui?.Print($"[绝妖星 P1] 战斗开始倒计时：{captured}秒");
                    DiagnosticLog.Info($"[UmadP1GI1] Pull countdown: {captured}s");
                });
            }
        }
        world.Events.Add(state.CountdownSeconds, () =>
        {
            Plugin.ChatGui?.Print("[绝妖星 P1] 战斗开始！");
            DiagnosticLog.Info("[UmadP1GI1] Pull countdown: ENGAGE!");
        });
    }

    private void CleanupObjects()
    {
        foreach (var helper in statueHelpers) helper.Despawn();
        statueHelpers.Clear();
        DespawnProp(ref statuePropUpper);
        DespawnProp(ref statuePropLower);
        DespawnProp(ref colossusProp);
        towerPositions.Clear();
    }

    private void DespawnProp(ref SimEventObject? prop)
    {
        if (prop == null) return;
        if (prop.IsAlive) prop.PlayBeat(EObjAnimDespawn.State, EObjAnimDespawn.Bitmask);
        prop.Despawn();
        prop = null;
    }

    private void SpawnInitialActors()
    {
        kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: Constants.BNpcBaseId.KefkaP3,
            NameId: Constants.BNpcNameId.Kefka,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            Visibility: SpawnVisibility.Visible,
            Placement: new Placement(Vector3.Zero, -float.Pi),
            LayoutId: Constants.BNpcLayoutId.KefkaP3));
        kefka?.KeepFacing(party.Get(PartyRole.MainTank));

        var propEventId = settingsWindow.Overrides.PropsBindDirector ? Constants.EObjId.PropEventId : 0u;
        statuePropUpper = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.EarlierStatueA,
            LayoutId = Constants.EObjId.EarlierStatueA1LayoutId,
            EventId = propEventId,
            EntityId = Constants.Spawn.EarlierStatueEntityIdBase,
            TargetableStatus = 5,
            Arg2 = 0x00420003U,
            Placement = new Placement(UpperStatuePos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        statuePropLower = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.EarlierStatueA,
            LayoutId = Constants.EObjId.EarlierStatueA2LayoutId,
            EventId = propEventId,
            EntityId = Constants.Spawn.EarlierStatueEntityIdBase + 1u,
            TargetableStatus = 5,
            Arg2 = 0x00410003U,
            Placement = new Placement(LowerStatuePos, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });
        colossusProp = world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = Constants.EObjId.GravenStatue,
            LayoutId = Constants.EObjId.GravenStatueLayoutId,
            EventId = propEventId,
            EntityId = 0x4000EB8Au,
            TargetableStatus = 5,
            Arg2 = Constants.EObjId.GravenStatueArg2,
            Placement = new Placement(Vector3.Zero, 0f),
            TimelineState = Constants.EObjId.GazeStatueSpawnState,
        });

        for (var i = 0; i < 4; i++)
        {
            if (SpawnGravenHelper(UpperStatuePos) is { } helper)
                statueHelpers.Add(helper);
        }
    }

    private SimEnemy? SpawnGravenHelper(Vector3 position)
        => world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: Constants.BNpcBaseId.GravenImage,
            NameId: Constants.BNpcNameId.GravenImage,
            Level: 100,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            Visibility: SpawnVisibility.InvisibleHelper,
            Placement: new Placement(position, 0f),
            LayoutId: Constants.BNpcLayoutId.GravenImage));

    private void TetherWaveBulletGroup()
    {
        var targets = state.DpsGetsTether ? UmadP1GravenImage1State.DpsRoles : UmadP1GravenImage1State.SupportRoles;
        for (var i = 0; i < targets.Length && i < statueHelpers.Count; i++)
        {
            if (party.Get(targets[i]) is { } member)
                world.Tether(statueHelpers[i], member, Constants.TetherId.GravenImage, duration: 5.08f);
        }
        DiagnosticLog.Info($"[UmadP1GI1] Tethered group={(state.DpsGetsTether ? "DPS" : "Support")}");
    }

    private void ResolveWaveBulletKnockback()
    {
        statuePropUpper?.PlayBeat(EObjAnimTetherFire.State, EObjAnimTetherFire.Bitmask);
        var targets = state.DpsGetsTether ? UmadP1GravenImage1State.DpsRoles : UmadP1GravenImage1State.SupportRoles;
        for (var i = 0; i < targets.Length && i < statueHelpers.Count; i++)
        {
            if (party.Get(targets[i]) is not { } member || !member.IsAlive()) continue;
            statueHelpers[i].Cast(UmadActions.WaveBullet, member);
            var away = new Vector2(member.Position.X - UpperStatuePos.X, member.Position.Z - UpperStatuePos.Z);
            away = away.LengthSquared() < 0.01f ? new Vector2(0f, 1f) : Vector2.Normalize(away);
            var dest2 = new Vector2(member.Position.X, member.Position.Z) + away * 10.0f;
            if (dest2.Length() > 18.5f)
                dest2 = Vector2.Normalize(dest2) * 18.5f;
            var dest = new Vector3(dest2.X, member.Position.Y, dest2.Y);
            var target = member;
            world.Events.Add(0.60f, () =>
            {
                if (target.IsAlive()) target.CarryTo(dest);
            });
        }
    }

    private void AttachFire1Markers()
    {
        foreach (var role in UmadP1GravenImage1State.AllRoles)
        {
            if (party.Get(role) is not { } member) continue;
            if (state.Fire1ShowsStackIcon)
            {
                if (role == state.Fire1StackSupport || role == state.Fire1StackDps)
                    member.AttachLockonVfx(Constants.LockonId.FireStack);
            }
            else
            {
                member.AttachLockonVfx(Constants.LockonId.FireSpread);
            }
        }
    }

    private void SpawnIceCones(bool diagonalB, bool isLie)
    {
        var realHeadings = diagonalB ? IcePairB : IcePairA;
        var fakeHeadings = diagonalB ? IcePairA : IcePairB;
        var realAction = isLie ? UmadActions.BlizzardBlowoutRealLie : UmadActions.BlizzardBlowoutRealTruth;

        foreach (var h in realHeadings)
            kefka?.CastFromHelper(realAction, at: new Placement(Vector3.Zero, h));

        if (!isLie) return;
        foreach (var h in fakeHeadings)
            kefka?.CastFromHelper(Constants.ActionId.BlizzardIIIBlowout_FakeOmen, at: new Placement(Vector3.Zero, h), animationLock: 1.1f);
    }

    private void ResolveFire1()
    {
        if (state.Fire1IsStack)
        {
            if (party.Get(state.Fire1StackSupport) is { } s && s.IsAlive())
                kefka?.CastFromHelper(UmadActions.FlagrantFireStack, s);
            if (party.Get(state.Fire1StackDps) is { } d && d.IsAlive())
                kefka?.CastFromHelper(UmadActions.FlagrantFireStack, d);
            return;
        }

        foreach (var role in UmadP1GravenImage1State.AllRoles)
        {
            if (party.Get(role) is { } member && member.IsAlive())
                kefka?.CastFromHelper(UmadActions.FlagrantFireSpread, member);
        }
    }

    private void ResolveWaveCannonLasers()
    {
        towerPositions.Clear();
        for (var i = 0; i < state.LaserTargets.Count && i < statueHelpers.Count; i++)
        {
            var role = state.LaserTargets[i];
            if (party.Get(role) is not { } target || !target.IsAlive()) continue;
            var heading = MathF.Atan2(target.Position.X - LowerStatuePos.X, target.Position.Z - LowerStatuePos.Z);
            statueHelpers[i].SetPosition(new Placement(LowerStatuePos, heading));
            statueHelpers[i].Cast(UmadActions.WaveCannonLaser, target);
            towerPositions.Add(new Vector3(target.Position.X, 0f, target.Position.Z));
            state.RecordTowerSpot(role, new Vector2(target.Position.X, target.Position.Z));
            DiagnosticLog.Info($"[UmadP1GI1] Wave Cannon laser -> {role} at ({target.Position.X:F1},{target.Position.Z:F1}) heading={heading:F3}");
        }
    }

    private void SpawnExplosionTowers()
    {
        foreach (var spot in towerPositions)
            kefka?.CastFromHelper(UmadActions.TowerExplosion, at: new Placement(spot, 0f));
    }

    private void ApplyTrap1Debuffs()
    {
        party.Get(state.Trap1Support)?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 5.0f);
        party.Get(state.Trap1Dps)?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 5.0f);
    }

    private void JudgeExplosionTowers()
    {
        var missed = 0;
        for (var i = 0; i < towerPositions.Count; i++)
        {
            var spot = towerPositions[i];
            var soakers = 0;
            foreach (var member in party.ActiveMembers())
            {
                if (!member.IsAlive()) continue;
                var dx = member.Position.X - spot.X;
                var dz = member.Position.Z - spot.Z;
                if (dx * dx + dz * dz <= TowerSoakRadius * TowerSoakRadius) soakers++;
            }
            if (soakers == 0)
            {
                missed++;
                DiagnosticLog.Warn($"[UmadP1GI1] Tower {i} at ({spot.X:F1},{spot.Z:F1}) went unsoaked.");
            }
        }

        if (missed == 0) return;
        kefka?.Cast(UmadActions.TowerBigBang);
        world.Fail($"Graven Image 1 tower unsoaked ({missed}/{towerPositions.Count} empty)");
    }

    private void SpawnThunderLines()
    {
        var realLine = state.ThunderIsLie ? UmadActions.ThrummingThunderReal2 : UmadActions.ThrummingThunderReal1;
        var anchors = UmadP1TeleTrouncingScenario.ThunderAnchors;
        for (var i = 0; i < anchors.Length; i++)
        {
            var real = (i + state.ThunderRealOffset) % 2 == 0;
            if (!real && !state.ThunderIsLie) continue;
            var placement = anchors[i].MulX(state.ThunderOrientation).MulRot(state.ThunderOrientation);
            if (real) kefka?.CastFromHelper(realLine, at: placement);
            else kefka?.CastFromHelper(Constants.ActionId.ThrummingThunderIII_FakeOmen, at: placement, animationLock: 1.1f);
        }
    }

    private void ResolveTrap1Stacks()
    {
        ResolveOneTrapStack(state.Trap1Support);
        ResolveOneTrapStack(state.Trap1Dps);
    }

    private void ResolveOneTrapStack(PartyRole holderRole)
    {
        if (party.Get(holderRole) is not { } holder || !holder.IsAlive()) return;
        kefka?.CastFromHelper(UmadActions.DoubleTroubleTrapStack, holder);

        SimCharacter? nextHolder = null;
        var bestDistSq = 36f;
        foreach (var member in party.ActiveMembers())
        {
            if (member == holder || !member.IsAlive()) continue;
            var dx = member.Position.X - holder.Position.X;
            var dz = member.Position.Z - holder.Position.Z;
            var dSq = dx * dx + dz * dz;
            if (dSq < bestDistSq)
            {
                bestDistSq = dSq;
                nextHolder = member;
            }
        }
        nextHolder?.AddStatus(Constants.StatusId.DoubleTroubleTrap, 68.0f);
    }
}