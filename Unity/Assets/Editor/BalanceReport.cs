using AgentClicker.Core;
using UnityEngine;

namespace AgentClicker.EditorTools
{
    /// <summary>
    /// Long balance runs that are too slow for the test suite:
    ///   Tools/unity.sh exec AgentClicker.EditorTools.BalanceReport.Career
    /// </summary>
    public static class BalanceReport
    {
        public static void Career()
        {
            var t = System.Diagnostics.Stopwatch.StartNew();
            var career = BalanceSimulator.RunCareer(10, postFactorySeconds: 3600);
            Debug.Log($"[BalanceReport] 10 divisions, 1 h after each Factory ({t.Elapsed.TotalSeconds:0}s to simulate)\n{career}");
        }
    }
}
