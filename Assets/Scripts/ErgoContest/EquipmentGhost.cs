using UnityEngine;

namespace ErgoContest
{
    public sealed class EquipmentGhost : MonoBehaviour
    {
        [SerializeField] private ErgoAgentController agent;
        [SerializeField] private EquipmentType equipmentType;
        [SerializeField] private Transform ghostRoot;
        [SerializeField] private Renderer[] renderers;

        private void Update()
        {
            if (agent == null || ghostRoot == null)
                return;
            AgentCandidate selected = agent.Selected;
            bool visible = selected.IsValid && selected.Equipment == equipmentType;
            ghostRoot.gameObject.SetActive(visible);
            if (visible)
                ghostRoot.position = selected.TargetPosition;
        }
    }
}
