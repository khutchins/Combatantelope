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

        private void Start() {
            var handlerOverride = GetComponentInChildren<ITextBattleHandler>();
            if (handlerOverride != null) {
                _handler = handlerOverride;
            }
            Entity e1 = new Entity(new EntityAttributes("mas", "Mas", "It's Mas!", 20, BattleStrategy.UseFirst, new Move[0]));
            Entity e2 = new Entity(new EntityAttributes("zel", "Zel", "It's Zel!", 20, BattleStrategy.UseFirst, new Move[0]));
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
                    default:
                        Debug.LogWarning($"Unrecognized event {evnt.GetType()}");
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