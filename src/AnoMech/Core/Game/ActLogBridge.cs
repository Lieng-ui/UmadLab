using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;

namespace AnoMech.Core.Game;

// Optional local Triggernometry Endpoint bridge (HTTP POST to localhost) that forwards UmadLab
// simulated events as Base64-batched lines to UmadLab_Endpoint_Bridge.xml inside Triggernometry.
// Never touches game network sockets and never requires an extra ACT .cs/.dll plugin.
public static class ActLogBridge
{
    public const string DefaultEndpointUrl = "http://localhost:51423/";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly ConcurrentQueue<string> Queue = new();
    private static readonly AutoResetEvent Signal = new(false);
    private static readonly object LifecycleLock = new();
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMilliseconds(1500) };

    private static Thread? worker;
    private static volatile bool running;
    private static volatile bool connected;
    private static long sentCount;

    public static bool Enabled => Plugin.Config?.EnableActBridge ?? false;
    public static bool IsConnected => connected;
    public static long SentCount => Interlocked.Read(ref sentCount);

    private static string EndpointUrl
    {
        get
        {
            var raw = Plugin.Config?.ActBridgeEndpointUrl?.Trim();
            return string.IsNullOrEmpty(raw) ? DefaultEndpointUrl : raw;
        }
    }

    public static void Start()
    {
        lock (LifecycleLock)
        {
            if (running) return;
            running = true;
            worker = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "UmadLab.ActEndpointBridge",
            };
            worker.Start();
        }
    }

    public static void Stop()
    {
        lock (LifecycleLock)
        {
            if (!running) return;
            running = false;
            Signal.Set();
            if (worker is { IsAlive: true })
                worker.Join(500);
            worker = null;
            connected = false;
            while (Queue.TryDequeue(out _)) { }
        }
    }

    private static void Enqueue(string payload)
    {
        if (!Enabled) return;
        if (!running) Start();
        if (Queue.Count > 4096) return;
        var ts = DateTime.Now.ToString("HH:mm:ss.fff", Inv);
        Queue.Enqueue($"[{ts}] {payload}");
        Signal.Set();
    }

    private static void EnqueueRaw(string rawLine)
    {
        if (!Enabled) return;
        if (!running) Start();
        if (Queue.Count > 4096) return;
        Queue.Enqueue(rawLine);
        Signal.Set();
    }

    private static void WorkerLoop()
    {
        while (running)
        {
            if (!Enabled)
            {
                connected = false;
                Signal.WaitOne(500);
                continue;
            }

            if (!Queue.TryDequeue(out var line))
            {
                Signal.WaitOne(100);
                continue;
            }

            try
            {
                var body = line.StartsWith("@@UMADLAB_PARTY|", StringComparison.Ordinal)
                    ? line
                    : "UMADLAB_LINE:" + line;
                using var content = new StringContent(body, Encoding.UTF8, "text/plain");
                using var resp = Http.PostAsync(EndpointUrl, content).GetAwaiter().GetResult();
                if (resp.IsSuccessStatusCode)
                {
                    connected = true;
                    Interlocked.Increment(ref sentCount);
                }
                else
                {
                    connected = false;
                    Thread.Sleep(400);
                }
            }
            catch
            {
                connected = false;
                Thread.Sleep(500);
            }
        }
    }

    private static string F2(float v) => v.ToString("F2", Inv);
    private static string F3(float v) => v.ToString("F3", Inv);
    private static string F4(float v) => v.ToString("F4", Inv);

    public static void EmitZoneEnter(uint territoryId, string zoneName)
    {
        if (!Enabled) return;
        var safeName = string.IsNullOrEmpty(zoneName) ? "妖星乱舞绝境战" : zoneName;
        Enqueue($"ChangeZone 01:{territoryId:X4}:{safeName}");
        Enqueue($"ChatLog 00:0038::zone {territoryId}");
        Enqueue($"ZoneChange 1001:{territoryId}:{safeName}:0:旅馆");
    }

    public static void EmitZoneExit(uint prevTerritoryId, string prevZoneName)
    {
        if (!Enabled) return;
        var safeName = string.IsNullOrEmpty(prevZoneName) ? "妖星乱舞绝境战" : prevZoneName;
        Enqueue($"ZoneChange 1001:0:旅馆:{prevTerritoryId}:{safeName}");
    }

    public static void EmitPartySync(PartyRole playerRole, IReadOnlyList<SimCharacter?> slots)
    {
        if (!Enabled) return;
        var myIdx = Math.Clamp((int)playerRole + 1, 1, 8);
        var ids = new string[8];
        for (var i = 0; i < 8; i++)
        {
            var eid = (i < slots.Count ? (slots[i] is SimNpc npc ? npc.ActEntityId : slots[i]?.EntityId) : null) ?? 0u;
            ids[i] = eid != 0 ? $"{eid:X8}" : $"10{(i + 1) * 111111}";
        }
        EnqueueRaw($"@@UMADLAB_PARTY|{myIdx}|{string.Join(",", ids)}");
    }

    public static void EmitCombatStart()
    {
        if (!Enabled) return;
        Enqueue("InCombat 104:1:1:1:1");
    }

    public static void EmitRunEnd(bool victory)
    {
        if (!Enabled) return;
        var code = victory ? "40000003" : "40000005";
        Enqueue($"DirectorUpdate 21:800375D2:{code}:00:00:00:00");
    }

    public static void EmitAddCombatant(uint entityId, string name, uint bNpcBaseId, Vector3 globalPos, float heading)
    {
        if (!Enabled || entityId == 0 || bNpcBaseId == 19512) return;
        Enqueue($"AddCombatant 03:{entityId:X8}:{name}:00:64:0000:00::0:{bNpcBaseId}:188300:188300:10000:10000:::{F2(globalPos.X)}:{F2(globalPos.Z)}:{F2(globalPos.Y)}:{F2(heading)}");
    }

    public static void EmitRemoveCombatant(uint entityId, string name, uint bNpcBaseId, Vector3 globalPos, float heading)
    {
        if (!Enabled || entityId == 0) return;
        Enqueue($"RemoveCombatant 04:{entityId:X8}:{name}:00:64:0000:00::0:{bNpcBaseId}:188300:188300:10000:10000:::{F2(globalPos.X)}:{F2(globalPos.Z)}:{F2(globalPos.Y)}:{F2(heading)}");
    }

    public static void EmitActorSetPos(uint entityId, string name, uint bNpcBaseId, Vector3 globalPos, float heading)
    {
        if (!Enabled || entityId == 0) return;
        Enqueue($"_ActorSetPos 110F:{entityId:X8}:{name}:0:{bNpcBaseId}:{F4(globalPos.X)}:{F4(globalPos.Z)}:{F4(globalPos.Y)}:{F4(heading)}");
    }

    public static void EmitStartsCasting(
        uint sourceId, string sourceName, uint actionId, string actionName,
        uint targetId, string targetName, float castSeconds, Vector3 globalPos, float heading)
    {
        if (!Enabled || sourceId == 0) return;
        var tid = targetId != 0 ? targetId : sourceId;
        var tname = string.IsNullOrEmpty(targetName) ? sourceName : targetName;
        Enqueue($"StartsCasting 14:{sourceId:X8}:{sourceName}:{actionId:X}:{actionName}:{tid:X8}:{tname}:{F3(castSeconds)}:{F2(globalPos.X)}:{F2(globalPos.Z)}:{F2(globalPos.Y)}:{F2(heading)}");
        var pos107 = actionId switch
        {
            0xBACDu => new Vector3(globalPos.X - 20f, globalPos.Y, globalPos.Z + 40f),
            0xBACEu => new Vector3(globalPos.X + 20f, globalPos.Y, globalPos.Z + 40f),
            _ => globalPos,
        };
        var h107 = actionId is 0xBACDu or 0xBACEu ? MathF.PI : heading;
        Enqueue($"263 107:{sourceId:X8}:{actionId:X}:{F3(pos107.X)}:{F3(pos107.Z)}:{F3(pos107.Y)}:{F3(h107)}");
    }

    public static void EmitActionEffect(
        uint sourceId, string sourceName, uint actionId, string actionName,
        Vector3 globalPos, float heading, uint primaryTargetId, IReadOnlyList<ActionEffectTarget>? targets)
    {
        if (!Enabled || sourceId == 0) return;
        var tid108 = primaryTargetId != 0 ? primaryTargetId : sourceId;
        Enqueue($"264 108:{sourceId:X8}:{actionId:X}:00000000:1:{F3(globalPos.X)}:{F3(globalPos.Z)}:{F3(globalPos.Y)}:{F3(heading)}:{tid108:X8}");

        if (targets is not { Count: > 0 }) return;
        var code = targets.Count > 1 ? "16" : "15";
        for (var i = 0; i < targets.Count; i++)
        {
            var tid = (uint)targets[i].Id.ObjectId;
            if (tid == 0) continue;
            Enqueue(
                $"ActionEffect {code}:{sourceId:X8}:{sourceName}:{actionId:X}:{actionName}:{tid:X8}:Target"
                + ":750003:0:0:0:0:0:0:0:0:0:0:0:0:0:0:0"
                + ":200000:200000:10000:10000:::100.00:100.00:0.00:0.00"
                + $":188300:188300:10000:10000:::{F2(globalPos.X)}:{F2(globalPos.Z)}:{F2(globalPos.Y)}:{F2(heading)}"
                + $":00000001:{i}:{targets.Count}:00::01:{actionId:X}:{actionId:X}:1.000:0");
        }
    }

    public static void EmitStatusAdd(ushort statusId, float duration, uint sourceId, uint targetId, ushort param)
    {
        if (!Enabled || targetId == 0) return;
        var sid = sourceId != 0 ? sourceId : 0xE0000000u;
        var dur = duration > 0f ? duration : 9999.00f;
        var rawName = Natives.Data.Status(statusId)?.Name;
        var name = string.IsNullOrEmpty(rawName) ? $"Status_{statusId:X}" : rawName;
        Enqueue($"StatusAdd 1A:{statusId:X}:{name}:{F2(dur)}:{sid:X8}::{targetId:X8}:Target:{param:X}:200000:0");
    }

    public static void EmitStatusRemove(ushort statusId, uint sourceId, uint targetId)
    {
        if (!Enabled || targetId == 0) return;
        var sid = sourceId != 0 ? sourceId : 0xE0000000u;
        var rawName = Natives.Data.Status(statusId)?.Name;
        var name = string.IsNullOrEmpty(rawName) ? $"Status_{statusId:X}" : rawName;
        Enqueue($"StatusRemove 1E:{statusId:X}:{name}:0.00:{sid:X8}::{targetId:X8}:Target:00:200000:0");
    }

    public static void EmitTargetIcon(uint targetId, string targetName, uint lockonId)
    {
        if (!Enabled || targetId == 0) return;
        Enqueue($"TargetIcon 1B:{targetId:X8}:{targetName}:0000:0000:{lockonId:X4}:{targetId:X8}:0000:0000");
    }

    public static void EmitSignMarker(Sign sign, uint sourceId, uint targetId)
    {
        if (!Enabled || targetId == 0) return;
        Enqueue($"SignMarker 1D:Add:{(int)sign}:{sourceId:X8}:Player:{targetId:X8}:Target");
    }

    public static void EmitTether(uint sourceId, uint targetId, ushort tetherId)
    {
        if (!Enabled || sourceId == 0 || targetId == 0) return;
        Enqueue($"Tether 23:{sourceId:X8}:Source:{targetId:X8}:Target:0000:0000:{tetherId:X4}:00000000:00000000:00000000");
    }

    public static void EmitBlackHoleSpawnExtra(uint entityId, Vector3 globalPos, ushort tetherId = 0x0054)
    {
        if (!Enabled || entityId == 0) return;
        Enqueue($"_ActorSpawnExtra 1110:{entityId:X8}:黑洞:8343:19512:{F3(globalPos.X)}:{F3(globalPos.Z)}:{F3(globalPos.Y)}:0.0000:E0000000:{tetherId:X4}:00");
    }

    public static void EmitSpawnEObj(uint entityId, uint eobjId, Vector3 globalPos, float heading)
    {
        if (!Enabled || entityId == 0 || eobjId is 0x1EBFB2u or 0x1EBFB3u) return;
        Enqueue($"_SpawnObject 1105:{entityId:X8}::{eobjId:X}:{F3(globalPos.X)}:{F3(globalPos.Z)}:{F3(globalPos.Y)}:{F3(heading)}:7");
    }

    public static void EmitEObjAnimation(uint entityId, uint eobjId, Vector3 globalPos, float heading, uint state, uint bitmask)
    {
        if (!Enabled || entityId == 0) return;
        Enqueue($"_EObjAnimation 1019D:{entityId:X8}::0:{eobjId}:{F4(globalPos.X)}:{F4(globalPos.Z)}:{F4(globalPos.Y)}:{F4(heading)}:{state:X}:{bitmask:X}:0:0");
    }

    public static void EmitMapEffect(uint packetFlags, byte index)
    {
        if (!Enabled) return;
        Enqueue($"MapEffect 101:800375D2:{packetFlags:X8}:{index:X2}:00");
    }

    public static void EmitDirectorUpdate(uint category, uint arg1, uint arg2, uint arg3, uint arg4)
    {
        if (!Enabled) return;
        Enqueue($"DirectorUpdate 21:800375D2:{category:X8}:{arg1:X2}:{arg2:X2}:{arg3:X2}:{arg4:X2}");
    }
}
