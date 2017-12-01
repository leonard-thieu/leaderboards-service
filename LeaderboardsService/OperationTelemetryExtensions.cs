using Microsoft.ApplicationInsights.Extensibility.Implementation;

namespace toofz.NecroDancer.Leaderboards.LeaderboardsService
{
    /// <summary>
    /// Contains extension methods for <see cref="OperationTelemetry"/>.
    /// </summary>
    internal static class OperationTelemetryExtensions
    {
        /// <summary>
        /// An exception filter that marks <paramref name="telemetry"/> as unsuccessful.
        /// </summary>
        /// <param name="telemetry">The telemetry item to fail.</param>
        /// <returns>Always returns false.</returns>
        public static bool MarkAsUnsuccessful(this OperationTelemetry telemetry)
        {
            telemetry.Success = false;

            return false;
        }
    }
}
