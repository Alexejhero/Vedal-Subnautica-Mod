using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using BepInEx.Logging;
using UnityEngine;

namespace SCHIZO.Tweaks.Doom;

internal partial class DoomEngine : MonoBehaviour
{
    private static DoomEngine _instance;
    public static DoomEngine Instance
    {
        get
        {
            if (_instance) return _instance;
            _instance = new GameObject(nameof(DoomEngine)).AddComponent<DoomEngine>();
            DontDestroyOnLoad(_instance.gameObject);
            return _instance;
        }
    }
    public Texture2D Screen { get; } = new Texture2D(0, 0, TextureFormat.BGRA32, false);
    public Sprite Sprite { get; private set; }
    public Vector2Int ScreenResolution => new(Screen.width, Screen.height);

    /// <summary>
    /// Whether the player has been initialized at least once.
    /// </summary>
    public bool IsInitialized => _doomThread.ThreadState != System.Threading.ThreadState.Unstarted;
    /// <summary>
    /// Whether the game has started.
    /// </summary>
    public bool IsStarted { get; private set; }
    /// <summary>
    /// If this is <see langword="false"/>, the game is paused externally (e.g. if there are no <see cref="IDoomClient"/>s connected).
    /// </summary>
    public bool IsRunning => _runningEvent.IsSet;
    public string WindowTitle { get; private set; }
    private DoomClientManager _clientManager;

    internal int ConnectedClients => _clientManager.Count;
    internal float StartupTime { get; private set; }
    internal static int LastExitCode { get; private set; }
    internal int CurrentTick { get; private set; }

    private void Awake()
    {
        _clientManager = new(this);
    }

    private void OnDestroy()
    {
        _doomThread.Abort();
    }
    private void Initialize()
    {
        if (IsInitialized) return;

        _doomThread.Name = "Doom";
        _doomThread.Priority = System.Threading.ThreadPriority.BelowNormal;
        if (!CurrentThreadIsMainThread())
            throw new InvalidOperationException("Doom must be initialized from the Unity thread");
        _mainThread = Thread.CurrentThread;
        _mainThreadId = _mainThread.ManagedThreadId;
        _doomThread.Start();
    }

    internal void RunOnUnityThread(Action action)
    {
        if (IsOnUnityThread())
            action();
        else
            ScheduleUnityThread(action);
    }
    private ConcurrentQueue<Action> _unityThreadQueue = new();
    internal void ScheduleUnityThread(Action action)
    {
        _unityThreadQueue.Enqueue(action);
    }

    private static bool IsOnUnityThread()
    {
        return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
    }

    public void Connect(IDoomClient client)
    {
        if (!IsInitialized) Initialize();
        else if (!IsOnUnityThread())
            throw new InvalidOperationException("Clients must connect from the Unity thread");
        _clientManager.Add(client);
    }

    public void Disconnect(IDoomClient client)
    {
        // theoretically this should only ever get called from the unity thread
        if (!IsInitialized)
        {
            LogError("DoomPlayer should have been initialized by the time Disconnect gets called");
            Initialize();
        }
        _clientManager.Remove(client);
    }

    public void SetPaused(bool paused)
    {
        if (!IsOnUnityThread())
        {
            LogWarning("doom thread calling SetPaused, it's about to ouroboros itself");
        }
        // TODO: check if time passing between pause/unpause is an issue
        // (next tick, the "tick count" will jump forward and maybe Doom won't handle that well)
        if (paused)
            _runningEvent.Reset();
        else
            _runningEvent.Set();
    }

    private void Update()
    {
        // TODO fix main thread freeze (related to input?)
        while (_unityThreadQueue.TryDequeue(out Action action))
        {
            action();
        }
        if (_clientManager.Any(c => c.IsAcceptingInput))
        {
            CollectKeys();
            CollectMouse();
        }
        if (_frameState == FrameState.GatherInput)
        {
            _frameState = FrameState.DoGameTick;
            _inputEvent.Set();
        }
        // game drew a frame, notify clients
        if (_frameState == FrameState.WaitForDraw)
        {
            _frameState = FrameState.FrameEnd;
            if (_screenBuffer == IntPtr.Zero)
            {
                // theoretically should never happen
                LogError("Tried to draw before screen buffer was assigned");
                return;
            }
            Screen.LoadRawTextureData(_screenBuffer, Screen.width * Screen.height * sizeof(uint));
            Screen.Apply();
            _clientManager.OnDrawFrame();
            _drawEvent.Set();
        }
    }

    private readonly HashSet<DoomKey> _pressedKeys = [];
    private readonly HashSet<DoomKey> _heldKeys = [];
    private readonly HashSet<DoomKey> _releasedKeys = [];
    // the blocklist is needed because some keys are rebound and thus double up w/ the "input string"
    // e.g. pause menu "save game" shortcut 's' conflicts w/ "down arrow" binding for S
    private readonly HashSet<byte> _blockDoubles = new(Encoding.ASCII.GetBytes("swe"));
    // without the lock, the main thread randomly freezes (presumably due to simultaneous access to _XXXKeys)
    private object _inputSync = new();

    private void CollectKeys()
    {
        if (!CurrentThreadIsMainThread())
        {
            LogError("Should not be in CollectKeys on background thread");
            Debugger.Break();
        }
        lock (_inputSync)
        {
            _releasedKeys.AddRange(_heldKeys);
            if (!Input.anyKey)
            {
                _heldKeys.Clear();
                return;
            }

            foreach ((KeyCode unityKey, DoomKey doomKey) in KeyCodeConverter.GetAllKeys())
            {
                if (!Input.GetKey(unityKey)) continue;

                if (_heldKeys.Add(doomKey))
                {
                    //LogDebug($"CollectKeys pressed {doomKey} ({unityKey})");
                    _pressedKeys.Add(doomKey);
                }
                // alternate binds, duplicate DoomKey values, etc.
                // otherwise it counts as pressed and released in the same tick and therefore the input gets dropped or spammed
                _releasedKeys.Remove(doomKey);
            }
            // 6/10 on the funny scale
            // but there are a shitload of keys checked in the C code that aren't in enums at all
            // and i cba to redefine them all
            _pressedKeys.AddRange(Encoding.ASCII.GetBytes(Input.inputString.ToLowerInvariant())
                .Where(c => !_blockDoubles.Contains(c))
                .Cast<DoomKey>()
                .Where(_heldKeys.Add));

            _releasedKeys.RemoveRange(_pressedKeys);
            _heldKeys.RemoveRange(_releasedKeys);
        }
    }

    private float _mouseDeltaX;
    private float _mouseDeltaY;
    private float _mouseWheelDelta;
    private bool _left;
    private bool _right;
    private bool _middle;
    private void CollectMouse()
    {
        lock (_inputSync)
        {
            _mouseDeltaX += Input.GetAxis("Mouse X");
            //_mouseDeltaY += Input.GetAxis("Mouse Y"); // this controls forward/back movement which feels mega weird
            _mouseWheelDelta += Input.mouseScrollDelta.y;
            if (_ignoringLeftClick)
            {
                _left = false;
                if (Input.GetMouseButtonUp(0))
                    _ignoringLeftClick = false;
            }
            else
            {
                _left = Input.GetMouseButton(0);
            }

            _right = Input.GetMouseButton(1);
            _middle = Input.GetMouseButton(2);
        }
    }

    private bool _ignoringLeftClick;
    internal void IgnoreNextLeftClick()
    {
        _ignoringLeftClick = true;
    }
}
