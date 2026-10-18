using UnityEngine;
using CozyFarm.Core;

namespace CozyFarm.Farming
{
    public class CropSlot : MonoBehaviour
    {
        public int rowIndex;
        public int colIndex;
        public bool isDecorativeNorthPlot;

        public CropType plantedCrop = CropType.None;
        public CropStage stage = CropStage.Empty;
        public bool isWatered = false;
        public float growthProgress = 0f; // 0..1

        public Renderer soilMoundRenderer;
        public GameObject sproutVisual;
        public GameObject bushVisual;
        public GameObject pumpkinVisual;
        public GameObject tomatoVisual;

        private static readonly Color DrySoilColor = new Color(0.42f, 0.25f, 0.14f);
        private static readonly Color WetSoilColor = new Color(0.26f, 0.15f, 0.08f);

        public void InitializeVisuals(
            Renderer moundRenderer,
            GameObject sproutObj,
            GameObject bushObj,
            GameObject pumpkinObj,
            GameObject tomatoObj)
        {
            soilMoundRenderer = moundRenderer;
            sproutVisual = sproutObj;
            bushVisual = bushObj;
            pumpkinVisual = pumpkinObj;
            tomatoVisual = tomatoObj;
            RefreshVisualState();
        }

        public bool CanPlant()
        {
            return !isDecorativeNorthPlot && stage == CropStage.Empty;
        }

        public bool Plant(CropType type)
        {
            if (!CanPlant() || type == CropType.None)
                return false;

            plantedCrop = type;
            stage = CropStage.Seeded;
            growthProgress = 0.05f;
            RefreshVisualState();
            return true;
        }

        public bool Water()
        {
            if (isDecorativeNorthPlot)
                return false;

            bool changed = !isWatered;
            isWatered = true;
            if (stage == CropStage.Seeded)
            {
                stage = CropStage.Sprout;
                growthProgress = Mathf.Max(growthProgress, 0.22f);
                changed = true;
            }
            else if (stage == CropStage.Empty)
            {
                // In @Mnilax video, watering a pre-seeded furrow immediately pops a green sprout
                plantedCrop = CropType.Pumpkin;
                stage = CropStage.Sprout;
                growthProgress = 0.25f;
                changed = true;
            }
            RefreshVisualState();
            return changed;
        }

        public void TickGrowth(float deltaTime, Season season)
        {
            if (isDecorativeNorthPlot || stage == CropStage.Empty || stage == CropStage.Mature)
                return;
            if (!isWatered)
                return;

            float mult = CozyFarmState.Instance != null
                ? CozyFarmState.Instance.GetSeasonGrowthMultiplier(plantedCrop)
                : 1.0f;

            // Fast cozy diorama growth (~8 seconds from sprout to harvestable)
            growthProgress += (deltaTime / 8.0f) * mult;

            if (growthProgress >= 1.0f)
            {
                growthProgress = 1.0f;
                stage = CropStage.Mature;
                RefreshVisualState();
            }
            else if (growthProgress >= 0.58f && stage != CropStage.GrowingBush)
            {
                stage = CropStage.GrowingBush;
                RefreshVisualState();
            }
            else if (growthProgress >= 0.18f && stage == CropStage.Seeded)
            {
                stage = CropStage.Sprout;
                RefreshVisualState();
            }

            // Subtle bounce animation for mature crops
            if (stage == CropStage.Mature)
            {
                float pulse = 1.0f + Mathf.Sin(Time.time * 3.2f + rowIndex + colIndex) * 0.03f;
                if (pumpkinVisual != null && pumpkinVisual.activeSelf)
                    pumpkinVisual.transform.localPosition = new Vector3(0f, (pulse - 1.0f) * 0.6f, 0f);
                if (tomatoVisual != null && tomatoVisual.activeSelf)
                    tomatoVisual.transform.localPosition = new Vector3(0f, (pulse - 1.0f) * 0.6f, 0f);
            }
        }

        public CropType Harvest()
        {
            if (stage != CropStage.Mature || plantedCrop == CropType.None)
                return CropType.None;

            CropType harvested = plantedCrop;
            plantedCrop = CropType.None;
            stage = CropStage.Empty;
            isWatered = false;
            growthProgress = 0f;
            RefreshVisualState();
            return harvested;
        }

        public void RefreshVisualState()
        {
            if (soilMoundRenderer != null)
            {
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                soilMoundRenderer.GetPropertyBlock(mpb);
                mpb.SetColor("_Color", isWatered ? WetSoilColor : DrySoilColor);
                soilMoundRenderer.SetPropertyBlock(mpb);
            }

            bool showSprout = stage == CropStage.Seeded || stage == CropStage.Sprout;
            bool showBush = stage == CropStage.GrowingBush;
            bool showPumpkin = stage == CropStage.Mature && plantedCrop == CropType.Pumpkin;
            bool showTomato = stage == CropStage.Mature && plantedCrop == CropType.Tomato;

            if (sproutVisual != null) sproutVisual.SetActive(showSprout);
            if (bushVisual != null) bushVisual.SetActive(showBush);
            if (pumpkinVisual != null) pumpkinVisual.SetActive(showPumpkin);
            if (tomatoVisual != null) tomatoVisual.SetActive(showTomato);
        }
    }
}
