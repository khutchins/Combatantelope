using System.Collections.Generic;
using UnityEngine;

namespace Combatantelope.Kerfuffle {
    /// <summary>
    /// This is an encapsulation of an entity in the battle. It all of its information is stored in immutable state
    /// references for snapshotting.
    /// </summary>
    public class Entity : BaseEntity<Entity.State, Entity.State.Builder> {
        public static int TURN_DELAY = 100;
        public override bool CanDoTurn => EntityState.HP > 0;

        public override int TimeToNextTurn => EntityState.TurnDelay;

        public override string Name => EntityState.Attributes.Name;

        public Entity(EntityAttributes attrs) {
            EntityState = State.FromAttrs(attrs);
        }

        public void SetDefense(int amt) {
            EntityState = EntityState.ToBuilder().SetDefense(amt).Build();
        }

        public void TakeDamage(Battle.AttackSummary summary) {
            EntityState = EntityState.ToBuilder().TakeDamage(summary.TotalDamage, summary.Crit).Build();
        }

        public void SetCharging() {
            EntityState = EntityState.ToBuilder().SetCharging().Build();
        }

        public void DidCharge() {
            EntityState = EntityState.ToBuilder().ClearCharging().AddTurnDelay().Build();
        }

        public void SetInitiative(int initiative) {
            EntityState = EntityState.ToBuilder().SetInitiative(initiative).Build();
        }

        public void SetTurnDelay(int delay) {
            EntityState = EntityState.ToBuilder().SetTurnDelay(delay).Build();
        }

        public void AddTurnDelay() {
            EntityState = EntityState.ToBuilder().AddTurnDelay().Build();
        }

        public void TimePassed(int time) {
            EntityState = EntityState.ToBuilder().TimePassed(time).Build();
        }

        public new class State : BaseEntity<State, State.Builder>.State {
            public readonly int HP;
            public readonly int Defense;
            public readonly EntityAttributes Attributes;
            public readonly int TurnDelay;
            public readonly int Initiative;
            public readonly bool NextMoveIsCharge;

            public State(int id, int hp, int defense, EntityAttributes attributes, int intiative, int turnDelay, bool nextMoveIsCharge = false) : base(id) {
                HP = hp;
                Defense = defense;
                Attributes = attributes;
                Initiative = intiative;
                TurnDelay = turnDelay;
                NextMoveIsCharge = nextMoveIsCharge;
            }

            public static State FromAttrs(EntityAttributes attrs) {
                return new State(0, attrs.MaxHP, 0, attrs, 0, 0, false);
            }

            public List<Move> ValidMoves {
                get {
                    List<Move> validMoves = new List<Move> {
                        Move.Attack,
                        Move.Charge
                    };
                    if (Defense == 0) validMoves.Add(Move.Defend);
                    return validMoves;
                }
            }

            public override Builder ToBuilder() {
                return new Builder(this);
            }

            public new class Builder : BaseEntity<State, Builder>.State.Builder {
                private int _hp;
                private int _defense;
                private EntityAttributes _attributes;
                private int _initiative;
                private int _turnDelay;
                private bool _nextMoveIsCharge;

                public Builder(int id, int hp, int defense, EntityAttributes attributes, int initiative, int turnDelay, bool nextMoveIsCharge) : base(id) {
                    _hp = hp;
                    _defense = defense;
                    _attributes = attributes;
                    _initiative = initiative;
                    _turnDelay = turnDelay;
                    _nextMoveIsCharge = nextMoveIsCharge;
                }

                public Builder(State state) : this(state.ID, state.HP, state.Defense, state.Attributes, state.Initiative, state.TurnDelay, state.NextMoveIsCharge) {
                }

                public Builder SetHP(int hp) {
                    _hp = hp;
                    return this;
                }

                public Builder TakeDamage(int amt, bool bypassDefense) {
                    if (!bypassDefense) {
                        if (_defense > amt) {
                            _defense -= amt;
                            amt = 0;
                        }
                        if (_defense <= amt) {
                            amt -= _defense;
                            _defense = 0;
                        }
                    }
                    _hp = Mathf.Max(0, _hp - amt);
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

                public Builder AddTurnDelay() {
                    _turnDelay += TURN_DELAY;
                    return this;
                }

                public Builder TimePassed(int timePassed) {
                    _turnDelay -= timePassed;
                    return this;
                }

                public Builder SetInitiative(int initiative) {
                    _initiative = initiative;
                    return this;
                }

                public Builder SetCharging() {
                    _defense = 0;
                    _nextMoveIsCharge = true;
                    return this;
                }

                public Builder ClearCharging() {
                    _nextMoveIsCharge = false;
                    return this;
                }

                public override State Build() {
                    return new State(_id, _hp, _defense, _attributes, _initiative, _turnDelay, _nextMoveIsCharge);
                }
            }
        }
    }
}