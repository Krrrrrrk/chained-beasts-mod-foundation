#if TC_EMBEDDED_CAMERA
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
#endif
using BepInEx.Logging;
using Cinemachine;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;
using UnityEngine.InputSystem;

#if TC_EMBEDDED_CAMERA
namespace TermsAndContusions.FirstPerson;
#else
namespace ChainedBeasts.FirstPerson;
#endif

public sealed class FirstPersonController : MonoBehaviour
{
    internal static FirstPersonController Instance;
    private Settings _settings;
    private Func<bool> _allowManual;
    private bool _manualPolicyProvided;
    private ManualLogSource _log;
    private Player _player;
    private Camera _camera;
    private CinemachineBrain _brain;
    private RestoreActions _restore;
    private readonly HashSet<IntPtr> _hidden = new();
    private bool _wanted, _active, _failed, _initialized;
    private float _yaw, _pitch, _noticeUntil, _auditAt;
    private string _notice = "";
    private CursorLockMode _savedLock;
    private bool _savedVisible;
    private IntPtr _model;
    internal float Yaw => _yaw;
    internal bool Ready => _initialized && !_failed;

    public FirstPersonController(IntPtr pointer) : base(pointer) { }

    [HideFromIl2Cpp]
    internal void Initialize(Settings settings, ManualLogSource log, Func<bool> allowManual = null)
    {
        _settings = settings; _log = log;
        _manualPolicyProvided = allowManual != null;
        _allowManual = allowManual ?? (() => true);
        _wanted = settings.Enabled && settings.StartFirstPerson;
        _initialized = true;
        Instance = this;
    }

    // Only a single LOCAL player can own the shared display. Online remote players are allowed.
    private Player EligiblePlayer()
    {
        if (!Application.isFocused || !NetworkSession.InstanceExists || !PlayerManager.InstanceExists) return null;
        var session = NetworkSession.Instance;
        if (session == null || !session.IsSpawned || session.IsPaused || session.TransitionPending) return null;
        var level = session.Level;
        if (level == null || !level.IsReady || level.ActiveGameState != GameLevel.eGameState.RUNNING ||
            level.BossCinematicPlaying || UIManager.GameInputBlocked || Time.timeScale <= 0) return null;
        // Read the same concrete list behind LocalPlayers (the generated IList loses ICollection.Count).
        var players = PlayerManager.Instance.localPlayers;
        if (players == null || players.Count != 1) return null;
        var player = players[0];
        if (player == null || !player.IsSpawned || !player.isActiveAndEnabled || player.IsDead || player.IsExecuting) return null;
        var id = player.clientID?.Value;
        if (id == null || !session.IsLocalClient(id) || player.agentModel == null || player.agentModel.headTransform == null)
            return null;
        return player;
    }

    // Read outside EligiblePlayer so F9 cannot change the saved manual choice
    // during a potion's temporary pause/cinematic suspension.
    private static Player LocalPlayer()
    {
        if (!NetworkSession.InstanceExists || !PlayerManager.InstanceExists) return null;
        var players = PlayerManager.Instance.localPlayers;
        if (players == null || players.Count != 1) return null;
        var player = players[0];
        var id = player?.clientID?.Value;
        return id != null && NetworkSession.Instance.IsLocalClient(id) ? player : null;
    }

    // Viewing is independent of the native action locks used for pickups and knockdowns.
    internal bool Views(NetworkAgent agent)
    {
        if (!_active || _failed || agent == null || _player == null || agent.Pointer != _player.Pointer) return false;
        var eligible = EligiblePlayer();
        var main = Camera.main;
        return eligible != null && eligible.Pointer == _player.Pointer && _camera != null && _camera.enabled &&
            eligible.agentModel.Pointer == _model && main != null && main.Pointer == _camera.Pointer &&
            _camera.gameObject.activeInHierarchy && _brain != null && !_brain.enabled;
    }

    internal bool Controls(NetworkAgent agent) => Views(agent) &&
        !_player.IsIncapacitated && !_player.IsInputBlocked && !_player.AutoMoveActive;

    public void Update()
    {
        if (!_initialized || _failed) return;
        try
        {
            var forced = ForcedViewServices.For(LocalPlayer());
            if (Application.isFocused && Keyboard.current != null && _settings!.ToggleKey != Key.None &&
                Keyboard.current[_settings.ToggleKey].wasPressedThisFrame)
                RequestToggle(forced);

            var eligible = EligiblePlayer();
            var pad = PairedPad(eligible);
            if (Application.isFocused && _settings!.PadToggle && pad?.rightStickButton.wasPressedThisFrame == true)
                RequestToggle(forced);

            if ((!(_allowManual() && _wanted) && !forced) || eligible == null) { Release(); return; }
            var camera = Camera.main;
            if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy || camera.targetTexture != null ||
                camera.targetDisplay != 0) { Release(); return; }
            if (_active && (_player == null || eligible.Pointer != _player.Pointer || _camera == null ||
                camera.Pointer != _camera.Pointer || eligible.agentModel.Pointer != _model)) Release();
            if (!_active && !Begin(eligible, camera)) return;

            // Reaudit late-loading plugins periodically before the next frame's native controls.
            if (Time.unscaledTime >= _auditAt) { NativeSafety.Audit(); _auditAt = Time.unscaledTime + 5; }
            var delta = Vector2.zero;
            if (pad != null)
                delta = pad.rightStick.ReadValue() * (_settings.StickSpeed * Mathf.Min(Time.unscaledDeltaTime, .05f));
            else if (eligible.UsingKeyboard && Mouse.current != null)
                delta = Mouse.current.delta.ReadValue() * _settings.MouseSensitivity;
            (_yaw, _pitch) = ViewMath.Look(_yaw, _pitch, delta.x, delta.y, _settings.InvertY);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        catch (Exception error) { Fail(error); }
    }

    private Gamepad PairedPad(Player player)
    {
        if (player == null) return null;
        var id = player.clientID?.Value;
        if (id == null) return null;
        var device = player.InputUser?.Device;
        var paired = device?.TryCast<Gamepad>();
        if (paired != null && SameClient(GameInput.GetDeviceClient(paired), id)) return paired;
        // Solo's global input may have no Device. Resolve through the game's read-only mapping.
        foreach (var candidate in Gamepad.all)
            if (SameClient(GameInput.GetDeviceClient(candidate), id)) return candidate;
        return null;
    }

    private static bool SameClient(ClientIdentifier a, ClientIdentifier b) =>
        a != null && b != null && a.NetworkID == b.NetworkID && a.LocalID == b.LocalID && a.PlayerIndex == b.PlayerIndex;

    private void Toggle()
    {
        _wanted = !_wanted;
        if (!_wanted) Release();
        _notice = _wanted ? "First person ON - activates during arena play" : "Third person";
        _noticeUntil = Time.unscaledTime + 3;
        _log?.LogInfo(_notice);
    }

    private void RequestToggle(bool forced)
    {
        if (!_allowManual()) return;
        if (forced)
        {
            _notice = "First person locked until this battle ends";
            _noticeUntil = Time.unscaledTime + 3;
        }
        else if (_manualPolicyProvided || _settings.Enabled) Toggle();
    }

    [HideFromIl2Cpp]
    internal void ReportForceFailure(string source, Exception error) =>
        _log?.LogWarning("First Person force source disabled for this launch: " + source + ": " + error);

    private bool Begin(Player player, Camera camera)
    {
        var brain = camera.GetComponent<CinemachineBrain>();
        if (brain == null || !brain.enabled) return false;
        NativeSafety.Audit();
        _restore = new RestoreActions(e => _log?.LogWarning("First Person cleanup: " + e.Message));
        _player = player; _camera = camera; _brain = brain; _model = player.agentModel.Pointer;
        var oldBrainEnabled = brain.enabled;
        _restore.Add(() => { if (brain != null) brain.enabled = oldBrainEnabled; });
        var position = camera.transform.position;
        var rotation = camera.transform.rotation;
        var fov = camera.fieldOfView;
        var near = camera.nearClipPlane;
        var orthographic = camera.orthographic;
        _restore.Add(() =>
        {
            if (camera == null) return;
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fov; camera.nearClipPlane = near; camera.orthographic = orthographic;
        });
        _savedLock = Cursor.lockState; _savedVisible = Cursor.visible;
        _restore.Add(() => { Cursor.lockState = _savedLock; Cursor.visible = _savedVisible; });
        var modelForward = player.ModelForward;
        _yaw = ViewMath.Wrap(Mathf.Atan2(modelForward.x, modelForward.z) * Mathf.Rad2Deg);
        _pitch = 0;
        brain.enabled = false;
        camera.orthographic = false;
        camera.fieldOfView = _settings!.Fov;
        camera.nearClipPlane = _settings.NearClip;
        HideOwnMeshes();
        _active = true;
        _auditAt = Time.unscaledTime + 5;
        return true;
    }

    public void LateUpdate()
    {
        if (!_active || _failed || _player == null) return;
        try
        {
            if (!Views(_player) || Camera.main == null || Camera.main.Pointer != _camera!.Pointer ||
                _player.agentModel.Pointer != _model) { Release(); return; }
            HideOwnMeshes(); // Includes cosmetics spawned after entering first person.
            var head = _player.agentModel.headTransform.position;
            var forward = ViewMath.Rotate(0, 1, _yaw);
            var eye = head + Vector3.up * _settings!.HeightOffset +
                new Vector3(forward.X, 0, forward.Z) * _settings.ForwardOffset;
            // Keep the prone camera just above the character's ground origin, with a level horizon.
            eye.y = Mathf.Max(eye.y, _player.transform.position.y + .2f);
            _camera!.transform.SetPositionAndRotation(eye, Quaternion.Euler(_pitch, _yaw, 0));
            _camera.fieldOfView = _settings.Fov;
            _camera.nearClipPlane = _settings.NearClip;
        }
        catch (Exception error) { Fail(error); }
    }

    private void HideOwnMeshes()
    {
        var model = _player!.agentModel;
        if (_settings!.HideBody)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                if (renderer != null && !IsEquipment(renderer.transform, model)) Hide(renderer);
            HideAll(model.mainRenderers);
        }
        if (!_settings.HideHead && !_settings.HideBody) return;
        Hide(model.headMaleRenderer); Hide(model.headFemaleRenderer);
        foreach (var renderer in model.headTransform.GetComponentsInChildren<Renderer>(true)) Hide(renderer);
        HideAll(model.browRenderers);
        HideAll(model.hairRenderers_M_act0); HideAll(model.hairRenderers_M_act1); HideAll(model.hairRenderers_M_act2);
        HideAll(model.hairRenderers_F_act0); HideAll(model.hairRenderers_F_act1); HideAll(model.hairRenderers_F_act2);
        HideAll(model.beardRenderers_Act0); HideAll(model.beardRenderers_Act1); HideAll(model.beardRenderers_Act2);
        if (model.selectedMask != null) Hide(model.selectedMask.Renderer);
        if (model.crownInstance != null)
            foreach (var renderer in model.crownInstance.GetComponentsInChildren<Renderer>(true)) Hide(renderer);
        if (model.eyePatch != null)
            foreach (var renderer in model.eyePatch.GetComponentsInChildren<Renderer>(true)) Hide(renderer);
    }

    private static bool IsEquipment(Transform transform, AgentModel model) =>
        Under(transform, model.mainHandTransform) || Under(transform, model.offHandTransform) ||
        Under(transform, model.bothHandsTransform) || Under(transform, model.bowTransform) ||
        Under(transform, model.backTransform);

    private static bool Under(Transform transform, Transform parent) => parent != null && transform.IsChildOf(parent);

    private void HideAll(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Renderer> renderers)
    {
        if (renderers == null) return;
        foreach (var renderer in renderers) Hide(renderer);
    }

    private void Hide(Renderer renderer)
    {
        if (renderer == null) return;
        if (_hidden.Add(renderer.Pointer))
        {
            var oldHidden = renderer.forceRenderingOff;
            _restore!.Add(() => { if (renderer != null) renderer.forceRenderingOff = oldHidden; });
        }
        renderer.forceRenderingOff = true;
    }

    private void Release()
    {
        _active = false; // Stop input hooks before restoring any Unity state.
        _player = null; _camera = null; _brain = null; _model = IntPtr.Zero;
        var restore = _restore;
        _restore = null;
        restore?.Dispose();
        _hidden.Clear();
    }

    [HideFromIl2Cpp]
    internal void Fail(Exception error)
    {
        if (_failed) return;
        _failed = true; _wanted = false;
        Release();
        _log?.LogError("First Person disabled for this session; restored third person. " + error);
    }

    public void OnApplicationFocus(bool focused) { if (!focused) Release(); }
    public void OnDisable() => Release();
    public void OnDestroy() => Shutdown();
    internal void Shutdown()
    {
        _initialized = false; _wanted = false;
        Release();
        if (ReferenceEquals(Instance, this)) Instance = null;
    }

    public void OnGUI()
    {
        if (!_initialized || _failed || Event.current.type != EventType.Repaint) return;
        var color = GUI.color;
        var depth = GUI.depth;
        try
        {
            GUI.depth = -950;
            GUI.color = Color.white;
            if (_active && _settings!.Crosshair && _player != null && Controls(_player))
            {
                var x = Screen.width * .5f; var y = Screen.height * .5f;
                GUI.DrawTexture(new Rect(x - 5, y - 1, 10, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(x - 1, y - 5, 2, 10), Texture2D.whiteTexture);
            }
            if (Time.unscaledTime < _noticeUntil) GUI.Label(new Rect(20, Screen.height - 65, 520, 35), _notice);
        }
        catch (Exception error) { Fail(error); }
        finally { GUI.color = color; GUI.depth = depth; }
    }
}
