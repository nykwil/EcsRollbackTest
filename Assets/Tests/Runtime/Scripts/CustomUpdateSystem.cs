using System;
using Unity.Entities;
using UnityEngine;

public class CustomUpdateSystem : IDisposable
{
    public static CustomUpdateSystem Instance { get; private set; }
    public static bool Simulating { get; set; }

    private ComponentSystemGroup initGroup;
    private ComponentSystemGroup simGroup;
    private ComponentSystemGroup presGroup;

    private World lockStepWorld;
    private World activeWorld;

    private bool rollbackTest;

    public CustomUpdateSystem(World world)
    {
        Instance = this;
        activeWorld = world;
        lockStepWorld = new World("lockStepWorld", WorldFlags.Simulation);
        initGroup = world.GetExistingSystem<InitializationSystemGroup>();
        simGroup = world.GetExistingSystem<SimulationSystemGroup>();
        presGroup = world.GetExistingSystem<PresentationSystemGroup>();
    }

    public void Initialization()
    {
        if (initGroup.Created)
        {
            initGroup.Update();
        }
    }

    public void FixedUpdate()
    {
        if (simGroup.Created && Simulating)
        {
            simGroup.Update();

            if (rollbackTest)
            {
                if (Time.frameCount % 7 == 0)
                {
                    SaveSimulationWorld();
                }
                else if (Time.frameCount % 11 == 0)
                {
                    RestoreSimulationWorld();
                }
            }
        }
    }

    public void PreLateUpdate()
    {
        if (presGroup.Created)
        {
            presGroup.Update();
        }
    }

    public void SaveSimulationWorld()
    {
        CopyWorld(lockStepWorld, activeWorld);
    }

    public void RestoreSimulationWorld()
    {
        CopyWorld(activeWorld, lockStepWorld);
    }

    public static void CopyWorld(World toWorld, World fromWorld)
    {
        //snapShotWorld.EntityManager.DestroyAndResetAllEntities();
        toWorld.EntityManager.CopyAndReplaceEntitiesFrom(fromWorld.EntityManager);
        toWorld.SetTime(new Unity.Core.TimeData(fromWorld.Time.ElapsedTime, fromWorld.Time.DeltaTime));
    }

    public void EnableRollbackTest()
    {
        CustomUpdateSystem.Instance.SaveSimulationWorld();
        rollbackTest = true;
    }

    public void Dispose()
    {
        Instance = null;
        lockStepWorld.Dispose();
        activeWorld.Dispose();
    }
}