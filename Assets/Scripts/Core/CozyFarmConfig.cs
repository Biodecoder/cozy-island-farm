using System;

namespace CozyFarm.Core
{
    public enum Season
    {
        Spring = 0,
        Summer = 1,
        Autumn = 2,
        Winter = 3
    }

    public enum CropType
    {
        None = 0,
        Pumpkin = 1,
        Tomato = 2
    }

    public enum CropStage
    {
        Empty = 0,
        Seeded = 1,
        Sprout = 2,
        GrowingBush = 3,
        Mature = 4
    }

    [Serializable]
    public class HotbarSlotData
    {
        public int slotIndex;
        public string id;
        public string label;
        public int count;
    }
}
