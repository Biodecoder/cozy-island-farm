using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CozyFarm.Core;
using CozyFarm.Farming;
using CozyFarm.Player;

namespace CozyFarm.Testing
{
    [Serializable]
    public class AutopilotVerificationReport
    {
        public string timestamp;
        public bool allChecksPassed;
        public int initialCoins;
        public int finalCoins;
        public int totalPlanted;
        public int totalWatered;
        public int totalHarvested;
        public int totalSold;
        public string verifiedSeasons;
        public List<string>bugsFound = new List<string>();
        public List<string> milestoneLogs = new List<string>();
    }

    public class FarmAutopilotPlaytest : MonoBehaviour
    {
        public static FarmAutopilotPlaytest Instance { get; private set; }

        public bool autopilotActive = true;
        private float stepCooldown = 0f;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (!autopilotActive) return;
            if (FarmerController.Instance == null || FarmPlotGrid.Instance == null || CozyFarmState.Instance == null)
                return;

            stepCooldown -= Time.deltaTime;
            if (stepCooldown > 0f) return;

            FarmerController farmer = FarmerController.Instance;
            CozyFarmState state = CozyFarmState.Instance;
            FarmPlotGrid grid = FarmPlotGrid.Instance;

            // 1. If carrying >= 4 harvested crops, walk to the cobblestone Market Plaza to sell!
            if (state.harvestedPumpkinCount + state.harvestedTomatoCount >= 4)
            {
                farmer.MoveToPoint(farmer.marketPlazaPosition);
                if (farmer.HasReachedTarget(1.8f))
                {
                    state.SellAllHarvestedProduce();
                    stepCooldown = 0.6f;
                }
                return;
            }

            // 2. If any crop is Mature, walk to it and harvest!
            CropSlot mature = grid.FindNextMatureSlot();
            if (mature != null)
            {
                farmer.MoveToPoint(mature.transform.position);
                if (farmer.HasReachedTarget(0.85f))
                {
                    farmer.InteractWithSlot(mature);
                    stepCooldown = 0.35f;
                }
                return;
            }

            // 3. Otherwise find next unwatered/empty furrow slot, plant & water it!
            CropSlot nextSlot = grid.FindNextUnwateredOrEmptySlot();
            if (nextSlot != null)
            {
                farmer.MoveToPoint(nextSlot.transform.position);
                if (farmer.HasReachedTarget(0.85f))
                {
                    if (nextSlot.stage == CropStage.Empty)
                    {
                        CropType choice = (nextSlot.rowIndex % 2 == 0) ? CropType.Pumpkin : CropType.Tomato;
                        nextSlot.Plant(choice);
                        state.totalPlanted++;
                    }
                    farmer.TriggerWateringOnSlot(nextSlot);
                    stepCooldown = 0.38f;
                }
            }
        }

        public static AutopilotVerificationReport RunDeterministicSimulationVerify()
        {
            AutopilotVerificationReport report = new AutopilotVerificationReport
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                allChecksPassed = true
            };

            CozyFarmState state = CozyFarmState.Instance;
            FarmPlotGrid grid = FarmPlotGrid.Instance;
            FarmerController farmer = FarmerController.Instance;

            if (state == null || grid == null || farmer == null)
            {
                report.allChecksPassed = false;
                report.bugsFound.Add("Missing core singleton (CozyFarmState, FarmPlotGrid, or FarmerController).");
                return report;
            }

            report.initialCoins = state.coins;
            report.milestoneLogs.Add($"Initial state: Coins={state.coins}, Day={state.day}, Season={state.currentSeason}");

            // Clear any pre-seeded starter slots before running the 8-slot deterministic loop
            for (int i = 0; i < grid.interactiveSlots.Count; i++)
            {
                grid.interactiveSlots[i].plantedCrop = CropType.None;
                grid.interactiveSlots[i].stage = CropStage.Empty;
                grid.interactiveSlots[i].isWatered = false;
                grid.interactiveSlots[i].growthProgress = 0f;
            }

            // Step A: Plant & water 8 furrow mounds across the 7-furrow plot
            int plantedCount = 0;
            for (int i = 0; i < Mathf.Min(8, grid.interactiveSlots.Count); i++)
            {
                CropSlot slot = grid.interactiveSlots[i];
                CropType crop = (i % 2 == 0) ? CropType.Pumpkin : CropType.Tomato;
                if (slot.Plant(crop))
                {
                    state.totalPlanted++;
                    plantedCount++;
                }
                farmer.TriggerWateringOnSlot(slot);
            }
            report.milestoneLogs.Add($"Planted and watered {plantedCount} furrow slots.");

            // Step B: Simulate growth across 10 seconds to reach Mature stage
            for (int step = 0; step < 25; step++)
            {
                for (int i = 0; i < grid.interactiveSlots.Count; i++)
                {
                    grid.interactiveSlots[i].TickGrowth(0.5f, state.currentSeason);
                }
            }

            // Step C: Harvest mature crops
            int harvestedNow = 0;
            for (int i = 0; i < Mathf.Min(8, grid.interactiveSlots.Count); i++)
            {
                if (grid.interactiveSlots[i].stage == CropStage.Mature)
                {
                    farmer.InteractWithSlot(grid.interactiveSlots[i]);
                    harvestedNow++;
                }
            }
            report.milestoneLogs.Add($"Harvested {harvestedNow} mature crops.");

            // Step D: Walk to Market Stall and sell produce
            farmer.transform.position = farmer.marketPlazaPosition;
            int earned = state.SellAllHarvestedProduce();
            report.milestoneLogs.Add($"Sold harvested crops at Market Plaza for +{earned}G (New balance: {state.coins}G).");

            // Step E: Verify 4-Season cycle progression
            List<string> seasonsVisited = new List<string> { state.currentSeason.ToString() };
            for (int d = 0; d < 48; d++)
            {
                state.AdvanceDay();
                string sName = state.currentSeason.ToString();
                if (!seasonsVisited.Contains(sName))
                    seasonsVisited.Add(sName);
            }
            // Restore default Day 12 Spring for initial player launch
            state.day = 12;
            state.currentSeason = Season.Spring;
            state.coins = 1240;

            // Re-seed 5 sprouts in rows 2..4 so the opening scene matches @Mnilax frame_05
            for (int i = 0; i < grid.interactiveSlots.Count; i++)
            {
                grid.interactiveSlots[i].plantedCrop = CropType.None;
                grid.interactiveSlots[i].stage = CropStage.Empty;
                grid.interactiveSlots[i].isWatered = false;
                grid.interactiveSlots[i].growthProgress = 0f;
                grid.interactiveSlots[i].RefreshVisualState();
            }
            int[] starterIndices = { 1, 7, 13, 14, 20 };
            foreach (int idx in starterIndices)
            {
                if (idx < grid.interactiveSlots.Count)
                {
                    grid.interactiveSlots[idx].Plant(CropType.Pumpkin);
                    grid.interactiveSlots[idx].Water();
                }
            }

            report.finalCoins = report.initialCoins + earned;
            report.totalPlanted = state.totalPlanted;
            report.totalWatered = state.totalWatered;
            report.totalHarvested = state.totalHarvested;
            report.totalSold = state.totalSold;
            report.verifiedSeasons = string.Join(" -> ", seasonsVisited);

            if (plantedCount < 8 || harvestedNow < 8 || earned <= 0 || seasonsVisited.Count < 4)
            {
                report.allChecksPassed = false;
                report.bugsFound.Add("Loop verification incomplete.");
            }

            string logDir = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(logDir);
            File.WriteAllText(
                Path.Combine(logDir, "autopilot_report.json"),
                JsonUtility.ToJson(report, true)
            );
            return report;
        }
    }
}
