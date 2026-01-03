using Pomerandomian;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Combatantelope.Kerfuffle {
    public class TextBattler : MonoBehaviour, IBattleListener<Battle.KEvent> {

        private int _lastInput = -1;
        private readonly List<Battle.KEvent> _unprocessedEvents = new List<Battle.KEvent>();
        private readonly Dictionary<Entity, IActionHandler> _actionHandlers = new Dictionary<Entity, IActionHandler>();
        private ITextBattleHandler _handler = new TextBattleHandlerConsole();

        private void Start() {
            var handlerOverride = GetComponentInChildren<ITextBattleHandler>();
            if (handlerOverride != null) {
                _handler = handlerOverride;
            }
            Entity e1 = new Entity(new EntityAttributes("mas", "Mas", 20, Dice.FromString("1d6"), Dice.FromString("1d8"), Dice.FromString("1d10"), Dice.FromString("1d4")));
            Entity e2 = new Entity(new EntityAttributes("zel", "Zel", 20, Dice.FromString("1d6"), Dice.FromString("1d8"), Dice.FromString("1d10"), Dice.FromString("1d4")));
            _actionHandlers[e2] = new AIActionHandler();
            Battle battle = new Battle(e1, e2);
            battle.RegisterListener(this);
            StartCoroutine(DoBattle(battle));
        }

        IEnumerator DoBattle(Battle battle) {
            battle.StartBattle();

            while (true) {
                while (_unprocessedEvents.Count == 0) yield return null;

                Battle.KEvent evnt = _unprocessedEvents[0];
                _unprocessedEvents.RemoveAt(0);

                string Name(Entity.State ent) {
                    return ent.Attributes.Name;
                }

                switch (evnt) {
                    case Battle.EventStartRoll rollEvent:
                        _handler.Log($"Initiative: {string.Join(", ", rollEvent.Snapshots.Select(x => $"{Name(x)} - {x.Initiative}"))}");
                        if (rollEvent.Snapshots[0].Initiative == rollEvent.Snapshots[1].Initiative) {
                            _handler.Log("Rerolling to break tie.");
                        }
                        break;
                    case Battle.EventTurn turnEvent:
                        _handler.Log($"{turnEvent.ActivePlayer.Attributes.Name}'s turn!");
                        yield return WaitForAction(battle, turnEvent);
                        break;
                    case Battle.EventAttackRoll attackEvent:
                        if (attackEvent.Move == Move.Charge) {
                            _handler.Log($"{Name(attackEvent.Attacker)} charged for {attackEvent.AttackSummary} attempted damage. Agility roll was {attackEvent.AttackSummary.Agility}.");
                        } else {
                            _handler.Log($"{Name(attackEvent.Attacker)} attacked for {attackEvent.AttackSummary} attempted damage. Agility roll was {attackEvent.AttackSummary.Agility}.");
                        }
                        break;
                    case Battle.EventMiss missEvent:
                        _handler.Log($"{Name(missEvent.Attacker)} missed on an attack with {missEvent.AttackSummary.TotalDamage} damage");
                        break;
                    case Battle.EventParryOpportunity epo:
                        _handler.Log($"{Name(epo.Defender)} can attempt to parry. They need to roll a {epo.NumberToHit} or higher to parry.");
                        yield return WaitForParryChoice(battle, epo);
                        break;
                    case Battle.EventNoParryOpportunity enpo:
                        _handler.Log($"{Name(enpo.PersonWhoCouldNotParry)} cannot parry because {enpo.Reason}.");
                        break;
                    case Battle.EventRaiseDefense erd:
                        _handler.Log($"{Name(erd.Defender)} rolled {erd.Amount} for defense. Defense now {erd.Amount.FinalResult}.");
                        break;
                    case Battle.EventChargeBegin ecb:
                        _handler.Log($"{Name(ecb.Charger)} began charging attack. {ecb.DefenseLost} defense lost.");
                        break;
                    case Battle.EventHit eh:
                        _handler.Log($"{Name(eh.Attacker)} hit {Name(eh.Defender)} for {eh.AttackSummary.TotalDamage}. {eh.Defender.Defense}D / {eh.Defender.HP}♥ remaining.");
                        break;
                    case Battle.EventParryResult epr:
                        if (epr.Successful) {
                            _handler.Log($"{Name(epr.Defender)} succesfully parried the attack! ({epr.ParryResult} vs {epr.AttackSummary})");
                        } else {
                            _handler.Log($"{Name(epr.Defender)} failed to parry the attack. ({epr.ParryResult} vs {epr.AttackSummary}) Their next turn will be skipped.");
                        }
                        break;
                    case Battle.EventMoveUsed emu:
                        _handler.Log($"{Name(emu.Mover)} picked move {emu.Move}.");
                        break;
                    case Battle.EventBattleOver ebo:
                        _handler.Log($"{Name(ebo.Winner)} has won with {ebo.Winner.HP} HP and {ebo.Winner.Defense} defense remaining!");
                        break;
                    default:
                        Debug.LogWarning($"Unrecognized event {evnt.GetType()}");
                        break;
                }
            }
        }

        void PrintMoves(Entity.State entity, List<Move> moves) {
            List<string> moveInput = new List<string>() {
                $"Choose move for {entity.Attributes.Name}:"
            };
            for (int i = 0; i < moves.Count; i++) {
                moveInput.Add($"{i + 1}. {moves[i]}");
            }
            _handler.InputRequest(moveInput.ToArray());
        }

        IEnumerator WaitForAction(Battle battle, Battle.EventTurn tevent) {
            Entity.State entity = tevent.ActivePlayer;
            if (_actionHandlers.TryGetValue(battle.GetPlayer(entity), out var handler)) {
                battle.MakeMove(entity, handler.ChooseMove(battle, tevent));
                yield break;
            }
            var moves = entity.ValidMoves;
            PrintMoves(entity, moves);
            while (true) {
                yield return WaitForSelection();

                if (_lastInput < moves.Count) {
                    _handler.ClearInputRequest();
                    break;
                }
            }

            Move move = moves[_lastInput];

            battle.MakeMove(entity, move);
        }

        void PrintParryChoice(Entity entity) {
            _handler.InputRequest(
                $"{entity.EntityState.Attributes.Name}, Do you want to attempt to parry?",
                "1: Yes",
                "2: No"
            );
        }

        IEnumerator WaitForParryChoice(Battle battle, Battle.EventParryOpportunity pevent) {
            Entity defender = battle.GetPlayer(pevent.Defender);
            if (_actionHandlers.TryGetValue(defender, out var handler)) {
                battle.MakeParryChoice(defender, handler.ChooseParry(battle, pevent));
                yield break;
            }
            PrintParryChoice(defender);
            while (true) {
                yield return WaitForSelection();

                if (_lastInput < 2) {
                    _handler.ClearInputRequest();
                    break;
                }
            }

            battle.MakeParryChoice(defender, _lastInput == 0);
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

        public void EventHappened(List<Battle.KEvent> allevents, Battle.KEvent thisEvent) {
            _unprocessedEvents.Add(thisEvent);
        }
    }

    public interface IActionHandler {
        public Move ChooseMove(Battle battle, Battle.EventTurn tevent);
        public bool ChooseParry(Battle battle, Battle.EventParryOpportunity pevent);
    }

    public class AIActionHandler : IActionHandler {
        public Move ChooseMove(Battle battle, Battle.EventTurn tevent) {
            return new SystemRandom().From(tevent.ActivePlayer.ValidMoves);
        }

        public bool ChooseParry(Battle battle, Battle.EventParryOpportunity pevent) {
            var defender = pevent.Defender;
            var parry = defender.Attributes.ParryDice;
            // Free parry.
            if (pevent.NumberToHit <= parry.MinRoll) return true;
            // Impossible parry.
            if (!pevent.CouldParry) return false;
            // Will kill, must parry.
            if (pevent.NumberToHit >= defender.HP + defender.Defense) return true;

            // Parry if there's a moderately good chance we'll succeed. Very naive chance computation.
            int range = parry.MaxRoll - parry.MinRoll;
            float percent = (pevent.NumberToHit - parry.MinRoll) / range;
            return percent < 0.3;
        }
    }
}