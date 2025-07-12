using System.Collections.Generic;
using UnityEngine;

namespace Combatantelope.WindUp {
    /// <summary>
    /// This is an encapsulation of an entity in the battle. It all of its information is stored in immutable state
    /// references for snapshotting.
    /// </summary>
    public class Entity : BaseEntity<Entity.State, Entity.State.Builder> {
        public override bool CanDoTurn => EntityState.HP > 0;

        public override float TimeToNextTurn => EntityState.DelayRemaining + (EntityState.FirstToAct ? 0 : 0.5f);

        public override string Name => EntityState.Attributes.Name;

        public Entity(EntityAttributes attrs) {
            EntityState = State.FromAttrs(attrs);
        }

        public void SetDefense(int amt) {
            EntityState = EntityState.ToBuilder().SetDefense(amt).Build();
        }

        public void TakeDamage(int damage) {
            EntityState = EntityState.ToBuilder().TakeDamage(damage).Build();
        }

        public void SetActiveMove(Move move) {
            EntityState = EntityState.ToBuilder().SetActiveMove(move).Build();
        }

        public void Tick(int ticks) {
            EntityState = EntityState.ToBuilder().Tick(ticks).Build();
        }

        public void TimePassed(int time) {
            EntityState = EntityState.ToBuilder().TimePassed(time).Build();
        }

        public new class State : BaseEntity<State, State.Builder>.State {
            public readonly int HP;
            public readonly int Defense;
            public readonly EntityAttributes Attributes;
            public readonly int DelayRemaining;
            public readonly Move ActiveMove;
            public readonly bool FirstToAct;

            public State(int id, int hp, int defense, EntityAttributes attributes, Move activeMove, int delayRemaining, bool firstToAct) : base(id) {
                HP = hp;
                Defense = defense;
                Attributes = attributes;
                ActiveMove = activeMove;
                DelayRemaining = delayRemaining;
                FirstToAct = firstToAct;
            }

            public static State FromAttrs(EntityAttributes attrs) {
                return new State(0, attrs.MaxHP, 0, attrs, null, 0, false);
            }

            public override Builder ToBuilder() {
                return new Builder(this);
            }

            public new class Builder : BaseEntity<State, Builder>.State.Builder {
                private int _hp;
                private int _defense;
                private EntityAttributes _attributes;
                private Move _activeMove;
                private int _initiative;
                private int _turnDelay;
                private bool _firstToAct;

                public Builder(int id, int hp, int defense, EntityAttributes attributes, Move activeMove, int turnDelay, bool firstToAct) : base(id) {
                    _hp = hp;
                    _defense = defense;
                    _attributes = attributes;
                    _activeMove = activeMove;
                    _turnDelay = turnDelay;
                    _firstToAct = firstToAct;
                }

                public Builder(State state) : this(state.ID, state.HP, state.Defense, state.Attributes, state.ActiveMove, state.DelayRemaining, state.FirstToAct) {
                }

                public Builder SetHP(int hp) {
                    _hp = hp;
                    return this;
                }

                public Builder TakeDamage(int damage) {
                    _hp = (int)Mathf.Max(0, _hp - damage);
                    return this;
                }

                public Builder SetDefense(int defense) {
                    _defense += defense;
                    return this;
                }

                public Builder SetTurnDelay(int turnDelay) {
                    _turnDelay = turnDelay;
                    return this;
                }

                public Builder Tick(int amount) {
                    _turnDelay -= amount;
                    return this;
                }

                public Builder TimePassed(int timePassed) {
                    _turnDelay -= timePassed;
                    return this;
                }

                public Builder SetActiveMove(Move move) {
                    _activeMove = move;
                    _turnDelay = _activeMove.MoveBattleStats.Delay;
                    return this;
                }

                public Builder SetFirstToAct(bool fta) {
                    _firstToAct = fta;
                    return this;
                }

                public override State Build() {
                    return new State(_id, _hp, _defense, _attributes, _activeMove, _turnDelay, _firstToAct);
                }
            }
        }
    }
}