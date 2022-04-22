using UnityEngine;

namespace Tests
{
    public class RollbackTests : MonoBehaviour
    {
        private void OnGUI()
        {
            if (CustomUpdateSystem.Instance != null)
            {
                GUILayout.Label("Frame: " + CustomUpdateSystem.Instance.TargetFrame);
                if (GUILayout.Button(CustomUpdateSystem.Simulating ? "Pause Simulating" : "Start Simulating"))
                {
                    CustomUpdateSystem.Simulating = !CustomUpdateSystem.Simulating;
                }
                if (GUILayout.Button("Enable Rollback Tests"))
                {
                    CustomUpdateSystem.Instance.EnableRollbackTest();
                }
            }
        }
    }
}