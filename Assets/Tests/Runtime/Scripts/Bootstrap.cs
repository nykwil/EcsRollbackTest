using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.LowLevel;

public class Bootstrap
{
    private static World defaultWorld = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (defaultWorld != null)
        {
            defaultWorld.Dispose();
        }

        defaultWorld = DefaultWorldInitialization.Initialize("Default World", false);
 //       GameObjectSceneUtility .AddGameObjectSceneReferences();

        ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(defaultWorld);

        var customUpdateSystem = new CustomUpdateSystem(defaultWorld);

        AppendToUpdate(customUpdateSystem.GetType(), customUpdateSystem.Initialization, typeof(UnityEngine.PlayerLoop.Initialization));
        AppendToUpdate(customUpdateSystem.GetType(), customUpdateSystem.FixedUpdate, typeof(UnityEngine.PlayerLoop.FixedUpdate));
        AppendToUpdate(customUpdateSystem.GetType(), customUpdateSystem.PreLateUpdate, typeof(UnityEngine.PlayerLoop.PreLateUpdate));
    }

    private static void AppendToUpdate(Type customUpdater, PlayerLoopSystem.UpdateFunction updateDelegate, Type updateType)
    {
        var playerLoopSystem = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
        var subsystems = playerLoopSystem.subSystemList;

        for (int i = 0; i < subsystems.Length; i++)
        {
            var system = subsystems[i];

            if (system.type == updateType)
            {
                var updateSystem = new List<UnityEngine.LowLevel.PlayerLoopSystem>(system.subSystemList);

                var customPlayerLoop = new UnityEngine.LowLevel.PlayerLoopSystem();
                customPlayerLoop.type = customUpdater;
                customPlayerLoop.updateDelegate = updateDelegate;

                updateSystem.Add(customPlayerLoop);

                system.subSystemList = updateSystem.ToArray();
                playerLoopSystem.subSystemList[i] = system;
            }
        }
        UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(playerLoopSystem);
    }
}