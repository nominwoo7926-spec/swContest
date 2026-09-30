namespace ErgoContest
{
    public enum WorkPhase
    {
        WaitingForPart,
        ReachingSupply,
        CarryingToBench,
        Assembling,
        Completed
    }

    public readonly struct WorkObservation
    {
        public WorkObservation(
            WorkPhase phase,
            int completedCycles,
            int queuedParts,
            float averageCycleSeconds,
            float lastCycleSeconds,
            float highReachSeconds,
            int highReachCount,
            float farReachSeconds,
            float assemblyHoldSeconds,
            float queueWaitSeconds,
            bool handInSupplyMoveZone,
            bool handInBenchMoveZone,
            bool holdingPart)
        {
            Phase = phase;
            CompletedCycles = completedCycles;
            QueuedParts = queuedParts;
            AverageCycleSeconds = averageCycleSeconds;
            LastCycleSeconds = lastCycleSeconds;
            HighReachSeconds = highReachSeconds;
            HighReachCount = highReachCount;
            FarReachSeconds = farReachSeconds;
            AssemblyHoldSeconds = assemblyHoldSeconds;
            QueueWaitSeconds = queueWaitSeconds;
            HandInSupplyMoveZone = handInSupplyMoveZone;
            HandInBenchMoveZone = handInBenchMoveZone;
            HoldingPart = holdingPart;
        }

        public WorkPhase Phase { get; }
        public int CompletedCycles { get; }
        public int QueuedParts { get; }
        public float AverageCycleSeconds { get; }
        public float LastCycleSeconds { get; }
        public float HighReachSeconds { get; }
        public int HighReachCount { get; }
        public float FarReachSeconds { get; }
        public float AssemblyHoldSeconds { get; }
        public float QueueWaitSeconds { get; }
        public bool HandInSupplyMoveZone { get; }
        public bool HandInBenchMoveZone { get; }
        public bool HoldingPart { get; }
    }
}
