﻿using Assets.Scripts;
using Shinobytes.Ravenfall.RavenNet;
using Shinobytes.Ravenfall.RavenNet.Models;
using Shinobytes.Ravenfall.RavenNet.Modules;
using Shinobytes.Ravenfall.RavenNet.Packets.Client;
using System.Collections;
using System.Net;
using UnityEngine;

public class NetworkClient : MonoBehaviour
{
    [SerializeField] private int serverPort = 8000;
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private Shinobytes.Ravenfall.RavenNet.Core.ILogger logger;
    [SerializeField] private PlayerManager playerManager;
    [SerializeField] private ObjectManager objectManager;

    private volatile bool canAuthenticate = true;
    private volatile bool canConnect = true;

    private IRavenClient gameClient;

    public Authentication Auth { get; private set; }
    public PlayerHandler PlayerHandler { get; private set; }
    public ObjectHandler ObjectHandler { get; private set; }

    public void MoveTo(Shinobytes.Ravenfall.RavenNet.Models.Vector3 destination)
    {
        var from = playerManager.Me.transform.position;
        if (from == (UnityEngine.Vector3)destination) return;
        gameClient.Send(new PlayerMoveRequest
        {
            Position = from,
            Destination = destination
        }, SendOption.Reliable);
    }

    public void SendPosition(UnityEngine.Vector3 position)
    {
        gameClient.Send(new PlayerPositionUpdate
        {
            Position = position
        }, SendOption.None);
    }

    public void SendObjectAction(int objectServerId, int actionId, int actionParameterId)
    {
        gameClient.Send(new PlayerObjectActionRequest
        {
            ObjectServerId = objectServerId,
            ActionId = actionId,
            ParameterId = actionParameterId
        }, SendOption.None);
    }

    // Start is called before the first frame update
    private void Start()
    {
        logger = GameObject.FindObjectOfType<GameUILog>();
        gameClient = IoCContainer.Instance.Resolve<IRavenClient>();
        Auth = gameClient.Modules.GetModule<Authentication>();
        PlayerHandler = gameClient.Modules.GetModule<PlayerHandler>();
        ObjectHandler = gameClient.Modules.GetModule<ObjectHandler>();

        Connect();
    }

    // Update is called once per frame
    private void Update()
    {
        if (!gameClient.IsConnected)
        {
            Connect();
            return;
        }

        if (!Auth.Authenticated)
        {
            Authenticate();
            return;
        }

        HandlePlayerUpdates();
        HandleObjectUpdates();
        HandleNpcUpdates();
    }

    private void HandleNpcUpdates()
    {
    }

    private void HandleObjectUpdates()
    {
        var stateChange = ObjectHandler.PollEvent();
        if (stateChange == null) return;

        switch (stateChange)
        {
            case EntityAdded<SceneObject> add:
                objectManager.OnObjectAdded(add.Entity);
                break;

            case EntityUpdated<SceneObject> updated:
                objectManager.OnObjectUpdated(updated.Entity);
                break;

            case EntityRemoved<SceneObject> removed:
                objectManager.OnObjectRemoved(removed.Entity);
                break;
        }
    }

    private void HandlePlayerUpdates()
    {
        var stateChange = PlayerHandler.PollEvent();
        if (stateChange == null) return;

        switch (stateChange)
        {
            case EntityAdded<Player> add:
                playerManager.OnPlayerAdded(add.Entity);
                break;

            case EntityRemoved<Player> removed:
                playerManager.OnPlayerRemoved(removed.Entity);
                break;

            case EntityMoved<Player> moved:
                playerManager.OnPlayerMove(moved.Entity);
                break;

            case PlayerObjectAction action:
                playerManager.OnPlayerAction(action.Entity, action.ObjectId, action.ActionType, action.ParameterId, action.Status);
                break;

            case PlayerAnimationStateUpdated animation:
                playerManager.OnPlayerAnimationStateChanged(animation.Entity, animation.AnimationState, animation.Enabled, animation.Trigger, animation.Action);
                break;

            case PlayerEquipmentStateUpdated equipmentState:
                playerManager.OnPlayerEquipmentStateChanged(equipmentState.Entity, equipmentState.ItemId, equipmentState.Equipped);
                break;

            case PlayerStatUpdated statsUpdated:
                playerManager.OnPlayerStatUpdated(statsUpdated.Entity, statsUpdated.Skill, statsUpdated.Level, statsUpdated.EffectiveLevel, statsUpdated.Experience);
                break;

            case PlayerStatsUpdated allStats:
                playerManager.OnPlayerStatsUpdated(allStats.Entity, allStats.Experience, allStats.EffectiveLevel);
                break;

            case PlayerLeveledUp levelUp:
                playerManager.OnPlayerLevelUp(levelUp.Entity, levelUp.Skill, levelUp.GainedLevels);
                break;
        }
    }

    private void Authenticate()
    {
        if (Auth.Authenticating || !canAuthenticate) return;
        StartCoroutine(AuthenticateWithServer());
    }

    private void Connect()
    {
        if (gameClient.IsConnecting || gameClient.IsConnected || !canConnect) return;
        StartCoroutine(ConnectToServer());
    }

    private IEnumerator AuthenticateWithServer()
    {
        canAuthenticate = false;
        try
        {
            Log("Authenticating with server...");

            Auth.Authenticate("player" + UnityEngine.Random.Range(1, 9999).ToString("0000"), "wowowow");

            while (Auth.Authenticating)
            {
                yield return new WaitForSeconds(0.1f);
            }

            if (Auth.Authenticated)
            {
                LogDebug("Authenticated");
                yield break;
            }

            LogError("Authentication failed.");
        }
        finally
        {
            canAuthenticate = true;
        }
    }

    private IEnumerator ConnectToServer()
    {
        canConnect = false;

        ResetState();
        try
        {
            Log("Connecting to server...");
            gameClient.ConnectAsync(TryGetServerAddress(), serverPort);

            while (gameClient.IsConnecting)
            {
                yield return new WaitForSeconds(0.1f);
            }

            if (gameClient.IsConnected)
            {
                LogDebug("Connected to the server");
                yield break;
            }

            LogError("Unable to connect to server");
            yield return new WaitForSeconds(1);
        }
        finally
        {
            canConnect = true;
        }
    }

    private void ResetState()
    {
        Auth.Reset();
        playerManager.ResetState();
        objectManager.ResetState();
    }

    private IPAddress TryGetServerAddress()
    {
        if (IPAddress.TryParse(serverAddress, out var address))
        {
            return address;
        }

        return IPAddress.Loopback;
    }

    private void Log(string message)
    {
        if (logger != null) logger.WriteLine(message);
    }

    private void LogDebug(string message)
    {
        if (logger != null) logger.Debug(message);
    }

    private void LogError(string message)
    {
        if (logger != null) logger.Error(message);
    }

    private void OnApplicationQuit()
    {
        gameClient.Dispose();
    }
}