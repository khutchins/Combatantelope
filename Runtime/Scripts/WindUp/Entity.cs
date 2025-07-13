using Codice.Utils;
using Combatantelope.Kerfuffle;
using PlasticGui.WorkspaceWindow.CodeReview.Comments;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        public int Heal(int amt) {
            int realAmt = (int)Mathf.Min(EntityState.Attributes.MaxHP - EntityState.HP, amt);
            EntityState = EntityState.ToBuilder().SetHP(EntityState.HP + realAmt).Build();
            return realAmt;
        }

        public void SetActiveMove(Move move) {
            EntityState = EntityState.ToBuilder().SetActiveMove(move).Build();
        }

        public void AddTicks(int ticks) {
            TimePassed(-ticks);
        }

        public void TimePassed(int time) {
            EntityState = EntityState.ToBuilder().TimePassed(time).Build();
        }

        public int TopUpStacks(Move move) {
            EntityState = EntityState.ToBuilder().TopUpStacks(move).Build();
            var stk = EntityState.Stacks.FirstOrDefault(x => x.AppliedMove == move);
            if (stk != null) {
                return stk.Count;
            } else {
                // Shouldn't happen.
                return 0;
            }
        }

        public void RemoveStack(Move move) {
            EntityState = EntityState.ToBuilder().RemoveStack(move).Build();
        }

        public class Stack {
            public readonly Move AppliedMove;
            public readonly int Count;

            public Stack(Move appliedMove, int count) {
                AppliedMove = appliedMove;
                Count = count;
            }

            public Stack TopUp(Move move) {
                int stacks = move.Attr is StackAttr stat ? stat.StacksToApply : 0;
                return new Stack(move, System.Math.Max(stacks, Count));
            }

            public Stack Decrease() {
                return new Stack(AppliedMove, Count - 1);
            }
        }

        public new class State : BaseEntity<State, State.Builder>.State {
            public readonly int HP;
            public readonly int Defense;
            public readonly EntityAttributes Attributes;
            public readonly int DelayRemaining;
            public readonly Move ActiveMove;
            public readonly bool FirstToAct;
            public readonly Stack[] Stacks;

            public State(int id, int hp, int defense, EntityAttributes attributes, Move activeMove, int delayRemaining, bool firstToAct, IEnumerable<Stack> stacks) : base(id) {
                HP = hp;
                Defense = defense;
                Attributes = attributes;
                ActiveMove = activeMove;
                DelayRemaining = delayRemaining;
                FirstToAct = firstToAct;
                Stacks = stacks.ToArray();
            }

            public static State FromAttrs(EntityAttributes attrs) {
                return new State(0, attrs.MaxHP, 0, attrs, null, 0, false, new Stack[0]);
            }

            public override Builder ToBuilder() {
                return new Builder(this);
            }

            public bool Dead() {
                return HP <= 0;
            }

            public bool TryGetStack(Move.Attribute attr, out Stack stack) {
                stack = Stacks.FirstOrDefault(x => x.AppliedMove.Attr.Attribute == attr);
                return stack != null;
            }

            public new class Builder : BaseEntity<State, Builder>.State.Builder {
                private int _hp;
                private int _defense;
                private EntityAttributes _attributes;
                private Move _activeMove;
                private int _initiative;
                private int _turnDelay;
                private bool _firstToAct;
                private List<Stack> _stacks;

                public Builder(int id, int hp, int defense, EntityAttributes attributes, Move activeMove, int turnDelay, bool firstToAct, IEnumerable<Stack> stacks) : base(id) {
                    _hp = hp;
                    _defense = defense;
                    _attributes = attributes;
                    _activeMove = activeMove;
                    _turnDelay = turnDelay;
                    _firstToAct = firstToAct;
                    _stacks = stacks.ToList();
                }

                public Builder(State state) : this(state.ID, state.HP, state.Defense, state.Attributes, state.ActiveMove, state.DelayRemaining, state.FirstToAct, state.Stacks.ToList()) {
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

                public Builder TopUpStacks(Move appliedMove) {
                    int idx = _stacks.FindIndex(x => x.AppliedMove == appliedMove);
                    if (idx < 0) {
                        int stacks = appliedMove.Attr is StackAttr stat ? stat.StacksToApply : 0;
                        _stacks.Add(new Stack(appliedMove, stacks));
                    } else {
                        Stack stk = _stacks[idx];
                        Stack newStk = stk.TopUp(appliedMove);
                        _stacks[idx] = newStk;
                    }
                    return this;
                }

                public Builder RemoveStack(Move move) {
                    int idx = _stacks.FindIndex(x => x.AppliedMove == move);
                    if (idx < 0) {
                        Debug.LogWarning($"No stack found for move {move}");
                        return null;
                    }
                    var stack = _stacks[idx];
                    int newCount = stack.Count - 1;
                    if (newCount <= 0) {
                        _stacks.Remove(stack);
                    } else {
                        _stacks[idx] = _stacks[idx].Decrease();
                    }
                    return this;
                }

                public override State Build() {
                    return new State(_id, _hp, _defense, _attributes, _activeMove, _turnDelay, _firstToAct, _stacks);
                }
            }
        }
    }
}