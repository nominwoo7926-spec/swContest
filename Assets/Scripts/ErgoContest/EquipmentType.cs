namespace ErgoContest
{
    public enum EquipmentType
    {
        None,
        SupplyTray,
        Workbench,
        Conveyor
    }

    public enum AgentStage
    {
        Observing,
        Evaluating,
        SafetyHold,
        WaitingForApproval,
        Moving,
        MeasuringAfter,
        Paused,
        Cancelled
    }
}
