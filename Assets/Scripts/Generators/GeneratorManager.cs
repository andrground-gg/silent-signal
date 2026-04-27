using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeneratorSystem
{
    public class GeneratorManager : Singleton<GeneratorManager>
    {
        public const int TOTAL_GENERATORS       = 6;
        public const int FUNCTIONAL_GENERATORS  = 3;
        public const int DUMMY_GENERATORS       = 3;
        public const int MAX_ACTIVE_GENERATORS  = 2;

        private Dictionary<GeneratorID, GeneratorData> _generators;

        private Queue<GeneratorID> _activeQueue = new Queue<GeneratorID>();

        public event Action<GeneratorID> OnGeneratorActivated;
        public event Action<GeneratorID> OnGeneratorDeactivated;
        public event Action<GeneratorID, GeneratorID> OnGeneratorAutoSwitched; 

        // public event Action<GeneratorID> OnGeneratorActivated;
        protected override void Awake()
        {
            base.Awake();
            InitGenerators();
        }

        private void InitGenerators()
        {
            _generators = new Dictionary<GeneratorID, GeneratorData>
            {
                { GeneratorID.GENERATOR_LIGHTHOUSE,      new GeneratorData(GeneratorID.GENERATOR_LIGHTHOUSE,      true)  },
                { GeneratorID.GENERATOR_SIGNAL_TOWERS,   new GeneratorData(GeneratorID.GENERATOR_SIGNAL_TOWERS,   true)  },
                { GeneratorID.GENERATOR_RESEARCH_STATION,new GeneratorData(GeneratorID.GENERATOR_RESEARCH_STATION,true)  },
                { GeneratorID.GENERATOR_DUMMY_1,         new GeneratorData(GeneratorID.GENERATOR_DUMMY_1,         false) },
                { GeneratorID.GENERATOR_DUMMY_2,         new GeneratorData(GeneratorID.GENERATOR_DUMMY_2,         false) },
                { GeneratorID.GENERATOR_DUMMY_3,         new GeneratorData(GeneratorID.GENERATOR_DUMMY_3,         false) },
            };
        }

        public void ToggleGenerator(GeneratorID id)
        {
            if (!_generators.TryGetValue(id, out GeneratorData gen))
            {
                Debug.LogWarning($"[GeneratorManager] Unknown generator id: {id}");
                return;
            }

            if (gen.isActive)
                DeactivateGenerator(id, forced: false);
            else
                TryActivateGenerator(id);
        }

        public bool IsActive(GeneratorID id) =>
            _generators.TryGetValue(id, out var g) && g.isActive;

        public bool IsFunctional(GeneratorID id) =>
            _generators.TryGetValue(id, out var g) && g.isFunctional;

        public int ActiveCount => _activeQueue.Count;

        public IReadOnlyList<GeneratorID> GetActiveGenerators() =>
            new List<GeneratorID>(_activeQueue);

        private void TryActivateGenerator(GeneratorID id)
        {
            if (_activeQueue.Count >= MAX_ACTIVE_GENERATORS)
            {
                GeneratorID oldest = _activeQueue.Dequeue();
                ForceDeactivate(oldest);

                Debug.Log($"[GeneratorManager] Auto-shutting {oldest} to make room for {id}");
                Activate(id);
                OnGeneratorAutoSwitched?.Invoke(oldest, id);
            }
            else
            {
                Activate(id);
            }
        }

        private void DeactivateGenerator(GeneratorID id, bool forced)
        {
            if (!_generators[id].isActive) return;

            var temp = new List<GeneratorID>(_activeQueue);
            temp.Remove(id);
            _activeQueue = new Queue<GeneratorID>(temp);

            _generators[id].isActive = false;
            Debug.Log($"[GeneratorManager] {id} deactivated (forced={forced})");
            OnGeneratorDeactivated?.Invoke(id);
        }

        private void Activate(GeneratorID id)
        {
            _generators[id].isActive = true;
            _activeQueue.Enqueue(id);
            Debug.Log($"[GeneratorManager] {id} activated");
            OnGeneratorActivated?.Invoke(id);
        }

        private void ForceDeactivate(GeneratorID id)
        {
            _generators[id].isActive = false;
            Debug.Log($"[GeneratorManager] {id} force-deactivated");
            OnGeneratorDeactivated?.Invoke(id);
        }

        public void NotifyActivated(GeneratorID id)
        {
            
        }
        
        public void NotifyDeactivated(GeneratorID id)
        {
            
        }

    }
}
