using System.Collections.Generic;
using UnityEngine;
using CozyFarm.Core;

namespace CozyFarm.World
{
    public class DayNightSeasonController : MonoBehaviour
    {
        public static DayNightSeasonController Instance { get; private set; }

        public Light directionalSun;
        public Camera mainCamera;
        public Renderer grassTerrainRenderer;
        public Renderer oceanWaterRenderer;
        public List<Light> streetLanternLights = new List<Light>();
        public List<Renderer> lanternBulbRenderers = new List<Renderer>();

        [Tooltip("Duration in seconds for a full day-to-dusk-to-dawn cycle")]
        public float dayDurationSeconds = 48f;
        public bool autoAdvanceTime = true;

        private void Awake()
        {
            Instance = this;
        }

        private void Update()
        {
            if (CozyFarmState.Instance == null) return;

            if (autoAdvanceTime)
            {
                CozyFarmState.Instance.timeOfDay += Time.deltaTime / Mathf.Max(10f, dayDurationSeconds);
                if (CozyFarmState.Instance.timeOfDay >= 1.0f)
                {
                    CozyFarmState.Instance.timeOfDay -= 1.0f;
                    CozyFarmState.Instance.AdvanceDay();
                }
            }

            ApplyLightingAndSeason(CozyFarmState.Instance.timeOfDay, CozyFarmState.Instance.currentSeason);
        }

        public void ApplyLightingAndSeason(float t, Season season)
        {
            // t in [0..1]:
            // 0.00 - 0.45: Bright sunny mid-day (matching frame_01..frame_03)
            // 0.45 - 0.78: Warm golden sunset / dusk (matching frame_05..frame_14)
            // 0.78 - 1.00: Cozy twilight with glowing lanterns
            float sunsetBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.38f, 0.72f, t));
            float duskBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.50f, 0.78f, t));

            Color middaySky = new Color(0.55f, 0.87f, 0.93f);
            Color sunsetSky = new Color(0.92f, 0.63f, 0.51f);
            Color skyColor = Color.Lerp(middaySky, sunsetSky, sunsetBlend);

            Color middaySun = new Color(1.0f, 0.97f, 0.88f);
            Color sunsetSun = new Color(1.0f, 0.67f, 0.42f);

            if (mainCamera != null)
            {
                mainCamera.backgroundColor = skyColor;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 78f;
            RenderSettings.fogColor = skyColor;
            RenderSettings.ambientLight = Color.Lerp(
                new Color(0.68f, 0.74f, 0.68f),
                new Color(0.66f, 0.46f, 0.40f),
                sunsetBlend
            );

            if (directionalSun != null)
            {
                directionalSun.color = Color.Lerp(middaySun, sunsetSun, sunsetBlend);
                directionalSun.intensity = Mathf.Lerp(1.35f, 0.92f, duskBlend);
                float pitch = Mathf.Lerp(48f, 24f, sunsetBlend);
                float yaw = Mathf.Lerp(-32f, -58f, sunsetBlend);
                directionalSun.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            // Seasonal grass tint
            if (grassTerrainRenderer != null)
            {
                Color springGrass = new Color(0.55f, 0.76f, 0.28f);
                Color summerGrass = new Color(0.48f, 0.73f, 0.24f);
                Color autumnGrass = new Color(0.68f, 0.66f, 0.26f);
                Color winterGrass = new Color(0.74f, 0.84f, 0.76f);

                Color baseGrass = springGrass;
                if (season == Season.Summer) baseGrass = summerGrass;
                else if (season == Season.Autumn) baseGrass = autumnGrass;
                else if (season == Season.Winter) baseGrass = winterGrass;

                SetRendererColor(grassTerrainRenderer, Color.Lerp(
                    baseGrass,
                    baseGrass * new Color(0.95f, 0.82f, 0.72f),
                    sunsetBlend * 0.65f
                ));
            }

            // Ocean water tint (turquoise midday -> rose-gold reflection at sunset as in frame_08..14)
            if (oceanWaterRenderer != null)
            {
                Color middayWater = new Color(0.32f, 0.76f, 0.80f);
                Color sunsetWater = new Color(0.68f, 0.50f, 0.54f);
                SetRendererColor(oceanWaterRenderer, Color.Lerp(middayWater, sunsetWater, sunsetBlend));
            }

            // Street lanterns glow warmly in sunset/dusk (frame_05..frame_14)
            float lanternIntensity = Mathf.Lerp(0.3f, 3.2f, duskBlend);
            for (int i = 0; i < streetLanternLights.Count; i++)
            {
                if (streetLanternLights[i] != null)
                {
                    streetLanternLights[i].intensity = lanternIntensity;
                }
            }

            Color bulbColor = Color.Lerp(
                new Color(1.0f, 0.88f, 0.55f),
                new Color(1.0f, 0.78f, 0.30f),
                duskBlend
            );
            for (int i = 0; i < lanternBulbRenderers.Count; i++)
            {
                if (lanternBulbRenderers[i] != null)
                {
                    SetRendererColor(lanternBulbRenderers[i], bulbColor);
                }
            }
        }

        private static void SetRendererColor(Renderer r, Color c)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", c);
            r.SetPropertyBlock(mpb);
        }
    }
}
