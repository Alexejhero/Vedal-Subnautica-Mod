﻿using System;
using UnityEngine;
using Random = System.Random;

namespace SCHIZO.Sounds;

public sealed class WorldSounds : MonoBehaviour
{
    public FMOD_CustomEmitter emitter;
    [SerializeField] private Pickupable _pickupable;
    [SerializeField] private SoundPlayer _soundPlayer;

    private float _timer = -1;
    private Random _random;

    public static void Add(GameObject obj, SoundPlayer soundPlayer)
    {
        if (soundPlayer == null) throw new ArgumentNullException(nameof(soundPlayer));
        WorldSounds player = obj.AddComponent<WorldSounds>();
        player._soundPlayer = soundPlayer;
    }

    private void Awake()
    {
        _random = new Random(GetInstanceID());

        _pickupable = GetComponent<Pickupable>();
        emitter = gameObject.AddComponent<FMOD_CustomEmitter>();
        emitter.followParent = true;

        _timer = _random.Next(CONFIG.MinWorldNoiseDelay, CONFIG.MaxWorldNoiseDelay);
    }

    private void Update()
    {
        if (CONFIG.DisableAllNoises) return;
        if (CONFIG.DisableWorldNoises) return;

        if (_pickupable && Inventory.main.Contains(_pickupable)) return;

        _timer -= Time.deltaTime;

        if (_timer < 0)
        {
            _timer = _random.Next(CONFIG.MinWorldNoiseDelay, CONFIG.MaxWorldNoiseDelay);
            // todo fix
            if (_soundPlayer == null)
                LOGGER.LogWarning($"no sound player on {name} {nameof(WorldSounds)}, cannot play");
            else
                _soundPlayer.Play(emitter);
        }
    }
}