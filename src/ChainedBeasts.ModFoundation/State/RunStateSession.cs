using System;
using System.Collections.Generic;

namespace ChainedBeasts.ModFoundation.State;

/// <summary>Live state has one exact run identity; archives are owned by CheckpointStore.</summary>
public sealed class RunStateSession
{
    public string RunKey { get; private set; }
    public string BattleKey { get; private set; }
    public bool LegacyImported { get; set; }
    public Dictionary<int, PlayerContentState> Players { get; private set; } = new();
    public bool Start(string runKey)
    {
        if (RunKey == runKey) return false;
        Clear(); RunKey = runKey; return true;
    }
    public void Restore(CheckpointEnvelope envelope)
    {
        var copy = envelope.Clone();
        RunKey = copy.Identity.RunKey; Players = copy.Players; LegacyImported = copy.LegacyImported;
        BattleKey = null;
        foreach (var player in Players.Values)
        { BattleKey = player.GetFeature<string>("foundation:battle-key"); break; }
    }
    public bool EnterBattle(int completedLevels)
    {
        if (RunKey is null) throw new InvalidOperationException("No active run");
        var key = RunKey + "/level/" + completedLevels;
        if (BattleKey == key) return false;
        BattleKey = key; return true;
    }
    public void Clear()
    { RunKey = null; BattleKey = null; LegacyImported = false; Players = new(); }
}
