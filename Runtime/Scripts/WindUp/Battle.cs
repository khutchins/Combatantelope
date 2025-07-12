using Pomerandomian;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Combatantelope.WindUp {
    public class Battle : BaseBattle<Battle.BEvent, Entity, Entity.State, Entity.State.Builder> {

        public class BEvent : BaseEvent {

            public BEvent(Entity.State[] snapshots) : base(snapshots) {}
        }

        public class BEventStart : BEvent {
            public BEventStart(Entity.State[] snapshots) : base(snapshots) {
            }
        }

        public class BEventAwaitingMove : BEvent {
            public readonly Entity.State AwaitingPlayer;

            public BEventAwaitingMove(Entity.State[] snapshots, Entity.State player) : base(snapshots) {
                AwaitingPlayer = player;
            }
        }

        public class BEventBattleEnded : BEvent {
            public readonly Entity.State Winner;
            public readonly Entity.State Loser;

            public BEventBattleEnded(Entity.State[] snapshots, Entity.State winner, Entity.State loser) : base(snapshots) {
                Winner = winner;
                Loser = loser;
            }
        }

        public class BEEventHealMoveOccurred : BEvent {
            public readonly Entity.State HealingPlayer;
            public readonly Entity.State HealedPlayer;
            public readonly Move HealingMove;
            public readonly int DamageDealt;

            public BEEventHealMoveOccurred(Entity.State[] snapshots, Entity.State healingPlayer, Entity.State healedPlayer, Move healingMove, int damageDealt) : base(snapshots) {
                HealingPlayer = healingPlayer;
                HealedPlayer = healedPlayer;
                HealingMove = healingMove;
                DamageDealt = damageDealt;
            }
        }

        public class BEventStackModified : BEvent {
            public readonly Entity.State Player;
            public readonly Move Move;
            public readonly Move.Attribute Attribute;
            public readonly int Count;

            public BEventStackModified(Entity.State[] snapshots, Entity.State player, Move move, int count) : base(snapshots) {
                Player = player;
                Move = move;
                Attribute = move.Attr;
                Count = count;
            }
        }

        public class BEventBonusDamageOccurred : BEvent {
            public readonly Entity.State Player;
            public readonly int DamageDealt;

            public BEventBonusDamageOccurred(Entity.State[] snapshots, Entity.State player, int damageDealt) : base(snapshots) {
                Player = player;
                DamageDealt = damageDealt;
            }
        }

        public class BEventMoveOccurred : BEvent {
            public readonly Entity.State AttackingPlayer;
            public readonly Entity.State DefendingPlayer;
            public readonly Move AttackingMove;
            public readonly Move DefendingMove;
            public readonly int DamageDealt;

            public BEventMoveOccurred(Entity.State[] snapshots, Entity.State attackingPlayer, Entity.State defendingPlayer, Move attackingMove, Move defendingMove, int damageDealt) : base(snapshots) {
                AttackingPlayer = attackingPlayer;
                DefendingPlayer = defendingPlayer;
                AttackingMove = attackingMove;
                DefendingMove = defendingMove;
                DamageDealt = damageDealt;
            }
        }

        public class BEventMoveChosen : BEvent {
            public readonly Entity.State Player;
            public readonly Move Move;

            public BEventMoveChosen(Entity.State[] snapshots, Entity.State player, Move move) : base(snapshots) {
                Player = player;
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

            public BEventMoveDelayChanged(Entity.State[] snapshots, Entity.State player, int newCount, int delta) : base(snapshots) {
                Player = player;
                NewCount = newCount;
                Delta = delta;
            }
        }

        List<BEvent> _events = new List<BEvent>();

        public Battle(Entity player, Entity enemy, IRandom random) : base(new List<Entity>() { player, enemy }, random) {
        }

        public override void StartBattle() {
            SendEvent(new BEventStart(States()));
            DoNextTurn();
        }

        public void TriggerLoss(BattlePlayer player) {
            int dmg = 9999;
            player.TakeDamage(dmg);
            SendEvent(new BEventBonusDamageOccurred(States(), player.GetSnapshot(), dmg));
            SendBattleEnded(Other(player));
        }

        private BattlePlayer Other(BattlePlayer player) {
            if (_playerStates.Length < 2) return null;
            PlayerState other = _playerStates[0].player != player ? _playerStates[0] : _playerStates[1];
            return other.player;
        }

        private void SendBattleEnded(BattlePlayer winner) {
            SendEvent(new BEventBattleEnded(States(), winner.GetSnapshot(), Other(winner).GetSnapshot()));
        }

        public void ScheduleMove(BattlePlayer player, Move move) {
            if (player != _waitingPlayer.player) {
                Debug.LogWarning("Got new move from non-awaiting player. Ignoring.");
                return;
            }

            _waitingPlayer.activeMove = move;
            _waitingPlayer.delayRemaining = Mathf.Max(1, move.Delay);
            SendEvent(new BEventMoveChosen(States(), player.GetSnapshot(), move));

            Debug.Log($"{player} chose {move}.");

            DoNextTurn();
        }

        void PrintStates() {
            for (int i = 0; i < _playerStates.Length; i++) {
                Debug.Log($"{_playerStates[i]}");
            }
        }

        public static int ComputeDamage(Move attack, Move defense) {
            if (attack.Attr == Move.Attribute.Heal) return 0;
            if (defense == null) return attack.Attack;
            return attack.Attr == Move.Attribute.Piercing
                ? attack.Attack : Mathf.Max(0, attack.Attack - defense.Defense);
        }

        public static int ComputeReflect(Move attack, Move defense) {
            if (defense == null || defense.Attr != Move.Attribute.Reflect) return 0;
            if (attack.Attr == Move.Attribute.Piercing) return 0;
            return Mathf.Min(attack.Attack, defense.Defense);
        }

        public static bool WillHitBeforeMoveChange(Move attack, Entity.State player) {
            return (player.Player.FirstToAct && attack.Delay < player.DelayRemaining)
                || attack.Delay <= player.DelayRemaining;
        }

        void DoNextTurn() {
            _waitingPlayer = null;

            int nextDelay = _playerStates.Select(x => x.delayRemaining).Min();

            PlayerState movingPlayer = null;
            foreach (PlayerState state in _playerStates) {
                state.delayRemaining -= nextDelay;
                if (movingPlayer == null && state.delayRemaining == 0) {
                    movingPlayer = state;
                }
            }
            SendEvent(new BEventTicksPassed(States(), nextDelay));

            if (movingPlayer == null) {
                Debug.LogError("No player hit delay zero. This is going to be a bug.");
                return;
            }

            Debug.Log("Waiting for next turn.");

            PlayerState otherPlayer = _playerStates.Where(x => x != movingPlayer).FirstOrDefault();

            if (movingPlayer.activeMove != null) {

                Move move = movingPlayer.activeMove;

                if (move.Attr == Move.Attribute.Heal) {
                    Debug.Log($"{movingPlayer.player} used {move} and healed for {move.Attack}");
                    movingPlayer.player.Heal(move.Attack);
                    Entity.State playerSnap = movingPlayer.player.GetSnapshot();
                    SendEvent(new BEEventHealMoveOccurred(States(), playerSnap, playerSnap, movingPlayer.activeMove, -move.Attack));
                } else {
                    int dmg = ComputeDamage(move, otherPlayer.activeMove);

                    if (dmg > 0 && move.AppliesStacks) {
                        Debug.Log($"{movingPlayer.player} used {move} and it applied {move.StacksToApply} stacks of {move.Attr}");
                        int count = otherPlayer.player.TopUpStacks(move);
                        SendEvent(new BEventStackModified(States(), otherPlayer.player.GetSnapshot(), move, count));
                    }

                    Debug.Log($"{movingPlayer.player} used {movingPlayer.activeMove} on {otherPlayer.player}, dealing {dmg} damage.");
                    otherPlayer.player.TakeDamage(dmg);
                    SendEvent(new BEventMoveOccurred(States(), movingPlayer.player.GetSnapshot(), otherPlayer.player.GetSnapshot(), movingPlayer.activeMove, otherPlayer.activeMove, dmg));

                    if (move.Attr == Move.Attribute.Stun) {
                        otherPlayer.delayRemaining += move.AttrEffect;
                        SendEvent(new BEventMoveDelayChanged(States(), otherPlayer.player.GetSnapshot(), otherPlayer.delayRemaining, move.AttrEffect));
                    }

                    if (otherPlayer.activeMove.Attr == Move.Attribute.Reflect) {
                        int deflected = move.Attack - dmg;
                        if (deflected > 0) {
                            int deflectDmg = deflected;
                            movingPlayer.player.TakeDamage(deflectDmg);
                            SendEvent(new BEventBonusDamageOccurred(States(), movingPlayer.player.GetSnapshot(), deflectDmg));
                        }
                    }

                    if (move.Attr == Move.Attribute.Vampiric && dmg > 0) {
                        int steal = dmg / 2;
                        Debug.Log($"{movingPlayer.player} stole {steal} health!");
                        movingPlayer.player.Heal(steal);
                        Entity.State playerSnap = movingPlayer.player.GetSnapshot();
                        SendEvent(new BEEventHealMoveOccurred(States(), playerSnap, playerSnap, movingPlayer.activeMove, -steal));
                    }
                }

                if (otherPlayer.player.Dead()) {
                    SendBattleEnded(movingPlayer.player);
                    return;
                }

                if (movingPlayer.player.TryGetStack(Move.Attribute.Bleed, out BattlePlayer.Stack stack)) {
                    int newCount = stack.Count - 1;
                    movingPlayer.player.RemoveStack(stack.AppliedMove);
                    SendEvent(new BEventStackModified(States(), movingPlayer.player.GetSnapshot(), stack.AppliedMove, newCount));
                    int dmg = stack.AppliedMove.DamageOnApply;
                    movingPlayer.player.TakeDamage(dmg);
                    SendEvent(new BEventBonusDamageOccurred(States(), movingPlayer.player.GetSnapshot(), dmg));
                }

                if (movingPlayer.player.Dead()) {
                    SendBattleEnded(otherPlayer.player);
                    return;
                }
            }
            PrintStates();

            _waitingPlayer = movingPlayer;
            SendEvent(new BEventAwaitingMove(States(), movingPlayer.player.GetSnapshot()));
        }
    }
}