using Pomerandomian;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Combatantelope.WindUp {
    public class TextBattler : MonoBehaviour, IBattleListener<Battle.BEvent> {

        private int _lastInput = -1;
        private readonly List<Battle.BEvent> _unprocessedEvents = new List<Battle.BEvent>();
        private readonly Dictionary<Entity, IActionHandler> _actionHandlers = new Dictionary<Entity, IActionHandler>();
        private ITextBattleHandler _handler = new TextBattleHandlerConsole();
        [SerializeField] BattleStrategy _aiStrategy = BattleStrategy.Cycle;

        private void Start() {
            var handlerOverride = GetComponentInChildren<ITextBattleHandler>();
            if (handlerOverride != null) {
                _handler = handlerOverride;
            }
            Move[] moves = new Move[] {
                new Move.Builder("mg_play_dagger", "Dagger").SetBattleStats(10, 2, 6).Build(),
                new Move.Builder("mg_play_fastshield", "Buckler").SetBattleStats(0, 16, 5).Build(),
                new Move.Builder("mg_play_swordbleed", "Sawtooth").SetBattleStats(15, 8, 12).SetAttr(new BleedAttr(3, 5)).Build(),
                new Move.Builder("mg_play_fireball", "Fireball").SetBattleStats(33, 0, 20).SetAttr(new PierceAttr()).Build()
            };
            Entity e1 = new Entity(new EntityAttributes("mas", "Mas", "It's Mas!", 30, BattleStrategy.UseFirst, moves));
            Entity e2 = new Entity(new EntityAttributes("zel", "Zel", "It's Zel!", 30, _aiStrategy, moves));
            _actionHandlers[e2] = new AIStrategy(e2.EntityState.Attributes.Strategy);
            Battle battle = new Battle(e1, e2);
            battle.RegisterListener(this);
            StartCoroutine(DoBattle(battle));
        }

        IEnumerator DoBattle(Battle battle) {
            battle.StartBattle();

            while (true) {
                while (_unprocessedEvents.Count == 0) yield return null;

                var evnt = _unprocessedEvents[0];
                _unprocessedEvents.RemoveAt(0);

                string Name(Entity.State ent) {
                    return ent.Attributes.Name;
                }

                switch (evnt) {
                    case Battle.BEventStart bstart:
                        _handler.Log($"Starting battle between {Name(bstart.Snapshots[0])} and {Name(bstart.Snapshots[1])}. {Name(bstart.Snapshots[0])} acts first!");
                        break;
                    case Battle.EventTurn turnEvent:
                        var awaitingPlayer = turnEvent.AwaitingPlayer;
                        _handler.Log($"{Name(awaitingPlayer)}'s turn. (HP: {awaitingPlayer.HP}, Def: {awaitingPlayer.Defense})");
                        var otherPlayer = turnEvent.Snapshots.FirstOrDefault(x => x.ID != awaitingPlayer.ID);
                        if (otherPlayer.ActiveMove != null) {
                            _handler.Log($"{Name(otherPlayer)} has {otherPlayer.ActiveMove} readied. {otherPlayer.DelayRemaining} ticks before attack.");
                        } else {
                            _handler.Log($"{Name(otherPlayer)} has no move readied.");
                        }
                        yield return WaitForAction(battle, turnEvent);
                        break;
                    case Battle.BEventMoveChosen chosenEvent:
                        _handler.Log($"{Name(chosenEvent.Player)} prepares {chosenEvent.Move.MoveText(false)}.");
                        break;
                    case Battle.BEventTicksPassed ticksEvent:
                        _handler.Log($"{ticksEvent.Ticks} ticks pass.");
                        break;
                    case Battle.BEventMoveOccurred moveEvent:
                        string defenseText = moveEvent.DefendingMove != null ? $"against {moveEvent.DefendingMove.Name}" : "against an open target";

                        _handler.Log($"{Name(moveEvent.AttackingPlayer)} uses {moveEvent.AttackingMove.Name} {defenseText}, dealing {moveEvent.DamageDealt} damage. {Name(moveEvent.DefendingPlayer)} has {moveEvent.DefendingPlayer.HP}/{moveEvent.DefendingPlayer.Attributes.MaxHP} HP.");
                        break;
                    case Battle.BEEventHealMoveOccurred healEvent:
                        if (healEvent.DamageDealt < 0) {
                            _handler.Log($"{Name(healEvent.HealingPlayer)} uses {healEvent.HealingMove.Name}, healing for {healEvent.DamageDealt} HP. {Name(healEvent.HealedPlayer)} is now at {healEvent.HealedPlayer.HP}/{healEvent.HealedPlayer.Attributes.MaxHP} HP.");
                        }
                        break;
                    case Battle.BEventBonusDamageOccurred bonusDmgEvent:
                        _handler.Log($"{Name(bonusDmgEvent.Player)} takes {bonusDmgEvent.DamageDealt} bonus damage, leaving them with {bonusDmgEvent.Player.HP}/{bonusDmgEvent.Player.Attributes.MaxHP} HP.");
                        break;
                    case Battle.BEventStackModified stackEvent:
                        if (stackEvent.Count > 0) {
                            _handler.Log($"{Name(stackEvent.Player)} now has {stackEvent.Count} stacks of {stackEvent.Attribute}.");
                        } else {
                            _handler.Log($"{stackEvent.Attribute} wears off for {Name(stackEvent.Player)}.");
                        }
                        break;
                    case Battle.BEventMoveDelayChanged delayEvent:
                        _handler.Log($"{Name(delayEvent.Player)}'s next action is delayed by {delayEvent.Delta} ticks.");
                        break;
                    case Battle.BEventBattleEnded endEvent:
                        _handler.Log($"The battle is over! {Name(endEvent.Winner)} is victorious over {Name(endEvent.Loser)}.");
                        break;
                    default:
                        _handler.Log($"Unrecognized event {evnt.GetType()}");
                        break;
                }
            }
        }

        void PrintMoves(Entity.State entity, Move[] moves) {
            List<string> moveInput = new List<string>() {
                $"Choose move for {entity.Attributes.Name}:"
            };
            for (int i = 0; i < moves.Length; i++) {
                moveInput.Add($"{i + 1}. {moves[i]}");
            }
            _handler.InputRequest(moveInput.ToArray());
        }

        IEnumerator WaitForAction(Battle battle, Battle.EventTurn tevent) {
            Entity.State entity = tevent.AwaitingPlayer;
            if (_actionHandlers.TryGetValue(battle.GetPlayer(entity), out var handler)) {
                battle.ScheduleMove(entity, handler.ChooseMove(battle, tevent));
                yield break;
            }
            var moves = entity.Attributes.Moves;
            PrintMoves(entity, moves);
            while (true) {
                yield return WaitForSelection();

                if (_lastInput < moves.Length) {
                    _handler.ClearInputRequest();
                    break;
                }
            }

            Move move = moves[_lastInput];

            battle.ScheduleMove(entity, move);
        }

        IEnumerator WaitForSelection() {
            _lastInput = -1;
            while (_lastInput == -1) {
                yield return null;
                CheckAllInputs();
            }
            _lastInput -= 1;
        }

        private bool CheckAllInputs() {
            if (SetIfTrue(1, KeyCode.Alpha1, KeyCode.Keypad1)) return true;
            if (SetIfTrue(2, KeyCode.Alpha2, KeyCode.Keypad2)) return true;
            if (SetIfTrue(3, KeyCode.Alpha3, KeyCode.Keypad3)) return true;
            if (SetIfTrue(4, KeyCode.Alpha4, KeyCode.Keypad4)) return true;
            if (SetIfTrue(5, KeyCode.Alpha5, KeyCode.Keypad5)) return true;
            if (SetIfTrue(6, KeyCode.Alpha6, KeyCode.Keypad6)) return true;
            if (SetIfTrue(7, KeyCode.Alpha7, KeyCode.Keypad7)) return true;
            if (SetIfTrue(8, KeyCode.Alpha8, KeyCode.Keypad8)) return true;
            if (SetIfTrue(9, KeyCode.Alpha9, KeyCode.Keypad9)) return true;
            return false;
        }

        private bool SetIfTrue(int value, params KeyCode[] code) {
            foreach (KeyCode key in code) {
                if (UnityEngine.Input.GetKeyDown(key)) {
                    _lastInput = value;
                    return true;
                }
            }

            return false;
        }

        public void EventHappened(List<Battle.BEvent> allevents, Battle.BEvent thisEvent) {
            _unprocessedEvents.Add(thisEvent);
        }
    }

    public interface IActionHandler {
        public Move ChooseMove(Battle battle, Battle.EventTurn tevent);
    }
}