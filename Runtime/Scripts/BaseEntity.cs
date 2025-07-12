
namespace Combatantelope {
    /// <summary>
    /// This is an encapsulation of an entity in the battle. It all of its information is stored in immutable state
    /// references for snapshotting.
    /// </summary>
    public abstract class BaseEntity<TState, TBuilder> : IBattleEntity 
            where TState : BaseEntity<TState, TBuilder>.State
            where TBuilder : BaseEntity<TState, TBuilder>.State.Builder {
        public TState EntityState;

        public abstract bool CanDoTurn { get; }

        public abstract float TimeToNextTurn { get; }

        public abstract string Name { get; }

        public void SetID(int id) { 
            EntityState = EntityState.ToBuilder().SetID(id).Build();
        }

        public abstract class State {
            /// <summary>
            /// Used to uniquely id the entity in the battle system. Will be autopopulated.
            /// </summary>
            public readonly int ID;

            public State(int id) {
                ID = id;
            }

            public abstract TBuilder ToBuilder();

            public abstract class Builder {
                protected int _id;

                public Builder(int id) {  this._id = id; }

                public Builder(State state) : this(state.ID) {
                }

                public TBuilder SetID(int id) { 
                    _id = id;
                    return (TBuilder)this;
                }

                public abstract TState Build();
            }
        }
    }
}