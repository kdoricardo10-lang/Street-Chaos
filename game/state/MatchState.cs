namespace StreetChaos
{
    public enum MatchState
    {
        Waiting,
        Starting,
        InProgress,
        ZoneClosing,
        FinalCircle,
        Finished
    }

    public struct SafeZone
    {
        public Godot.Vector3 Center;
        public float Radius;
        public float TargetRadius;
        public float ShrinkSpeed;
    }
}
