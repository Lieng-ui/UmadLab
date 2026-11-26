using System;
using AnoMech.Core.Game.Party;
using AnoMech.Scenarios.Umad.P5Celestriad;
using AnoMech.Scenarios.Umad.P5Exaflares;
using AnoMech.Scenarios.Umad.P5Flood;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Umad.P5Full;

public sealed class UmadP5FullSettingsWindow
{
    public UmadP5FullStateOverrides Overrides { get; } = new();

    private static readonly string[] DirectionLabels = ["随机", "正向", "反向"];
    private static readonly string[] FirstLabels = ["随机", "东北-西南先", "西北-东南先"];
    private static readonly string[] AnchorLabels = ["每波随机", "H1", "H2", "D1", "D2", "D3", "D4"];
    private static readonly PartyRole[] AnchorRoles =
    [
        PartyRole.RegenHealer, PartyRole.ShieldHealer,
        PartyRole.MeleeDpsA, PartyRole.MeleeDpsB, PartyRole.PhysRangedDps, PartyRole.CasterDps,
    ];
    private static readonly string[] QuadrantLabels = ["自动推导", "北", "东", "南", "西"];
    private static readonly string[] RotationLabels = ["自动推导", "顺时针", "逆时针"];

    private static readonly string[] DebuffLabels = ["随机", "火", "冰", "雷", "无属性 (自由人)"];
    private static readonly string[] VariantLabels = ["随机", "风 (绿/靠近场中)", "土 (棕/远离场中)"];
    private static readonly string[] LitTowerLabels = ["随机", "单塔中/双塔外"];
    private static readonly string[] OrderLabels =
    [
        "随机",
        "火-冰-雷", "火-雷-冰",
        "冰-火-雷", "冰-雷-火",
        "雷-火-冰", "雷-冰-火",
    ];
    private static readonly string[] ExaOrderLabels =
    [
        "随机",
        "14 -> 25 -> 36", "14 -> 36 -> 25",
        "25 -> 14 -> 36", "25 -> 36 -> 14",
        "36 -> 14 -> 25", "36 -> 25 -> 14",
    ];

    public void Draw()
    {
        if (ImGui.Button("重置默认##umadp5full"))
        {
            Overrides.CountdownSeconds = 5.0f;
            Overrides.ClearTimeSec = 205.0f;
            Overrides.PlayOpeningTransitionWhenSolo = false;
            Overrides.Flood.LineNeSw = FloodDirection.Random;
            Overrides.Flood.LineNwSe = FloodDirection.Random;
            Overrides.Flood.NeSwFirst = null;
            Overrides.Flood.AnchorRole = null;
            Overrides.Flood.StartQuadrant = null;
            Overrides.Flood.RotationClockwise = null;
            Overrides.Celestriad.DoubleOrder = null;
            Overrides.Celestriad.SectorOrder = null;
            Overrides.Celestriad.FixedLitTowers = false;
            Overrides.Celestriad.Set1 = CatastrophicVariantOverride.Random;
            Overrides.Celestriad.Set3 = CatastrophicVariantOverride.Random;
            Overrides.Celestriad.Debuff.Mine = null;
            Overrides.Exaflares.LeftOrder = ExaFlareOrder.Random;
            Overrides.Exaflares.RightOrder = ExaFlareOrder.Random;
        }

        var cd = Overrides.CountdownSeconds;
        if (ImGui.SliderFloat("开怪倒数 (秒)##umadp5full_cd", ref cd, 0f, 15f, "%.0f 秒"))
            Overrides.CountdownSeconds = MathF.Round(cd);

        var clearSec = Overrides.ClearTimeSec;
        if (ImGui.SliderFloat("过本击杀时间 (秒)##umadp5full_clear", ref clearSec, 160f, 220f, "%.1f 秒"))
            Overrides.ClearTimeSec = clearSec;

        var intro = Overrides.PlayOpeningTransitionWhenSolo;
        if (ImGui.Checkbox("单练 P5 时播放 30 秒开场转场##umadp5full_intro", ref intro))
            Overrides.PlayOpeningTransitionWhenSolo = intro;

        if (ImGui.CollapsingHeader("洪水机制覆写##umadp5full_flood"))
        {
            var neSw = (int)Overrides.Flood.LineNeSw;
            if (ImGui.Combo("东北-西南线##p5full_nesw", ref neSw, DirectionLabels, DirectionLabels.Length))
                Overrides.Flood.LineNeSw = (FloodDirection)neSw;

            var nwSe = (int)Overrides.Flood.LineNwSe;
            if (ImGui.Combo("西北-东南线##p5full_nwse", ref nwSe, DirectionLabels, DirectionLabels.Length))
                Overrides.Flood.LineNwSe = (FloodDirection)nwSe;

            var firstIdx = Overrides.Flood.NeSwFirst switch { true => 1, false => 2, null => 0 };
            if (ImGui.Combo("先手对角线##p5full_first", ref firstIdx, FirstLabels, FirstLabels.Length))
                Overrides.Flood.NeSwFirst = firstIdx switch { 1 => true, 2 => false, _ => null };

            var anchorIdx = Overrides.Flood.AnchorRole is { } r ? Array.IndexOf(AnchorRoles, r) + 1 : 0;
            if (ImGui.Combo("分摊目标##p5full_anchor", ref anchorIdx, AnchorLabels, AnchorLabels.Length))
                Overrides.Flood.AnchorRole = anchorIdx == 0 ? null : AnchorRoles[anchorIdx - 1];

            var quadIdx = (Overrides.Flood.StartQuadrant ?? -1) + 1;
            if (ImGui.Combo("起始安全区##p5full_quad", ref quadIdx, QuadrantLabels, QuadrantLabels.Length))
                Overrides.Flood.StartQuadrant = quadIdx == 0 ? null : quadIdx - 1;

            var rotIdx = Overrides.Flood.RotationClockwise switch { true => 1, false => 2, null => 0 };
            if (ImGui.Combo("旋转方向##p5full_rot", ref rotIdx, RotationLabels, RotationLabels.Length))
                Overrides.Flood.RotationClockwise = rotIdx switch { 1 => true, 2 => false, _ => null };
        }

        if (ImGui.CollapsingHeader("三星机制覆写##umadp5full_cel"))
        {
            var myDebuff = Overrides.Celestriad.Debuff.Mine is { } d ? (int)d + 1 : 0;
            if (ImGui.Combo("自身属性 Debuff##p5full_cel_debuff", ref myDebuff, DebuffLabels, DebuffLabels.Length))
                Overrides.Celestriad.Debuff.Mine = myDebuff == 0 ? null : (CelestriadDebuff)(myDebuff - 1);

            var dblIdx = Overrides.Celestriad.DoubleOrder is { } doVal ? (int)doVal + 1 : 0;
            if (ImGui.Combo("双塔属性顺序##p5full_cel_dbl", ref dblIdx, OrderLabels, OrderLabels.Length))
                Overrides.Celestriad.DoubleOrder = dblIdx == 0 ? null : (CelestriadElementOrder)(dblIdx - 1);

            var secIdx = Overrides.Celestriad.SectorOrder is { } soVal ? (int)soVal + 1 : 0;
            if (ImGui.Combo("扇区顺时针排布##p5full_cel_sec", ref secIdx, OrderLabels, OrderLabels.Length))
                Overrides.Celestriad.SectorOrder = secIdx == 0 ? null : (CelestriadElementOrder)(secIdx - 1);

            var litIdx = Overrides.Celestriad.FixedLitTowers ? 1 : 0;
            if (ImGui.Combo("亮塔位置##p5full_cel_lit", ref litIdx, LitTowerLabels, LitTowerLabels.Length))
                Overrides.Celestriad.FixedLitTowers = litIdx == 1;

            var s1 = (int)Overrides.Celestriad.Set1;
            if (ImGui.Combo("第 1 轮二选一##p5full_cel_s1", ref s1, VariantLabels, VariantLabels.Length))
                Overrides.Celestriad.Set1 = (CatastrophicVariantOverride)s1;

            var s3 = (int)Overrides.Celestriad.Set3;
            if (ImGui.Combo("第 3 轮二选一##p5full_cel_s3", ref s3, VariantLabels, VariantLabels.Length))
                Overrides.Celestriad.Set3 = (CatastrophicVariantOverride)s3;
        }

        if (ImGui.CollapsingHeader("地火机制覆写##umadp5full_exa"))
        {
            var leftIdx = (int)Overrides.Exaflares.LeftOrder;
            if (ImGui.Combo("左侧地火顺序##p5full_exa_left", ref leftIdx, ExaOrderLabels, ExaOrderLabels.Length))
                Overrides.Exaflares.LeftOrder = (ExaFlareOrder)leftIdx;

            var rightIdx = (int)Overrides.Exaflares.RightOrder;
            if (ImGui.Combo("右侧地火顺序##p5full_exa_right", ref rightIdx, ExaOrderLabels, ExaOrderLabels.Length))
                Overrides.Exaflares.RightOrder = (ExaFlareOrder)rightIdx;
        }
    }
}
