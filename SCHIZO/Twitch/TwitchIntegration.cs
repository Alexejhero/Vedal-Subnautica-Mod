﻿using System;
using System.Collections.Concurrent;
using JetBrains.Annotations;
using Nautilus.Commands;
using SCHIZO.Attributes.Loading;
using SCHIZO.Helpers;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using UnityEngine;

namespace SCHIZO.Twitch;

[AddComponent(AddComponentAttribute.Target.Plugin)]
[LoadConsoleCommands]
public sealed class TwitchIntegration : MonoBehaviour
{
    private const string OWNER_USERNAME = "alexejherodev";
    private const string TARGET_CHANNEL = "vedal987";
    private const string COMMAND_SENDER = "alexejherodev";

    private const string _playerPrefsKey = "SCHIZO_TwitchIntegration_OAuthToken";

    private readonly TwitchClient _client;
    private readonly ConcurrentQueue<string> _msgQueue = new();

    public TwitchIntegration()
    {
        ClientOptions clientOptions = new()
        {
            MessagesAllowedInPeriod = 750,
            ThrottlingPeriod = TimeSpan.FromSeconds(30)
        };
        WebSocketClient customClient = new(clientOptions);
        _client = new TwitchClient(customClient);

        _client.OnError += (_, evt) => LOGGER.LogError(evt.Exception);
        _client.OnIncorrectLogin += (_, evt) => LOGGER.LogError($"Could not connect: {evt.Exception.Message}");
        _client.OnConnected += (_, _) => LOGGER.LogInfo("Connected");
        _client.OnConnectionError += (_, evt) => LOGGER.LogError($"Could not connect: {evt.Error.Message}");
        _client.OnJoinedChannel += (_, _) => LOGGER.LogInfo("Joined");
        _client.OnFailureToReceiveJoinConfirmation += (_, evt) => LOGGER.LogError($"Could not join: {evt.Exception.Details}");
        _client.OnMessageReceived += Client_OnMessageReceived;

        string key = PlayerPrefs.GetString(_playerPrefsKey, "");
        if (string.IsNullOrWhiteSpace(key))
        {
            LOGGER.LogWarning("Twitch OAuth token is not set, Twitch Integration will be disabled.");
            LOGGER.LogMessage("Run 'settwitchkey <key>' in the developer console and restart Subnautica in order to enable it.");
            return;
        }
        ConnectionCredentials credentials = new(OWNER_USERNAME, key);

        _client.Initialize(credentials, TARGET_CHANNEL);

        _client.Connect();
    }

    private void Client_OnMessageReceived(object _, OnMessageReceivedArgs evt)
    {
        const string PREFIX = "pls ";

        ChatMessage message = evt.ChatMessage;

        if (message.Username.ToLower() != COMMAND_SENDER) return; // ensure I don't get isekaid
        if (!message.Message.StartsWith(PREFIX)) return;

        // OnMessageReceived runs in a worker thread, where we can't use Unity APIs
        _msgQueue.Enqueue(message.Message[PREFIX.Length..]);
    }

    private void FixedUpdate()
    {
        if (_msgQueue.Count > 0 && _msgQueue.TryDequeue(out string message)) HandleMessage(message);
    }

    private void HandleMessage(string message)
    {
        MessageHelpers.SuppressOutput = true;
        DevConsole.SendConsoleCommand(message);
        MessageHelpers.SuppressOutput = false;
    }

    [ConsoleCommand("settwitchkey"), UsedImplicitly]
    public static string OnConsoleCommand_settwitchkey(string key)
    {
        PlayerPrefs.SetString(_playerPrefsKey, key);
        return "Twitch token updated. Please restart Subnautica.";
    }
}
