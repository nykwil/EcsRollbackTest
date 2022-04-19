using UnityEngine;

namespace Tests {

    public class RollbackTests : MonoBehaviour {

        [InspectorButton]
        private void EnableAutoMode() {
            CustomUpdateSystem.Instance.SaveSimulationWorld();
            CustomUpdateSystem.Instance.autoMode = true;
        }

        [InspectorButton]
        public void ToggleSimulating() {
            CustomUpdateSystem.simulating = !CustomUpdateSystem.simulating;
        }

        [InspectorButton]
        private void SaveSimulationWorld() {
            CustomUpdateSystem.Instance.SaveSimulationWorld();
        }

        [InspectorButton]
        private void RestoreSimulationWorld() {
            CustomUpdateSystem.Instance.RestoreSimulationWorld();
        }
    }
}