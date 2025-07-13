using Pomerandomian;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Combatantelope.WindUp {
    public class Battle : BaseBattle<Battle.BEvent, Entity, Entity.State, Entity.State.Builder> {
        private Entity _currentEntity;
        private BattleQueue<Entity> _queue;
        private State _battleState = State.WaitingForStart;

        enum State {
            WaitingForStart,
            WaitingForMove,
            BattleOver
        }

        public Battle(Entity entity1, Entity entity2, IRandom random) : base(new List<Entity>() { entity1, entity2 }, random) {
            _queue = new BattleQueue<Entity>(_entities, QueuePriority.Low);
        }

        public override void StartBattle() {
            if (_battleState != State.WaitingForStart) {
                Debug.LogWarning("Attempting to start started battle!");
                return;
            }
            SendEvent(new BEventStart(States()));
            DoNextTurn();
        }

        public void TriggerLoss(Entity.State playerState) {
            var player = GetPlayer(playerState);
            int dmg = 9999;
            player.TakeDamage(dmg);
            SendEvent(new BEventBonusDamageOccurred(States(), player, dmg));
            SendBattleEnded(Other(player));
        }

        private Entity Other(Entity.State entity) {
            var player = GetPlayer(entity);
            return Other(player);
        }

        private Entity Other(Entity entity) {
            if (_entities.Count < 2) return null;
            return _entities[0] != entity ? _entities[0] : _entities[1];
        }

        private void SendBattleEnded(Entity winner) {
            SendEvent(new BEventBattleEnded(States(), winner, Other(winner)));
        }

        public void ScheduleMove(Entity.State playerState, Move move) {
            var player = GetPlayer(playerState);
            if (player != _currentEntity) {
                Debug.LogWarning("Got new move from non-awaiting player. Ignoring.");
                return;
            }

            _currentEntity.SetActiveMove(move);
            SendEvent(new BEventMoveChosen(States(), player, move));

            Debug.Log($"{player} chose {move}.");

            DoNextTurn();
        }

        void PrintStates() {
            for (int i = 0; i < _entities.Count; i++) {
                Debug.Log($"{_entities[i]}");
            }
        }

        public static int ComputeDamage(Move attack, Move defense) {
            if (attack.Attr.Attribute == Move.Attribute.Heal) return 0;
            if (defense == null) return attack.MoveBattleStats.Effect;
            return attack.Attr.Attribute == Move.Attribute.Piercing
                ? attack.MoveBattleStats.Effect : Mathf.Max(0, attack.MoveBattleStats.Effect - defense.MoveBattleStats.Defense);
        }

        public static int ComputeReflect(Move attack, Move defense) {
            if (defense == null || defense.Attr.Attribute != Move.Attribute.Reflect) return 0;
            if (attack.Attr.Attribute == Move.Attribute.Piercing) return 0;
            return Mathf.Min(attack.MoveBattleStats.Effect, defense.MoveBattleStats.Defense);
        }

        public static bool WillHitBeforeMoveChange(Move attack, Entity.State player) {
            return (player.FirstToAct && attack.MoveBattleStats.Delay < player.DelayRemaining)
                || attack.MoveBattleStats.Delay <= player.DelayRemaining;
        }

        void DoNextTurn() {
            _currentEntity = _queue.NextEntity();
            int nextDelay = _currentEntity.EntityState.DelayRemaining;

            foreach (var entity in _entities) {
                entity.TimePassed(nextDelay);
            }
            SendEvent(new BEventTicksPassed(States(), nextDelay));

            if (_currentEntity == null) {
                Debug.LogError("No player hit delay zero. This is going to be a bug.");
                return;
            }

            var otherPlayer = Other(_currentEntity);

            if (_currentEntity.EntityState.ActiveMove != null) {
                var movingPlayer = _currentEntity;
                Move move = movingPlayer.EntityState.ActiveMove;

                if (move.Attr.Attribute == Move.Attribute.Heal) {
                    var amt = movingPlayer.Heal(move.MoveBattleStats.Effect);
                    SendEvent(new BEEventHealMoveOccurred(States(), movingPlayer, movingPlayer, move, amt));
                } else {
                    int dmg = ComputeDamage(move, otherPlayer.EntityState.ActiveMove);

                    if (dmg > 0 && move.AppliesStacks) {
                        //Debug.Log($"{movingPlayer.player} used {move} and it applied {move.StacksToApply} stacks of {move.Attr}");
                        int count = otherPlayer.TopUpStacks(move);
                        SendEvent(new BEventStackModified(States(), otherPlayer, move, count));
                    }

                    otherPlayer.TakeDamage(dmg);
                    SendEvent(new BEventMoveOccurred(States(), movingPlayer, otherPlayer, movingPlayer.EntityState.ActiveMove, otherPlayer.EntityState.ActiveMove, dmg));

                    if (move.Attr.Attribute == Move.Attribute.Stun && move.Attr is EffectAttr eat) {
                        otherPlayer.AddTicks(eat.Effect);
                        SendEvent(new BEventMoveDelayChanged(States(), otherPlayer, otherPlayer.EntityState.DelayRemaining, eat.Effect));
                    }

                    if (otherPlayer.EntityState.ActiveMove.Attr.Attribute == Move.Attribute.Reflect) {
                        int deflected = move.MoveBattleStats.Effect - dmg;
                        if (deflected > 0) {
                            int deflectDmg = deflected;
                            movingPlayer.TakeDamage(deflectDmg);
                            SendEvent(new BEventBonusDamageOccurred(States(), movingPlayer, deflectDmg));
                        }
                    }

                    if (move.Attr.Attribute == Move.Attribute.Vampiric && dmg > 0) {
                        int steal = dmg / 2;
                        //Debug.Log($"{movingPlayer.player} stole {steal} health!");
                        movingPlayer.Heal(steal);
                        SendEvent(new BEEventHealMoveOccurred(States(), movingPlayer, movingPlayer, move, -steal));
                    }
                }

                if (otherPlayer.EntityState.Dead()) {
                    SendBattleEnded(movingPlayer);
                    return;
                }

                if (movingPlayer.EntityState.TryGetStack(Move.Attribute.Bleed, out var stack)) {
                    int newCount = stack.Count - 1;
                    movingPlayer.RemoveStack(stack.AppliedMove);
                    SendEvent(new BEventStackModified(States(), movingPlayer, stack.AppliedMove, newCount));
                    int dmg = (stack.AppliedMove.Attr as StackAttr).DamageOnApply;
                    movingPlayer.TakeDamage(dmg);
                    SendEvent(new BEventBonusDamageOccurred(States(), movingPlayer, dmg));
                }

                if (movingPlayer.EntityState.Dead()) {
                    SendBattleEnded(otherPlayer);
                    return;
                }
            }
            PrintStates();

            SendEvent(new BEventAwaitingMove(States(), _currentEntity));
        }

        public class BEvent : BaseEvent {

            public BEvent(Entity.State[] snapshots) : base(snapshots) { }
        }

        public class BEventStart : BEvent {
            public BEventStart(Entity.State[] snapshots) : base(snapshots) {
            }
        }

        public class BEventAwaitingMove : BEvent {
            public readonly Entity.State AwaitingPlayer;

            public BEventAwaitingMove(Entity.State[] snapshots, Entity player) : base(snapshots) {
                AwaitingPlayer = player.EntityState;
            }
        }

        public class BEventBattleEnded : BEvent {
            public readonly Entity.State Winner;
            public readonly Entity.State Loser;

            public BEventBattleEnded(Entity.State[] snapshots, Entity winner, Entity loser) : base(snapshots) {
                Winner = winner.EntityState;
                Loser = loser.EntityState;
            }
        }

        public class BEEventHealMoveOccurred : BEvent {
            public readonly Entity.State HealingPlayer;
            public readonly Entity.State HealedPlayer;
            public readonly Move HealingMove;
            public readonly int DamageDealt;

            public BEEventHealMoveOccurred(Entity.State[] snapshots, Entity healingPlayer, Entity healedPlayer, Move healingMove, int healAmt) : base(snapshots) {
                HealingPlayer = healingPlayer.EntityState;
                HealedPlayer = healedPlayer.EntityState;
                HealingMove = healingMove;
                DamageDealt = healAmt;
            }
        }

        public class BEventStackModified : BEvent {
            public readonly Entity.State Player;
            public readonly Move Move;
            public readonly Move.Attribute Attribute;
            public readonly int Count;

            public BEventStackModified(Entity.State[] snapshots, Entity player, Move move, int count) : base(snapshots) {
                Player = player.EntityState;
                Move = move;
                Attribute = move.Attr.Attribute;
                Count = count;
            }
        }

        public class BEventBonusDamageOccurred : BEvent {
            public readonly Entity.State Player;
            public readonly int DamageDealt;

            public BEventBonusDamageOccurred(Entity.State[] snapshots, Entity player, int damageDealt) : base(snapshots) {
                Player = player.EntityState;
                DamageDealt = damageDealt;
            }
        }

        public class BEventMoveOccurred : BEvent {
            public readonly Entity.State AttackingPlayer;
            public readonly Entity.State DefendingPlayer;
            public readonly Move AttackingMove;
            public readonly Move DefendingMove;
            public readonly int DamageDealt;

            public BEventMoveOccurred(Entity.State[] snapshots, Entity attackingPlayer, Entity defendingPlayer, Move attackingMove, Move defendingMove, int damageDealt) : base(snapshots) {
                AttackingPlayer = attackingPlayer.EntityState;
                DefendingPlayer = defendingPlayer.EntityState;
                AttackingMove = attackingMove;
                DefendingMove = defendingMove;
                DamageDealt = damageDealt;
            }
        }

        public class BEventMoveChosen : BEvent {
            public readonly Entity.State Player;
            public readonly Move Move;

            public BEventMoveChosen(Entity.State[] snapshots, Entity player, Move move) : base(snapshots) {
                Player = player.EntityState;
                Move = move;
            }
        }

        public class BEventTicksPassed : BEvent {
            public readonly int Ticks;

            public BEventTicksPassed(Entity.State[] snapshots, int ticks) : base(snapshots) {
                Ticks = ticks;
            }
        }

        public class BEventMoveDelayChanged : BEvent {
            public readonly Entity.State Player;
            public readonly int NewCount;
            public readonly int Delta;

            public BEventMoveDelayChanged(Entity.State[] snapshots, Entity player, int newCount, int delta) : base(snapshots) {
                Player = player.EntityState;
                NewCount = newCount;
                Delta = delta;
            }
        }
    }
}