namespace Graphing
{
    namespace Analyzer
    {
        [Flags]
        public enum PerformAnalysisType
        {
            None = 0,
            Domain = 0x01,
            Range = 0x02,
            Parity = 0x04,
            InterceptionPointsWithXAndYAxis = 0x08,
            CriticalPoints = 0x10,
            Asymptotes = 0x20,
            Monotonicity = 0x40,
            Period = 0x80,
            All = 0xFF
        }
    }
}
