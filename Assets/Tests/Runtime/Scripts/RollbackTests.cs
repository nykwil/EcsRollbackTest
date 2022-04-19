using UnityEngine;

namespace Tests
{
    public class RollbackTests : MonoBehaviour
    {
        private void OnGUI()
        {
            if (GUILayout.Button("Enable Rollback Tests"))
            {
                CustomUpdateSystem.Instance.EnableRollbackTest();
            }
            if (GUILayout.Button("Toggle Simulating"))
            {
                CustomUpdateSystem.Simulating = !CustomUpdateSystem.Simulating;
            }
            if (GUILayout.Button("Save World "))
            {
                CustomUpdateSystem.Instance.SaveSimulationWorld();
            }

            if (GUILayout.Button("Restore World "))
            {
                CustomUpdateSystem.Instance.RestoreSimulationWorld();
            }
        }
    }
}