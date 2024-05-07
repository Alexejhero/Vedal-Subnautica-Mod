using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace SCHIZO.Tweaks.Doom;
partial class DoomEngine
{
    private readonly Stopwatch _gameClock = new();
    private readonly Thread _doomThread = new(StartDoom_);
    private readonly ManualResetEventSlim _runningEvent = new(true);
    private readonly ManualResetEventSlim _inputEvent = new(false);
    private readonly ManualResetEventSlim _drawEvent = new(false);
    private static Thread _mainThread;
    private static int _mainThreadId;

    private static readonly string[] _launchArgs =
    [
        Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "doomgeneric.dll")
    ];

    private enum FrameState
    {
        FrameStart,
        GatherInput,
        DoGameTick,
        WaitForDraw,
        FrameEnd,
    }
    private FrameState _frameState;

    private static void StartDoom_() => Instance.StartDoom();
    private void StartDoom()
    {
        if (IsStarted)
        {
            LogWarning("Tried to start doom more than once");
            return;
        }
        if (!File.Exists(_launchArgs[0]))
        {
            IsStarted = true;
            LogError("doomgeneric.dll was not found");
            Doom_Exit(1);
            return;
        }
        _gameClock.Start();
        Stopwatch sw = Stopwatch.StartNew();
        DoomAudioNative.SetAudioCallbacks(DoomFmodAudio.SfxCallbacks(), DoomFmodAudio.MusicCallbacks());
        float audioInitTime = (float)sw.Elapsed.TotalMilliseconds;
        DoomNative.Start(new()
        {
            Init = Doom_Init,
            DrawFrame = Doom_DrawFrame,
            Sleep = Doom_Sleep,
            GetTicksMillis = Doom_GetTicksMillis,
            GetKey = Doom_GetKey,
            GetMouse = Doom_GetMouse,
            SetWindowTitle = Doom_SetWindowTitle,
            Exit = Doom_Exit,
            Log = Doom_Log,
        }, _launchArgs.Length, _launchArgs);
        sw.Stop();
        StartupTime = (float) sw.Elapsed.TotalMilliseconds;
        LogWarning($"Startup took: {StartupTime:n}ms ({audioInitTime:n}ms audio, {StartupTime - audioInitTime}ms engine)");
        IsStarted = true;
        if (IsOnUnityThread())
        {
            LogError("Should not be on Unity thread here");
            return;
        }
        Application.quitting += _doomThread.Abort;
        DoomLoop();
    }

    private void DoomLoop()
    {
        while (true)
        {
            _runningEvent.Wait();
            switch (_frameState)
            {
                case FrameState.FrameStart:
                    _frameState = FrameState.GatherInput;
                    _inputEvent.Reset();
                    break;
                case FrameState.GatherInput:
                    // wait for unity side
                    _inputEvent.Wait();
                    break;
                case FrameState.DoGameTick:
                    DoomNative.Tick();
                    break;
                case FrameState.WaitForDraw:
                    _drawEvent.Wait();
                    break;
                case FrameState.FrameEnd:
                    _clientManager.OnTick();
                    _frameState = FrameState.FrameStart;
                    break;
            }
        }
    }

    private void Doom_Init(int resX, int resY)
    {
        LogWarning($"Init {resX}x{resY}");
        //Screen.width = resX;
        //Screen.height = resY;
        Screen.Resize(resX, resY, TextureFormat.BGRA32, false);
        Sprite = Sprite.Create(Screen, new Rect(0, 0, resX, resY), new Vector2(0.5f, 0.5f));
        _clientManager.OnInit();
    }

    private uint Doom_GetTicksMillis()
    {
        CurrentTick = (int) _gameClock.ElapsedMilliseconds;
        //LogDebug($"GetTicksMillis {CurrentTick}");
        return (uint) CurrentTick;
    }

    private void Doom_SetWindowTitle(string title)
    {
        LogWarning($"SetWindowTitle {title}");
        WindowTitle = title;
        _clientManager.OnWindowTitleChanged(title);
    }

    private IntPtr _screenBuffer;
    private void Doom_DrawFrame(IntPtr screenBuffer, int bufferBytes)
    {
        // special logic for first ever drawn frame
        if (_screenBuffer == IntPtr.Zero)
        {
            //LogDebug($"DrawFrame (first) {screenBuffer:x}");
            if (screenBuffer == IntPtr.Zero)
                throw new ArgumentNullException(nameof(screenBuffer));
            _screenBuffer = screenBuffer;
            //LogDebug($"DrawFrame (first) {screenBuffer:x} assigned");
            return;
        }
        // only draw after game tick starts
        // (we already have the buffer so it doesn't matter when/how many times we get called)
        if (_frameState == FrameState.DoGameTick)
        {
            _frameState = FrameState.WaitForDraw;
            _drawEvent.Reset();
        }
    }

    private void Doom_Sleep(uint millis)
    {
        if (IsOnUnityThread())
        {
            LogError($"Should not be on Unity thread in {nameof(Doom_Sleep)}");
            return;
        }
        //LogDebug($"Sleep {millis}");
        // millis *= 100;
        Thread.Sleep(TimeSpan.FromMilliseconds(millis));
    }

    private bool Doom_GetKey(out bool pressed, out DoomKey key)
    {
        pressed = default;
        key = default;
        lock (_inputSync)
        {
            foreach (bool isPress in new[] { true, false })
            {
                HashSet<DoomKey> keys = isPress ? _pressedKeys : _releasedKeys;
                if (keys.Count == 0) continue;

                pressed = isPress;
                key = keys.First();
                keys.Remove(key);
                string keyName = Enum.IsDefined(typeof(DoomKey), key)
                    ? key.ToString()
                    : $"'{(char)key}'"; // ascii/limited to byte so it's fine
                //LogMessage($"GetKey {keyName} {(isPress ? "press" : "release")} consumed");
                return true;
            }
        }
        //LogDebug("GetKey nothing");
        return false;
    }

    private void Doom_GetMouse(out int deltaX, out int deltaY, out bool left, out bool right, out bool middle, out int wheel)
    {
        lock (_inputSync)
        {
            deltaX = (int)Interlocked.Exchange(ref _mouseDeltaX, 0f);
            deltaY = (int)Interlocked.Exchange(ref _mouseDeltaY, 0f);
            wheel = Mathf.Approximately(_mouseWheelDelta, 0f) ? 0 : (int)Mathf.Sign(_mouseWheelDelta);
            _mouseWheelDelta = 0;
            left = _left;
            right = _right;
            middle = _middle;
        }
    }

    private void Doom_Exit(int exitCode)
    {
        LogWarning($"Exit {exitCode}");
        LastExitCode = exitCode;
        IsStarted = false;
        _clientManager.OnExit(exitCode);
    }

    private void Doom_Log(string message)
    {
        LogMessage($"(Native) {message}");
    }
}
