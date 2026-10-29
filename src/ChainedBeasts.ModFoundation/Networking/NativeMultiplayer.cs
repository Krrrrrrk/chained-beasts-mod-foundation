using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using ChainedBeasts.ModFoundation.Runtime;
using ChainedBeasts.ModFoundation.Logic;
using ChainedBeasts.ModFoundation.State;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Allocator = Unity.Collections.Allocator;
using Unity.Netcode;
using UnityEngine;

namespace ChainedBeasts.ModFoundation.Networking;

/// <summary>All peers install the same gameplay plugins. NGO remains the sole transport and HP authority.</summary>
public static class MultiplayerServices
{
    private static readonly Dictionary<string, string> Components = new(StringComparer.Ordinal);
    internal static readonly Dictionary<string, (bool Outgoing, int Maximum)> GoldRules = new(StringComparer.Ordinal);
    internal static readonly Dictionary<string, (double MaximumFraction, bool RequiresKill)> HealthRules = new(StringComparer.Ordinal);
    public static void RegisterHealthGrantRule(string key, double maximumFraction, bool requiresKill)
    {
        if (!double.IsFinite(maximumFraction) || maximumFraction <= 0 || maximumFraction > .25 || !FoundationServices.Content.TryGet(key, out _))
            throw new ArgumentException("Invalid health grant rule.");
        HealthRules.Add(key, (maximumFraction, requiresKill));
    }
    public static bool Ready => NativeMultiplayer.Ready;
    public static bool IsHost => NetworkManager.Singleton?.IsServer == true;
    public static void RegisterCompatibility(string guid, string version, string mechanics)
    {
        if (!Components.TryAdd(guid, guid + "/" + version + "/" + mechanics)) throw new InvalidOperationException("Duplicate multiplayer compatibility owner.");
    }
    /// <summary>Include compiled gameplay providers, so same-version local edits cannot silently differ.</summary>
    public static void RegisterBinary(System.Reflection.Assembly assembly)
    {
        var name = assembly.GetName().Name;
        RegisterCompatibility("binary:" + name, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location))), "sha256");
    }
    public static void RegisterGoldRule(string contentKey, bool outgoing, int maximum)
    {
        if (maximum is < 1 or > 3 || !FoundationServices.Content.TryGet(contentKey, out _)) throw new ArgumentException("Invalid registered gold rule.");
        GoldRules.Add(contentKey, (outgoing, maximum));
    }
    internal static string Fingerprint() => MultiplayerCodec.Fingerprint(Components.Values.Concat(
        FoundationServices.Content.Entries.Select(x => $"{x.Key}/{x.Category}/{x.NumericId}/{x.MaximumLevel}/{x.Enabled}"))
        .Append("foundation/" + Plugin.PluginVersion));
    public static bool IsPlayerOwner(PlayerData data)
    {
        try { return data is not null && NetworkSession.InstanceExists && NetworkSession.Instance.IsLocalClient(NetworkSession.Instance.GetClientByPlayerIndex((ulong)data.playerID)); }
        catch { return false; }
    }
    public static bool CapturePlayer(PlayerData data) => SessionAccess.IsSoloMode || IsPlayerOwner(data);
    public static void RequestGold(Player player, string key, int delta) => NativeMultiplayer.RequestGold(player, key, delta);
    public static void PublishSharedMoney() => NativeMultiplayer.PublishMoney();
    public static bool LedgerPaid(int player) => NativeMultiplayer.LedgerBattle == NativePersistence.BattleKey && NativeMultiplayer.Ledger.Contains(player);
    public static void SetLedgerPaid(int player, bool paid) { if (IsHost && paid) NativeMultiplayer.Ledger.Add(player); }
}

internal static class NativeMultiplayer
{
    private const string Channel = "local.chainedbeasts.foundation/v1";
    private static NetworkManager _manager;
    private static CustomMessagingManager.HandleNamedMessageDelegate _handler;
    private static readonly Dictionary<ulong, double> Waiting = new();
    private static readonly HashSet<ulong> Matched = new();
    private static string _session, _fingerprint, _offerJson, _offerDigest;
    private static bool _ready;
    private static bool _arming, _faulted;
    private static readonly ReadyBarrier ReadyAcks = new();
    private static readonly HashSet<string> Errors = new();
    private static double _helloAt, _nextTick;
    private static double _moneyAt;
    private static long _sequence;
    private static readonly SequenceWindow MoneySequence = new();
    internal static readonly HashSet<int> Ledger = new();
    internal static string LedgerBattle;
    internal static bool Ready
    {
        get
        {
            if (!_ready || _manager == null || !_manager.IsListening) return false;
            if (!_manager.IsServer) return true;
            // A newly connected peer must block Start even before the next
            // quarter-second handshake tick or its first hello packet.
            if (!ReadyAcks.Complete) return false;
            var clients = _manager.ConnectedClientsIds;
            var count = clients.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<ulong>>().Count;
            for (var i = 0; i < count; i++) if (clients[i] != _manager.LocalClientId && !Matched.Contains(clients[i])) return false;
            return true;
        }
    }
    internal static bool Online => GameManager.InstanceExists && GameManager.Instance.CurrentGameMode is GameMode.Online or GameMode.Local;
    internal static event Action<ulong, MultiplayerPacket> Message;

    internal static void VerifyGame()
    {
        using var stream = File.OpenRead(Path.Combine(BepInEx.Paths.GameRootPath, "GameAssembly.dll"));
        using var hash = SHA256.Create();
        var digest = Convert.ToHexString(hash.ComputeHash(stream)).ToLowerInvariant();
        if (digest != "ba3ef02fa87da4c41bb27d7610539c9578132204f0fb5713833e2ef93415ce29")
            throw new NotSupportedException("Uninspected game build; multiplayer hooks disabled. GameAssembly SHA256=" + digest);
        MultiplayerServices.RegisterCompatibility("native-game", digest, "Steam-26231777");
    }

    internal static void Install(Harmony harmony)
    {
        MultiplayerServices.RegisterBinary(typeof(Plugin).Assembly);
        FeaturePatcher.Patch(harmony, typeof(NativeMultiplayer), FeaturePatcher.RequireMethod(typeof(RunDataManager), nameof(RunDataManager.StartNewRunRpc),
            typeof(int), typeof(int), typeof(AscensionLevel), typeof(DifficultyConfig), typeof(int)), prefix: nameof(CanStart));
        FoundationServices.BattleStarted += key => { if (MultiplayerServices.IsHost) { Ledger.Clear(); LedgerBattle = key; PublishMoney(); } GoldClaims.Clear(); GoldStates.Clear(); };
        FoundationServices.RunReset += () => { Ledger.Clear(); LedgerBattle = null; GoldClaims.Clear(); GoldStates.Clear(); GoldFeedback.Clear(); };
        FoundationServices.CheckpointValidating += snapshot =>
        {
            foreach (var state in snapshot.Players.Values)
                if (state.GetFeature<HostGoldSnapshot>("foundation:gold-guard") is { } guard) HostGoldGuard.Restore(guard, 0);
        };
    }
    private static bool CanStart(RunDataManager __instance)
    {
        if (!Online || Ready || __instance.__rpc_exec_stage == NetworkBehaviour.__RpcExecStage.Execute) return true;
        FoundationServices.Logger?.LogWarning("[Multiplayer] start blocked until every connected player has matching Foundation/content versions and settings");
        return false;
    }
    internal static void Tick()
    {
        if (_faulted) return;
        try { TickCore(); }
        catch (Exception e) { _ready = false; _faulted = true; FoundationServices.Logger?.LogError("[Multiplayer] transport disabled for this launch: " + e); }
    }
    private static void TickCore()
    {
        var manager = NetworkManager.Singleton;
        if (!Online || manager == null || !manager.IsListening)
        { if (_manager is not null) Reset(); return; }
        if (_manager?.Pointer != manager.Pointer)
        {
            if (manager.CustomMessagingManager is null) return;
            Reset(); _manager = manager; _fingerprint = MultiplayerServices.Fingerprint();
            _session = manager.IsServer ? Guid.NewGuid().ToString("N") : null;
            if (manager.IsServer)
            {
                ContentOfferServices.FreezeHost();
                _offerJson = ContentOfferServices.Snapshot();
                _offerDigest = ContentOfferServices.Digest(_offerJson);
            }
            _handler = (Action<ulong, FastBufferReader>)Receive;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(Channel, _handler);
            FoundationServices.Logger?.LogInfo("[Multiplayer] transport ready; checking peer compatibility " + _fingerprint);
        }
        var now = Time.unscaledTimeAsDouble;
        if (now < _nextTick) return; _nextTick = now + .25;
        if (!manager.IsServer)
        {
            if (!_ready && now >= _helloAt) { Send(NetworkManager.ServerClientId, new() { Kind = "hello", Fingerprint = _fingerprint }); _helloAt = now + 1; }
            return;
        }
        var clients = manager.ConnectedClientsIds;
        var live = new HashSet<ulong>();
        var count = clients.Cast<Il2CppSystem.Collections.Generic.IReadOnlyCollection<ulong>>().Count;
        for (var i = 0; i < count; i++) if (clients[i] != manager.LocalClientId) live.Add(clients[i]);
        Matched.RemoveWhere(id => !live.Contains(id));
        ReadyAcks.Retain(live);
        var rejectedLate = new HashSet<ulong>();
        foreach (var id in Waiting.Keys.Where(id => !live.Contains(id)).ToArray()) Waiting.Remove(id);
        foreach (var id in live)
        {
            if (!Matched.Contains(id) && NetworkSession.InstanceExists && NetworkSession.Instance.runDataManager?.runStartTime > 0)
            { rejectedLate.Add(id); manager.DisconnectClient(id, "Join the modded party before starting the run. Mid-run joining is not supported."); continue; }
            if (!Matched.Contains(id) && !Waiting.ContainsKey(id)) Waiting[id] = now;
            if (Waiting.TryGetValue(id, out var started) && now - started > 20)
            { manager.DisconnectClient(id, "Install matching Chained Beasts Foundation/content mods on every player."); Waiting.Remove(id); }
        }
        // Peers acknowledge ready before the host can send StartNewRunRpc.
        // This avoids a native start arriving before a client's mod-ready flag.
        var allMatched = live.Where(id => !rejectedLate.Contains(id)).All(Matched.Contains);
        if (!allMatched)
        {
            if (_ready || _arming) foreach (var id in Matched) Send(id, new() { Kind = "waiting", Fingerprint = _fingerprint });
            _ready = false; _arming = false; ReadyAcks.Clear();
        }
        else if (!_ready)
        {
            if (!_arming)
            {
                ReadyAcks.Begin(Matched);
                _arming = true; foreach (var id in Matched) Send(id, new() { Kind = "ready", Fingerprint = _fingerprint, Nonce = _offerDigest });
            }
            if (ReadyAcks.Complete) { _ready = true; FoundationServices.Logger?.LogInfo("[Multiplayer] party compatibility confirmed; host and clients ready"); }
        }
        if (_ready && now >= _moneyAt) { _moneyAt = now + 1; PublishMoney(); }
    }
    private static void Reset()
    {
        try { _manager?.CustomMessagingManager?.UnregisterNamedMessageHandler(Channel); } catch { }
        _manager = null; _handler = null; _ready = false; _session = null; Waiting.Clear(); Matched.Clear(); MoneySequence.Clear(); Ledger.Clear(); GoldClaims.Clear(); GoldStates.Clear(); GoldFeedback.Clear();
        _helloAt = _nextTick = _moneyAt = 0; _sequence = 0; NetworkCombat.Reset(); NetworkCheckpoints.ResetSession(); NativeHealthGrants.ResetSession();
        _arming = false; ReadyAcks.Clear(); LedgerBattle = null;
        _offerJson = _offerDigest = null; ContentOfferServices.ResetSession();
    }
    internal static void Send(ulong client, MultiplayerPacket packet)
    {
        if (_manager == null || !_manager.IsListening) return;
        packet.Session = _session;
        var bytes = MultiplayerCodec.Encode(packet);
        var writer = new FastBufferWriter(bytes.Length, Allocator.Temp, bytes.Length);
        try { writer.WriteBytesSafe((Il2CppStructArray<byte>)bytes); _manager.CustomMessagingManager.SendNamedMessage(Channel, client, writer, NetworkDelivery.ReliableFragmentedSequenced); }
        finally { writer.Dispose(); }
    }
    internal static void Broadcast(MultiplayerPacket packet)
    { foreach (var id in Matched.ToArray()) Send(id, packet); }
    internal static bool IsMatched(ulong sender) => _manager?.IsServer == true && Matched.Contains(sender);
    internal static ulong[] Peers => Matched.ToArray();
    internal static void DisconnectPeer(ulong id, string reason) { if (_manager?.IsServer == true) { _manager.DisconnectClient(id, reason); Matched.Remove(id); Waiting.Remove(id); } }
    private static void Receive(ulong sender, FastBufferReader reader)
    {
        try
        {
            var length = reader.Length - reader.Position;
            if (length < 1 || length > MultiplayerCodec.MaximumBytes || !reader.TryBeginRead(length)) return;
            var data = new Il2CppStructArray<byte>(length); reader.ReadBytesSafe(ref data, length);
            var packet = MultiplayerCodec.Decode(data.ToArray());
            if (_manager.IsServer)
            {
                if (sender == _manager.LocalClientId) return;
                if (packet.Kind == "hello")
                {
                    if (!Matched.Contains(sender) && NetworkSession.InstanceExists && NetworkSession.Instance.runDataManager?.runStartTime > 0)
                    { _manager.DisconnectClient(sender, "Join the modded lobby before starting the run. Mid-run joining is not supported."); return; }
                    if (packet.Fingerprint != _fingerprint) { _manager.DisconnectClient(sender, "Chained Beasts mod versions, enabled content or prices differ from host."); return; }
                    if (!Matched.Contains(sender) && !Waiting.ContainsKey(sender)) Waiting[sender] = Time.unscaledTimeAsDouble;
                    if (!Matched.Contains(sender)) { _ready = false; _arming = false; ReadyAcks.Clear(); }
                    Send(sender, new() { Kind = "accepted", Fingerprint = _fingerprint, Json = _offerJson, Nonce = _offerDigest });
                    return;
                }
                if (packet.Kind == "settings-ack")
                {
                    if (!OfferSelectionAcknowledgement.Matches(_session, _fingerprint, _offerDigest, packet) ||
                        (!Waiting.ContainsKey(sender) && !Matched.Contains(sender))) return;
                    if (Matched.Add(sender))
                    {
                        _ready = false; _arming = false; ReadyAcks.Clear();
                        FoundationServices.Logger?.LogInfo("[Multiplayer] peer acknowledged host offer selection: " + sender);
                    }
                    Waiting.Remove(sender);
                    return;
                }
                if (!Matched.Contains(sender) || packet.Session != _session) return;
                if (packet.Kind == "ready-ack") { if (OfferSelectionAcknowledgement.Matches(_session, _fingerprint, _offerDigest, packet)) ReadyAcks.Acknowledge(sender); return; }
            }
            else
            {
                if (sender != NetworkManager.ServerClientId) return;
                if (packet.Kind == "accepted")
                {
                    if (packet.Fingerprint != _fingerprint || string.IsNullOrEmpty(packet.Session) ||
                        (_session is not null && packet.Session != _session)) return;
                    var firstSelection = _offerDigest is null;
                    ContentOfferServices.Adopt(packet.Json, packet.Nonce);
                    _session = packet.Session; _offerDigest = packet.Nonce;
                    if (firstSelection)
                    {
                        var choices = ContentOfferServices.EffectivePreferences();
                        FoundationServices.Logger?.LogInfo("[Multiplayer] host offer selection adopted before ready: " + choices.Count(item => item.Value) + "/" + choices.Count + "; personal preferences unchanged");
                    }
                    Send(NetworkManager.ServerClientId, new() { Kind = "settings-ack", Fingerprint = _fingerprint, Nonce = _offerDigest });
                    return;
                }
                if (packet.Session != _session || _session is null) return;
                if (packet.Kind is "ready" or "waiting")
                {
                    _ready = packet.Kind == "ready" && ContentOfferServices.SessionLocked &&
                        OfferSelectionAcknowledgement.Matches(_session, _fingerprint, _offerDigest, packet);
                    if (_ready) Send(NetworkManager.ServerClientId, new() { Kind = "ready-ack", Fingerprint = _fingerprint, Nonce = _offerDigest });
                    return;
                }
            }
            if (packet.Kind == "money") { ReceiveMoney(packet); return; }
            if (packet.Kind == "gold") { ReceiveGold(sender, packet); return; }
            if (packet.Kind == "gold-feedback" && !_manager.IsServer && packet.Amount > 0 && packet.Run == NativePersistence.BattleKey && GoldFeedback.Accept("gold", packet.Sequence))
            {
                var player = NativePersistence.PlayerById(NetworkSession.Instance.runDataManager, packet.Player)?.PlayerObject;
                if (player?.IsOwner == true) CombatServices.NotifyActivation(player, packet.Json, packet.Amount);
                return;
            }
            if (packet.Kind == "surgery" && _manager.IsServer) { NativeSurgeries.RemoteRequest(sender, packet); return; }
            Message?.Invoke(sender, packet);
        }
        catch (Exception e) { var error = e.GetType().Name + ": " + e.Message; if (Errors.Count < 16 && Errors.Add(error)) FoundationServices.Logger?.LogError("[Multiplayer] rejected message: " + error); }
    }
    internal static void PublishMoney()
    {
        if (!Ready || !MultiplayerServices.IsHost || !NetworkSession.InstanceExists) return;
        var run = NetworkSession.Instance.runDataManager; if (run is null) return;
        Broadcast(new() { Kind = "money", Sequence = ++_sequence, Json = JsonSerializer.Serialize(new SharedMoneySnapshot { Money = run.Money, Earned = run.totalMoneyEarned, Ledger = Ledger.OrderBy(x => x).ToArray(), Battle = NativePersistence.BattleKey }) });
    }
    private static void ReceiveMoney(MultiplayerPacket packet)
    {
        if (MultiplayerServices.IsHost || !Ready || !MoneySequence.Accept("money", packet.Sequence)) return;
        var value = JsonSerializer.Deserialize<SharedMoneySnapshot>(packet.Json);
        if (value is null || !value.IsValid()) return;
        var run = NetworkSession.Instance.runDataManager; if (run is null) return;
        if (run.Money != value.Money) run.ModifyMoney(value.Money - run.Money);
        run.totalMoneyEarned = value.Earned;
        Ledger.Clear(); LedgerBattle = value.Battle; foreach (var id in value.Ledger) Ledger.Add(id);
    }
    private static readonly HashSet<string> GoldClaims = new(StringComparer.Ordinal);
    private static readonly Dictionary<int, HostGoldGuard> GoldStates = new();
    private static readonly SequenceWindow GoldFeedback = new();
    private static HostGoldGuard GoldState(int id)
    { if (!GoldStates.TryGetValue(id, out var value)) GoldStates[id] = value = new(); return value; }
    internal static HostGoldSnapshot CaptureGoldGuard(int id) => GoldState(id).Capture(Time.timeAsDouble);
    internal static void RestoreGoldGuards(CheckpointEnvelope envelope)
    {
        GoldStates.Clear();
        foreach (var pair in envelope.Players)
            if (pair.Value.GetFeature<HostGoldSnapshot>("foundation:gold-guard") is { } state) GoldStates[pair.Key] = HostGoldGuard.Restore(state, Time.timeAsDouble);
    }
    internal static void RequestGold(Player player, string key, int delta)
    {
        if (!Ready || player is null || !player.IsOwner || string.IsNullOrEmpty(NetworkCombat.CurrentReceipt)) return;
        var packet = new MultiplayerPacket { Kind = "gold", Json = key, Nonce = NetworkCombat.CurrentReceipt, Player = player.PlayerData.playerID, Amount = delta };
        if (MultiplayerServices.IsHost) ReceiveGold(_manager.LocalClientId, packet);
        else Send(NetworkManager.ServerClientId, packet);
    }
    private static void ReceiveGold(ulong sender, MultiplayerPacket packet)
    {
        if (!MultiplayerServices.IsHost || !Ready || packet.Amount is not (1 or -1 or -2 or -3) ||
            !NetworkCombat.VerifyGold(sender, packet.Player, packet.Nonce, packet.Amount > 0) ||
            !MultiplayerServices.GoldRules.TryGetValue(packet.Json, out var rule) || rule.Outgoing != (packet.Amount > 0) || Math.Abs(packet.Amount) > rule.Maximum ||
            !FoundationServices.Content.TryGet(packet.Json, out var content) || !content.Enabled) return;
        var run = NetworkSession.Instance.runDataManager; var player = NativePersistence.PlayerById(run, packet.Player);
        if (packet.Amount > 0 && (content.Category != Content.ContentCategory.Equipment || player.currentEquipment != (PurchaseType)content.NumericId)) return;
        if (packet.Amount < 0 && (content.Category != Content.ContentCategory.Affliction || player.PlayerObject is not { } live || live.HasInjury((InjuryType)content.NumericId) != -packet.Amount)) return;
        var claim = packet.Player + "/" + packet.Json + "/" + packet.Nonce;
        if (GoldClaims.Count > 4096 || !GoldClaims.Add(claim)) return;
        if (packet.Amount > 0)
        {
            if (!GoldState(packet.Player).ClaimBackstab(NetworkCombat.ProofTarget(packet.Nonce))) return;
        }
        else
        {
            if (!GoldState(packet.Player).ClaimLoss(Time.timeAsDouble)) return;
        }
        if (packet.Amount > 0 && run.Money == int.MaxValue) return;
        var before = run.Money;
        run.ModifyMoney(Math.Max(packet.Amount, -Math.Max(0, run.Money))); PublishMoney();
        if (packet.Amount > 0 && run.Money > before && player.PlayerObject is { } beneficiary)
        {
            if (beneficiary.IsOwner) CombatServices.NotifyActivation(beneficiary, packet.Json, run.Money - before);
            else Send(beneficiary.OwnerClientId, new() { Kind = "gold-feedback", Player = packet.Player, Json = packet.Json, Run = NativePersistence.BattleKey, Sequence = ++_sequence, Amount = run.Money - before });
        }
    }
}
