using System;
using UnityEngine;

namespace ChromaBlast
{
    public class OceanRescueController : MonoBehaviour
    {
        private static readonly string[] CandidateShapeIds =
        {
            "single",
            "line2_v",
            "line2_h",
            "line3_v",
            "line3_h",
            "corner3"
        };

        private static readonly ChromaColor[] RescueColors =
        {
            ChromaColor.Cyan,
            ChromaColor.Magenta,
            ChromaColor.Lime
        };

        [SerializeField] private OceanRescueUI oceanRescueUI;

        private GameManager gameManager;
        private BoardManager board;
        private PieceSpawner pieceSpawner;
        private PieceInstance[] selectedRescueSet;
        private bool consumedThisRound;
        private bool declinedThisBlockedSequence;
        private bool blockingInput;
        private bool rewardedRequestActive;

        public bool IsBlockingInput => blockingInput;
        public bool ConsumedThisRound => consumedThisRound;

        public void Initialize(
            GameManager owner,
            BoardManager boardManager,
            PieceSpawner spawner)
        {
            gameManager = owner;
            board = boardManager;
            pieceSpawner = spawner;
            oceanRescueUI?.Initialize(this);
        }

        public void ResetForNewRound()
        {
            consumedThisRound = false;
            declinedThisBlockedSequence = false;
            blockingInput = false;
            rewardedRequestActive = false;
            selectedRescueSet = null;
            oceanRescueUI?.HideImmediate();
        }

        internal void RestoreConsumedState(bool consumed)
        {
            consumedThisRound = consumed;
        }

        public bool TryInterceptBlockedRound()
        {
            if (blockingInput)
            {
                return true;
            }

            if (consumedThisRound
                || declinedThisBlockedSequence
                || oceanRescueUI == null
                || board == null
                || pieceSpawner == null)
            {
                return false;
            }

            BoardSnapshot snapshot = board.CreateSnapshot();
            if (!TryFindRescueSet(snapshot, out PieceInstance[] rescueSet))
            {
                return false;
            }

            selectedRescueSet = CloneSet(rescueSet);
            rewardedRequestActive = false;
            blockingInput = true;
            oceanRescueUI.Show(selectedRescueSet);
            return true;
        }

        public void RequestRewardedRescue()
        {
            if (!blockingInput
                || rewardedRequestActive
                || selectedRescueSet == null
                || selectedRescueSet.Length != GameConstants.TraySize)
            {
                return;
            }

            rewardedRequestActive = true;
            oceanRescueUI?.SetButtonsInteractable(false);

            AdManager ads = AdManager.Instance;
            bool requestStarted = ads != null
                && ads.TryShowRewarded(
                    "ocean_rescue",
                    CompleteRewardedRescue,
                    HandleRewardedUnavailable);
            if (!requestStarted)
            {
                HandleRewardedUnavailable();
            }
        }

        public void DeclineRescue()
        {
            if (!blockingInput || rewardedRequestActive)
            {
                return;
            }

            declinedThisBlockedSequence = true;
            rewardedRequestActive = false;
            oceanRescueUI?.SetButtonsInteractable(false);
            oceanRescueUI?.CloseAnimated(() =>
            {
                blockingInput = false;
                selectedRescueSet = null;
                gameManager?.ContinueToGameOverAfterOceanRescue();
            });
        }

        public void HideForGameOver()
        {
            rewardedRequestActive = false;
            blockingInput = false;
            selectedRescueSet = null;
            oceanRescueUI?.HideImmediate();
        }

#if UNITY_EDITOR
        public bool DebugShowOceanRescue()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[Ocean Rescue Debug] Show Ocean Rescue is available only in Play Mode.");
                return false;
            }

            if (oceanRescueUI == null || board == null || pieceSpawner == null)
            {
                Debug.LogWarning(
                    "[Ocean Rescue Debug] Ocean Rescue runtime references are not initialized.");
                return false;
            }

            if (blockingInput && oceanRescueUI.IsVisible)
            {
                Debug.Log("[Ocean Rescue Debug] Ocean Rescue is already open.");
                return true;
            }

            BoardSnapshot snapshot = board.CreateSnapshot();
            if (!TryFindRescueSet(snapshot, out PieceInstance[] rescueSet))
            {
                Debug.LogWarning(
                    "[Ocean Rescue Debug] No valid three-piece rescue sequence exists for the current board.");
                return false;
            }

            selectedRescueSet = CloneSet(rescueSet);
            rewardedRequestActive = false;
            blockingInput = true;
            oceanRescueUI.Show(selectedRescueSet);
            Debug.Log("[Ocean Rescue Debug] Opened Ocean Rescue.");
            return true;
        }

        public bool DebugSimulateRewardSuccess()
        {
            if (!CanRunDebugPopupCommand("Simulate Reward Success"))
            {
                return false;
            }

            rewardedRequestActive = true;
            oceanRescueUI.SetButtonsInteractable(false);
            CompleteRewardedRescue();
            Debug.Log("[Ocean Rescue Debug] Simulated rewarded success.");
            return true;
        }

        public bool DebugSimulateAdFailure()
        {
            if (!CanRunDebugPopupCommand("Simulate Ad Failure"))
            {
                return false;
            }

            rewardedRequestActive = true;
            oceanRescueUI.SetButtonsInteractable(false);
            HandleRewardedUnavailable();
            Debug.Log("[Ocean Rescue Debug] Simulated ad failure.");
            return true;
        }

        public bool DebugCloseOceanRescue()
        {
            if (!Application.isPlaying
                || oceanRescueUI == null
                || !oceanRescueUI.IsVisible
                || !blockingInput)
            {
                Debug.LogWarning(
                    "[Ocean Rescue Debug] Close Ocean Rescue requires an open popup in Play Mode.");
                return false;
            }

            rewardedRequestActive = false;
            oceanRescueUI.SetButtonsInteractable(false);
            oceanRescueUI.CloseAnimated(() =>
            {
                selectedRescueSet = null;
                blockingInput = false;
                Debug.Log("[Ocean Rescue Debug] Closed Ocean Rescue.");
            });
            return true;
        }

        public bool DebugResetOceanRescueState()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[Ocean Rescue Debug] Reset Ocean Rescue State is available only in Play Mode.");
                return false;
            }

            ResetForNewRound();
            Debug.Log("[Ocean Rescue Debug] Runtime Ocean Rescue state reset.");
            return true;
        }

        private bool CanRunDebugPopupCommand(string commandName)
        {
            if (!Application.isPlaying
                || oceanRescueUI == null
                || !oceanRescueUI.IsVisible
                || !blockingInput
                || selectedRescueSet == null
                || selectedRescueSet.Length != GameConstants.TraySize)
            {
                Debug.LogWarning(
                    $"[Ocean Rescue Debug] {commandName} requires an open Ocean Rescue popup.");
                return false;
            }

            return true;
        }
#endif

        public static bool TryFindRescueSet(
            BoardSnapshot snapshot,
            out PieceInstance[] rescueSet)
        {
            rescueSet = null;
            if (!TryCreateOccupancy(snapshot, out bool[] occupancy))
            {
                return false;
            }

            string[] preferred = { "single", "line2_v", "single" };
            if (CanPlaceEntireSequence(occupancy, preferred))
            {
                rescueSet = CreateSet(preferred);
                return true;
            }

            for (int first = 0; first < CandidateShapeIds.Length; first++)
            {
                for (int second = 0; second < CandidateShapeIds.Length; second++)
                {
                    for (int third = 0; third < CandidateShapeIds.Length; third++)
                    {
                        string[] candidate =
                        {
                            CandidateShapeIds[first],
                            CandidateShapeIds[second],
                            CandidateShapeIds[third]
                        };

                        if (!CanPlaceEntireSequence(occupancy, candidate))
                        {
                            continue;
                        }

                        rescueSet = CreateSet(candidate);
                        return true;
                    }
                }
            }

            return false;
        }

        private void CompleteRewardedRescue()
        {
            if (!blockingInput || !rewardedRequestActive || selectedRescueSet == null)
            {
                return;
            }

            rewardedRequestActive = false;
            consumedThisRound = true;
            gameManager?.PersistOceanRescueConsumedState();
            PieceInstance[] grantedSet = CloneSet(selectedRescueSet);
            AnalyticsManager.Instance?.RecordRewardedCompleted("ocean_rescue");

            oceanRescueUI?.CloseAnimated(() =>
            {
                selectedRescueSet = null;
                blockingInput = false;
                pieceSpawner.SpawnSet(grantedSet);
                gameManager?.ResumeAfterOceanRescue();
            });
        }

        private void HandleRewardedUnavailable()
        {
            if (!blockingInput || !rewardedRequestActive)
            {
                return;
            }

            rewardedRequestActive = false;
            oceanRescueUI?.ShowAdUnavailable();
            AdManager.Instance?.PrepareRewarded();
        }

        private static bool TryCreateOccupancy(
            BoardSnapshot snapshot,
            out bool[] occupancy)
        {
            int cellCount = GameConstants.BoardSize * GameConstants.BoardSize;
            occupancy = new bool[cellCount];
            if (snapshot == null
                || snapshot.colors == null
                || snapshot.colors.Length < cellCount)
            {
                return false;
            }

            for (int i = 0; i < cellCount; i++)
            {
                occupancy[i] = snapshot.colors[i] >= 0;
            }

            return true;
        }

        private static bool CanPlaceEntireSequence(
            bool[] initialOccupancy,
            string[] shapeIds)
        {
            bool[] occupancy = (bool[])initialOccupancy.Clone();
            return SearchPlacementSequence(occupancy, shapeIds, 0);
        }

        private static bool SearchPlacementSequence(
            bool[] occupancy,
            string[] shapeIds,
            int pieceIndex)
        {
            if (pieceIndex >= shapeIds.Length)
            {
                return true;
            }

            PieceData data = PieceCatalog.Get(shapeIds[pieceIndex]);
            for (int y = 0; y <= GameConstants.BoardSize - data.height; y++)
            {
                for (int x = 0; x <= GameConstants.BoardSize - data.width; x++)
                {
                    if (!CanPlace(occupancy, data, x, y))
                    {
                        continue;
                    }

                    bool[] next = (bool[])occupancy.Clone();
                    PlaceAndResolveLines(next, data, x, y);
                    if (SearchPlacementSequence(next, shapeIds, pieceIndex + 1))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CanPlace(
            bool[] occupancy,
            PieceData data,
            int originX,
            int originY)
        {
            for (int i = 0; i < data.cells.Length; i++)
            {
                int x = originX + data.cells[i].x;
                int y = originY + data.cells[i].y;
                int index = y * GameConstants.BoardSize + x;
                if (x < 0
                    || x >= GameConstants.BoardSize
                    || y < 0
                    || y >= GameConstants.BoardSize
                    || occupancy[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static void PlaceAndResolveLines(
            bool[] occupancy,
            PieceData data,
            int originX,
            int originY)
        {
            for (int i = 0; i < data.cells.Length; i++)
            {
                int x = originX + data.cells[i].x;
                int y = originY + data.cells[i].y;
                occupancy[y * GameConstants.BoardSize + x] = true;
            }

            bool[] clearRows = new bool[GameConstants.BoardSize];
            bool[] clearColumns = new bool[GameConstants.BoardSize];
            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                clearRows[y] = true;
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (!occupancy[y * GameConstants.BoardSize + x])
                    {
                        clearRows[y] = false;
                        break;
                    }
                }
            }

            for (int x = 0; x < GameConstants.BoardSize; x++)
            {
                clearColumns[x] = true;
                for (int y = 0; y < GameConstants.BoardSize; y++)
                {
                    if (!occupancy[y * GameConstants.BoardSize + x])
                    {
                        clearColumns[x] = false;
                        break;
                    }
                }
            }

            for (int y = 0; y < GameConstants.BoardSize; y++)
            {
                for (int x = 0; x < GameConstants.BoardSize; x++)
                {
                    if (clearRows[y] || clearColumns[x])
                    {
                        occupancy[y * GameConstants.BoardSize + x] = false;
                    }
                }
            }
        }

        private static PieceInstance[] CreateSet(string[] shapeIds)
        {
            PieceInstance[] set = new PieceInstance[GameConstants.TraySize];
            for (int i = 0; i < set.Length; i++)
            {
                set[i] = new PieceInstance(
                    shapeIds[i],
                    RescueColors[i % RescueColors.Length]);
            }

            return set;
        }

        private static PieceInstance[] CloneSet(PieceInstance[] source)
        {
            if (source == null)
            {
                return null;
            }

            PieceInstance[] clone = new PieceInstance[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                clone[i] = source[i]?.Clone();
            }

            return clone;
        }
    }
}
