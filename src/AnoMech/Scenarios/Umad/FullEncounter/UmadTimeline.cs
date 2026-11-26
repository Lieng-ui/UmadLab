namespace AnoMech.Scenarios.Umad.FullEncounter;

/// <summary>
/// Start offset shared by the full-encounter scenarios: /umadenc p1 100 runs the P1 timeline from
/// its 100th second, which is what lets a real AnoMech mechanic be spliced into the middle of it.
/// </summary>
internal static class UmadTimeline
{
    public static float StartSec;

    // 0 = run the timeline to its own end; otherwise the segment stops here, which is how a
    // mechanic gets spliced into the middle of a phase instead of after it.
    public static float EndSec;

    public static void Reset() { StartSec = 0f; EndSec = 0f; }
}
