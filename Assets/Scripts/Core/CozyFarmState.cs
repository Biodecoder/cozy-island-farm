using System;
using UnityEngine;

namespace CozyFarm.Core
{
    public class CozyFarmState : MonoBehaviour
    {
        private static CozyFarmState instance;
        public static CozyFarmState Instance
        {
            get
            {
                if (instance == null)
                    instance = FindFirstObjectByType<CozyFarmState>();
                return instance;
            }
            private set { instance = value; }
        }

        [Header("Economy & Calendar")]
        public int coins = 1240;
        public int day = 12;
        public Season currentSeason = Season.Spring;
        [Range(0f, 1f)]
        public float timeOfDay = 0.28f; // 0.25 = bright midday, 0.62 = golden sunset, 0.80 = dusk lanterns

        [Header("Hotbar Inventory (6 Slots matching @Mnilax HUD)")]
        public int selectedHotbarSlot = 0; // 0..5
        public int wateringCanCount = 1;
        public int pumpkinSeedCount = 12;
        public int harvestedPumpkinCount = 4;
        public int tomatoSeedCount = 10;
        public int harvestedTomatoCount = 3;
        public int hoeCount = 1;

        [Header("Telemetry Counters")]
        public int totalPlanted = 0;
        public int totalWatered = 0;
        public int totalHarvested = 0;
        public int totalSold = 0;
        public int totalCoinsEarned = 0;
        public float coinPulseTimer = 0f;

        public event Action<int, int> OnCoinsEarned;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void Update()
        {
            if (coinPulseTimer > 0f)
            {
                coinPulseTimer = Mathf.Max(0f, coinPulseTimer - Time.deltaTime);
            }
        }

        public float GetSeasonGrowthMultiplier(CropType crop)
        {
            switch (currentSeason)
            {
                case Season.Spring:
                    return 1.0f;
                case Season.Summer:
                    return crop == CropType.Tomato ? 1.30f : 1.10f;
                case Season.Autumn:
                    return crop == CropType.Pumpkin ? 1.35f : 1.0f;
                case Season.Winter:
                    return 0.80f;
                default:
                    return 1.0f;
            }
        }

        public int GetCropSellPrice(CropType crop)
        {
            int basePrice = crop == CropType.Pumpkin ? 45 : 30;
            if (currentSeason == Season.Autumn && crop == CropType.Pumpkin)
                return Mathf.RoundToInt(basePrice * 1.35f);
            if (currentSeason == Season.Winter)
                return Mathf.RoundToInt(basePrice * 1.25f);
            return basePrice;
        }

        public int SellAllHarvestedProduce()
        {
            int pumpkins = harvestedPumpkinCount;
            int tomatoes = harvestedTomatoCount;
            if (pumpkins <= 0 && tomatoes <= 0)
                return 0;

            int earned = pumpkins * GetCropSellPrice(CropType.Pumpkin)
                       + tomatoes * GetCropSellPrice(CropType.Tomato);

            harvestedPumpkinCount = 0;
            harvestedTomatoCount = 0;
            coins += earned;
            totalSold += pumpkins + tomatoes;
            totalCoinsEarned += earned;
            coinPulseTimer = 1.6f;

            // Replenish seeds slightly on market visit so loop never stalls
            pumpkinSeedCount = Mathf.Max(pumpkinSeedCount, 8);
            tomatoSeedCount = Mathf.Max(tomatoSeedCount, 8);

            OnCoinsEarned?.Invoke(earned, coins);
            return earned;
        }

        public void AdvanceDay()
        {
            day++;
            if (day > 15)
            {
                day = 1;
                currentSeason = (Season)(((int)currentSeason + 1) % 4);
            }
        }
    }
}
