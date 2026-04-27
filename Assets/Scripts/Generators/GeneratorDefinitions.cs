using UnityEngine;

namespace GeneratorSystem
{
    public enum GeneratorID
    {
        GENERATOR_LIGHTHOUSE = 0,
        GENERATOR_SIGNAL_TOWERS = 1,
        GENERATOR_RESEARCH_STATION = 2,
        GENERATOR_DUMMY_1 = 3,
        GENERATOR_DUMMY_2 = 4,
        GENERATOR_DUMMY_3 = 5
    }

    [System.Serializable]
    public class GeneratorData
    {
        public GeneratorID id;
        public bool isFunctional;
        public bool isActive;

        public GeneratorData(GeneratorID id, bool isFunctional)
        {
            this.id = id;
            this.isFunctional = isFunctional;
            this.isActive = false;
        }
    }
}
