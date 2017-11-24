using Microsoft.ApplicationInsights.Extensibility.Implementation;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    /// <summary>
    /// Contains utility methods.
    /// </summary>
    internal static class Util
    {
        /// <summary>
        /// An exception filter that marks <paramref name="telemetry"/> as non-successful.
        /// </summary>
        /// <param name="telemetry">The telemetry item to fail.</param>
        /// <returns>Always returns false.</returns>
        public static bool FailTelemetry(OperationTelemetry telemetry)
        {
            telemetry.Success = false;

            return false;
        }
    }
}
