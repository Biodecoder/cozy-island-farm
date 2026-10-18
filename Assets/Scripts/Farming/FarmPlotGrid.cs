using System.Collections.Generic;
using UnityEngine;
using CozyFarm.Core;

namespace CozyFarm.Farming
{
    public class FarmPlotGrid : MonoBehaviour
    {
        private static FarmPlotGrid instance;
        public static FarmPlotGrid Instance
        {
            get
            {
                if (instance == null)
                    instance = FindFirstObjectByType<FarmPlotGrid>();
                return instance;
            }
            private set { instance = value; }
        }

        public List<CropSlot> interactiveSlots = new List<CropSlot>();
        public List<CropSlot> northShowcaseSlots = new List<CropSlot>();

        private void Awake()
        {
            instance = this;
        }

        private void Update()
        {
            Season season = CozyFarmState.Instance != null
                ? CozyFarmState.Instance.currentSeason
                : Season.Spring;

            float dt = Time.deltaTime;
            for (int i = 0; i < interactiveSlots.Count; i++)
            {
                if (interactiveSlots[i] != null)
                {
                    interactiveSlots[i].TickGrowth(dt, season);
                }
            }
        }

        public CropSlot FindNearestSlot(Vector3 worldPos, float maxRadius)
        {
            CropSlot best = null;
            float bestDistSq = maxRadius * maxRadius;
            for (int i = 0; i < interactiveSlots.Count; i++)
            {
                CropSlot slot = interactiveSlots[i];
                if (slot == null) continue;
                Vector3 d = slot.transform.position - worldPos;
                d.y = 0f;
                float dSq = d.sqrMagnitude;
                if (dSq <= bestDistSq)
                {
                    bestDistSq = dSq;
                    best = slot;
                }
            }
            return best;
        }

        public CropSlot FindNextUnwateredOrEmptySlot()
        {
            for (int i = 0; i < interactiveSlots.Count; i++)
            {
                CropSlot s = interactiveSlots[i];
                if (s != null && !s.isWatered && s.stage != CropStage.Mature)
                    return s;
            }
            return null;
        }

        public CropSlot FindNextMatureSlot()
        {
            for (int i = 0; i < interactiveSlots.Count; i++)
            {
                CropSlot s = interactiveSlots[i];
                if (s != null && s.stage == CropStage.Mature)
                    return s;
            }
            return null;
        }
    }
}
