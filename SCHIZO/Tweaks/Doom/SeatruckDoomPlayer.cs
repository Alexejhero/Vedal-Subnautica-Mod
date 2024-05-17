using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SCHIZO.Tweaks.Doom;

partial class SeatruckDoomPlayer : MonoBehaviour
{
    private Transform _pictureFrame;
    private Transform _screen;
    private Transform _handTrigger;
    private MeshRenderer _screenRenderer;
    private GenericHandTarget _oldHandTarget;
    private GenericHandTarget _ourHandTarget;
    private DoomFrontend _connection;
    private Vector3 _oldScreenScale;
    private bool _die;
    private void OnEnable()
    {
        if (DoomEngine.LastExitCode != 0 || !DoomNative.CheckDll())
        {
            Destroy(this);
            return;
        }
        // seatruck modules have no discriminator, they're just prefabs
        _pictureFrame = transform.parent.Find("PictureFrame");
        if (!_pictureFrame)
        {
            Destroy(this);
            return;
        }
        _pictureFrame.GetComponent<PictureFrame>().enabled = false;
        // here's where we would move the frame to be eye-level
        // but it's part of the seatruck mesh...

        _handTrigger = _pictureFrame.Find("Trigger");
        _oldHandTarget = _handTrigger.GetComponent<GenericHandTarget>();
        _oldHandTarget.enabled = false;

        _screen = _pictureFrame.Find("Screen");
        _screenRenderer = _screen.GetComponent<MeshRenderer>();
        _screenRenderer.enabled = true;
        // the screen is cut off a little bit vertically
        _oldScreenScale = _screen.localScale;
        _screen.localScale = new Vector3(1.3f, 0.75f, 1);

        _connection = _screen.gameObject.AddComponent<DoomFrontend>();
        _connection.Connected += PlayerConnected;
        _connection.Disconnected += PlayerDisconnected;
        _connection.Exited += OnDoomExit;

        _ourHandTarget = _handTrigger.gameObject.AddComponent<GenericHandTarget>();
        _ourHandTarget.onHandHover = new();
        _ourHandTarget.onHandHover.AddListener(OnHandHover);
        _ourHandTarget.onHandClick = new();
        _ourHandTarget.onHandClick.AddListener(OnHandClick);
    }

    private void OnDoomExit(int code)
    {
        if (code != 0) _die = true; // thread
    }

    private void OnDisable()
    {
        if (_connection)
        {
            _connection.enabled = false;
            _connection.Connected -= PlayerConnected;
            _connection.Disconnected -= PlayerDisconnected;
            _connection.Exited -= OnDoomExit;
            Destroy(_connection);
        }
        if (_ourHandTarget)
        {
            _oldHandTarget.enabled = true;
            Destroy(_ourHandTarget);
        }
        if (!_pictureFrame) return;

        _screen.localScale = _oldScreenScale;

        _pictureFrame.GetComponent<PictureFrame>().enabled = true;
    }

    private void OnHandHover(HandTargetEventData eventData)
    {
        if (IsControlling) return;

        HandReticle.main.SetText(HandReticle.TextType.Hand, $"Play {DoomEngine.Instance.WindowTitle ?? "game"}", false, GameInput.Button.LeftHand);
        HandReticle.main.SetText(HandReticle.TextType.HandSubscript, "", false);
        HandReticle.main.SetIcon(HandReticle.IconType.Interact);
    }
    private bool _hintUnderstood;
    private bool _controlling;
    public bool IsControlling
    {
        get => _controlling;
        set
        {
            if (_controlling == value) return;

            _controlling = value;
            DoomEngine.Instance.IgnoreNextLeftClick();
            ToggleGameInput(!value);
            _connection.PlayerPlaying = value;
            _handTrigger.gameObject.SetActive(!value);
            ParentToSeatruck(value);
            if (value)
            {
                // todo make another transform below (so it comes from a "speaker")
                DoomFmodAudio.Emitter = _screen; // center of screen
                _connection.Connect();
                LookAtScreen();
                if (!_hintUnderstood) ShowHint();
            }
            else
            {
                _hintUnderstood = true;
                Hint.main.message.Hide();
            }
        }
    }
    private static void ShowHint()
    {
        uGUI_PopupMessage msg = Hint.main.message;
        msg.SetText("Press <color=yellow>Ctrl+Q</color> to exit", TextAnchor.MiddleLeft);
        msg.Show(-1, 0f, 0.25f, 0.25f, null);
    }

    private void OnHandClick(HandTargetEventData eventData)
    {
        _connection.enabled = true;
        IsControlling = true;
    }
    private Texture _oldTex;
    private void PlayerConnected()
    {
        if (_screenRenderer)
        {
            _oldTex = _screenRenderer.sharedMaterial.mainTexture;
            _screenRenderer.sharedMaterial.mainTexture = _connection.DoomScreen;
            Vector2 scale = _screenRenderer.sharedMaterial.mainTextureScale;
            scale.y *= -1;
            _screenRenderer.sharedMaterial.mainTextureScale = scale;
        }
        else
        {
            LOGGER.LogWarning("(Seatruck DOOM Player) Screen should have a mesh renderer");
        }
    }
    private void PlayerDisconnected()
    {
        if (_screenRenderer)
        {
            _screenRenderer.sharedMaterial.mainTexture = _oldTex;
            Vector2 scale = _screenRenderer.sharedMaterial.mainTextureScale;
            scale.y *= -1;
            _screenRenderer.sharedMaterial.mainTextureScale = scale;
        }
        IsControlling = false; // just in case
    }
    private void ToggleGameInput(bool enable)
    {
        if (GameInput.instance.enabled == enable) return;

        GameInput.ClearInput();
        GameInput.instance.enabled = enable;
        // todo completely disable game input (specifically the F-keys - debug stuff, feedback screen, UI, etc)
        //Player.main.playerController.SetEnabled(!locked);
        //FPSInputModule.current.lockMovement = locked;
        //FPSInputModule.current.lockRotation = locked;
        //FPSInputModule.current.lockPauseMenu = locked;
    }

    private void LookAtScreen()
    {
        Vector3 center = _screenRenderer.bounds.center;
        //RuntimeDebugDraw.Draw.DrawLine(center, Camera.main.transform.position, null, 5, false);
        StartCoroutine(LookAtCoro(center));
    }
    private IEnumerator LookAtCoro(Vector3 target)
    {
        MainCameraControl cam = MainCameraControl.main;
        Vector2 lookError;
        do
        {
            yield return null;
            Vector3 lookPoint = Camera.main.ScreenToWorldPoint(new(Screen.width / 2f, Screen.height / 2f));
            Vector3 plrToMe = target - lookPoint;
            Vector3 direction = plrToMe.normalized;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            Quaternion camRotation = cam.cameraOffsetTransform.rotation;
            Quaternion lookErrorQ = Quaternion.Inverse(camRotation) * targetRotation;
            lookError = lookErrorQ.eulerAngles;
            lookError.x = lookError.x > 180 ? lookError.x - 360 : lookError.x < -180 ? lookError.x + 360 : lookError.x;
            lookError.y = lookError.y > 180 ? lookError.y - 360 : lookError.y < -180 ? lookError.y + 360 : lookError.y;
            if (Mathf.Approximately(lookError.x, 0) && Mathf.Approximately(lookError.y, 0))
                continue;
            float yawDelta = lookError.y * Time.deltaTime * 5;
            float pitchDelta = lookError.x * Time.deltaTime * 5;
            cam.rotationX += yawDelta;
            cam.rotationY -= pitchDelta;
        }
        while (IsControlling);
    }

    private void ParentToSeatruck(bool parent)
    {
        Player.main.transform.parent = parent ? transform : null;
    }

    private Queue<float> _escapePresses = [];
    private float _escapeHeld;
    private void Update()
    {
        if (_die)
        {
            Destroy(this);
            return;
        }
        if (IsControlling)
        {
            if (Input.GetKey(KeyCode.Escape))
            {
                _escapeHeld += Time.deltaTime;
                if (_escapeHeld > 3f)
                    ShowHint();
            }
            else
            {
                _escapeHeld = 0f;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // 5 presses in 5 seconds
                while (_escapePresses.Count > 0 && Time.time - _escapePresses.Peek() > 5f)
                    _escapePresses.Dequeue();

                _escapePresses.Enqueue(Time.time);
                if (_escapePresses.Count >= 5)
                    ShowHint();
            }
            if (Input.GetKey(KeyCode.Q) && Input.GetKey(KeyCode.LeftControl))
            {
                IsControlling = false;
                _escapeHeld = 0;
                _escapePresses.Clear();
            }
        }
    }
}