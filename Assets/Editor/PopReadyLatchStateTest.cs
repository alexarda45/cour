using System;
using UnityEditor;
using UnityEngine;

namespace ChromaBlast.EditorTools
{
    // Deterministic gameplay-state assertion. It intentionally creates no scene
    // or asset state and validates ScoreManager rather than UI visibility alone.
    public static class PopReadyLatchStateTest
    {
        [MenuItem("Chroma Blast/Tests/Run POP Ready Latch State Test", false, 120)]
        public static void RunFromMenu()
        {
            Debug.Log(Run());
        }

        public static string Run()
        {
            GameObject host = new GameObject("POP Ready Latch State Test")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                ScoreManager score = host.AddComponent<ScoreManager>();
                score.ResetScore();
                score.SetPopRechargeMultiplier(1f, notifyChanged: false);

                ChromaColor purple = ChromaColor.Magenta;
                ChromaColor green = ChromaColor.Lime;
                ChromaColor yellow = ChromaColor.Amber;
                ChromaColor red = ChromaColor.Cyan;
                int initialRequirement = score.CurrentPopRequirement;
                score.DebugSetPopState(purple, initialRequirement, readyLatched: true);
                score.DebugSetPopState(green, initialRequirement, readyLatched: true);
                score.DebugSetPopState(yellow, 0, readyLatched: false);
                score.DebugSetPopState(red, 0, readyLatched: false);

                Assert(score.IsPopReady(purple), "Purple must begin READY.");
                Assert(score.IsPopReady(green), "Green must begin READY.");
                Assert(!score.IsPopReady(yellow), "Yellow must begin unready.");
                Assert(!score.IsPopReady(red), "Red must begin unready.");

                int greenChargeBefore = score.GetChroma(green);
                score.ApplyPop(purple, 1, notifyChanged: false);
                score.SetPopRechargeMultiplier(1.35f, notifyChanged: false);

                Assert(!score.IsPopReady(purple), "Purple must be consumed.");
                Assert(score.GetChroma(purple) == 0, "Purple charge must reset to zero.");
                Assert(score.IsPopReady(green), "Green READY latch must survive Purple use.");
                Assert(score.DebugGetPopReadyLatch(green), "Green latch must remain set.");
                Assert(score.GetChroma(green) == greenChargeBefore, "Green charge must remain unchanged.");
                Assert(!score.IsPopReady(yellow), "Yellow must remain unready.");
                Assert(!score.IsPopReady(red), "Red must remain unready.");

                score.ApplyPop(green, 1, notifyChanged: false);
                Assert(!score.IsPopReady(green), "Green must become unready after Green is used.");
                Assert(score.GetChroma(green) == 0, "Green charge must reset after Green use.");

                return "POP ready-latch state test PASS: Purple consumption preserved Green charge/readiness; Green then consumed independently.";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("POP ready-latch state test FAIL: " + message);
            }
        }
    }
}
