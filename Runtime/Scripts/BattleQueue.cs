using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Combatantelope {

    public interface IBattleEntity {
        string Name { get; }
        bool CanDoTurn { get; }
        int TimeToNextTurn { get; }
    }

    public enum QueuePriority {
        Low,
        High
    }

    public class BattleQueue<T> where T : IBattleEntity {

        readonly List<T> _entities;
        public readonly QueuePriority priority;

        public BattleQueue(IEnumerable<T> entities, QueuePriority priority) {
            _entities = entities.ToList();
            this.priority = priority;
        }

        public void AddEntity(T entity) {
            _entities.Add(entity);
        }

        public void RemoveEntity(T entity) {
            _entities.Remove(entity);
        }

        public T NextEntity() {
            return priority == QueuePriority.Low ? LowestEntity() : HighestEntity();
        }

        T LowestEntity() {
            T lowestE = default;
            int lowest = int.MaxValue;
            foreach (T entity in _entities) {
                if (!entity.CanDoTurn) continue;
                int next = entity.TimeToNextTurn;
                if (next < lowest) {
                    lowest = next;
                    lowestE = entity;
                }
            }
            return lowestE;
        }

        T HighestEntity() {
            T highestE = default;
            int highest = int.MinValue;
            foreach (T entity in _entities) {
                if (!entity.CanDoTurn) continue;
                int next = entity.TimeToNextTurn;
                if (next > highest) {
                    highest = next;
                    highestE = entity;
                }
            }
            return highestE;
        }

        public override string ToString() {
            var enumerable = _entities.Where(x => x.CanDoTurn);
            if (priority == QueuePriority.Low) {
                enumerable = enumerable.OrderBy(x => x.TimeToNextTurn);
            } else {
                enumerable = enumerable.OrderByDescending(x => x.TimeToNextTurn);
            }
            return $"[{string.Join(", ", enumerable.Select(x => $"({x.Name}: {x.TimeToNextTurn})"))}]";
        }
    }
}