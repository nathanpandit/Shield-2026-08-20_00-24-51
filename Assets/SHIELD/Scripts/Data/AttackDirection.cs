namespace ShieldGame
{
    public enum AttackDirection
    {
        Top = 0,
        Right = 1,
        Bottom = 2,
        Left = 3
    }

    public static class AttackDirectionUtility
    {
        public static AttackDirection Clockwise(AttackDirection direction)
        {
            return (AttackDirection)(((int)direction + 1) & 3);
        }

        public static int ClockwiseSteps(AttackDirection from, AttackDirection to)
        {
            return ((int)to - (int)from + 4) & 3;
        }
    }
}
