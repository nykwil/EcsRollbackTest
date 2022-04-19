using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.LowLevel;

public class Bootstrap {
    private static World defaultWorld = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize() {
        if (defaultWorld != null) {
            defaultWorld.Dispose();
        }
        Debug.Log("Yo!!!!");
        defaultWorld = DefaultWorldInitialization.Initialize("Default World", false);
        GameObjectSceneUtility.AddGameObjectSceneReferences();

        ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(defaultWorld);

        CustomUpdateSystem customUpdateSystem = new CustomUpdateSystem(defaultWorld);

        AppendToUpdate(customUpdateSystem.GetType(), customUpdateSystem.Initialization, typeof(UnityEngine.PlayerLoop.Initialization));
        AppendToUpdate(customUpdateSystem.GetType(), customUpdateSystem.FixedUpdate, typeof(UnityEngine.PlayerLoop.FixedUpdate));
        AppendToUpdate(customUpdateSystem.GetType(), customUpdateSystem.PreLateUpdate, typeof(UnityEngine.PlayerLoop.PreLateUpdate));
    }

    private static void AppendToUpdate(Type customUpdater, PlayerLoopSystem.UpdateFunction updateDelegate, Type updateType) {
        var playerLoopSystem = UnityEngine.LowLevel.PlayerLoop.GetCurrentPlayerLoop();
        var subsystems = playerLoopSystem.subSystemList;

        for (int i = 0; i < subsystems.Length; i++) {
            var system = subsystems[i];

            if (system.type == updateType) {
                var fixedUpdateSystem = system;

                var fixedUpdateSystems = new List<UnityEngine.LowLevel.PlayerLoopSystem>(system.subSystemList);

                var customPlayerLoop = new UnityEngine.LowLevel.PlayerLoopSystem();
                customPlayerLoop.type = customUpdater;
                customPlayerLoop.updateDelegate = updateDelegate;

                fixedUpdateSystems.Add(customPlayerLoop);

                fixedUpdateSystem.subSystemList = fixedUpdateSystems.ToArray();
                playerLoopSystem.subSystemList[i] = fixedUpdateSystem;
            }
        }
        UnityEngine.LowLevel.PlayerLoop.SetPlayerLoop(playerLoopSystem);
    }
}

public class CustomUpdateSystem : IDisposable {
    public static CustomUpdateSystem Instance { get; private set; }
    private ComponentSystemGroup initGroup;

    private ComponentSystemGroup simGroup;

    private ComponentSystemGroup presGroup;

    public static bool simulating = true;
    private World lockStepWorld;
    private World activeWorld;

    public CustomUpdateSystem(World world) {
        activeWorld = world;
        lockStepWorld = new World("lockStepWorld", WorldFlags.Simulation);
        initGroup = world.GetExistingSystem<InitializationSystemGroup>();

        simGroup = world.GetExistingSystem<SimulationSystemGroup>();

        presGroup = world.GetExistingSystem<PresentationSystemGroup>();
        Instance = this;
    }

    public void Initialization() {
        if (initGroup.Created) {
            initGroup.Update();
        }    }

    public bool autoMode;

    public void FixedUpdate() {
        if (simGroup.Created && simulating) {
            simGroup.Update();

            if (autoMode) {
                if (Time.frameCount % 10 == 0) {
                    SaveSimulationWorld();
                }
                else if (Time.frameCount % 7 == 0) {
                    RestoreSimulationWorld();
                }
            }
        }
    }

    public void PreLateUpdate() {
        if (presGroup.Created ) {
            presGroup.Update();
        }    }

    public void SaveSimulationWorld() {
        CopyWorld(lockStepWorld, activeWorld);
    }

    public void RestoreSimulationWorld() {
        CopyWorld(activeWorld, lockStepWorld);
    }

    public static void CopyWorld(World toWorld, World fromWorld) {
        //snapShotWorld.EntityManager.DestroyAndResetAllEntities();
        toWorld.EntityManager.CopyAndReplaceEntitiesFrom(fromWorld.EntityManager);
        toWorld.SetTime(new Unity.Core.TimeData(fromWorld.Time.ElapsedTime, fromWorld.Time.DeltaTime));
    }

    public void Dispose() {
        Instance = null;
    }
}