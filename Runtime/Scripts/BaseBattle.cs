using Pomerandomian;
using System.Collections.Generic;
using System.Linq;

namespace Combatantelope {

    public interface IBattleListener<T> {
        void EventHappened(List<T> allevents, T thisEvent);
    }

    public abstract class BaseBattle<TEvent, TEntity, TEntityState, TEntityBuilder>
            where TEvent : BaseBattle<TEvent, TEntity, TEntityState, TEntityBuilder>.BaseEvent
            where TEntity : BaseEntity<TEntityState, TEntityBuilder> 
            where TEntityState : BaseEntity<TEntityState, TEntityBuilder>.State 
            where TEntityBuilder : BaseEntity<TEntityState, TEntityBuilder>.State.Builder {

        protected readonly List<TEntity> _entities;
        protected readonly IRandom _random;
        private readonly List<TEvent> _events = new List<TEvent>();
        private readonly List<IBattleListener<TEvent>> _listeners = new List<IBattleListener<TEvent>>();

        public BaseBattle(List<TEntity> entities, IRandom random = null) {
            _entities = new List<TEntity>();
            _entities.AddRange(entities);
            for (int i = 0; i < entities.Count; i++) {
                entities[i].SetID(i);
            }
            _random = random ?? new SystemRandom();
        }

        public void RegisterListener(IBattleListener<TEvent> listener) {
            UnregisterListener(listener);
            _listeners.Add(listener);
        }

        public void UnregisterListener(IBattleListener<TEvent> listener) {
            _listeners.Remove(listener);
        }

        public TEntity GetPlayer(TEntityState snap) {
            return _entities.Where(x => x.EntityState.ID == snap.ID).FirstOrDefault();
        }

        public abstract void StartBattle();

        protected TEntityState[] States() {
            return _entities.Select(x => x.EntityState).ToArray();
        }

        protected void SendEvent(TEvent eventToSend) {
            _events.Add(eventToSend);
            foreach (IBattleListener<TEvent> listener in _listeners) {
                listener.EventHappened(_events, eventToSend);
            }
        }

        public class BaseEvent {
            public readonly TEntityState[] Snapshots;

            public BaseEvent(TEntityState[] snapshots) {
                Snapshots = snapshots;
            }
        }
    }
}