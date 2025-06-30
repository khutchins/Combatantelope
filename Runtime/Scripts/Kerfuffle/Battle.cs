using Pomerandomian;
using System.Collections.Generic;
using UnityEngine;

namespace Combatantelope.Kerfuffle {

    public enum Move {
        Attack,
        Defend,
        Charge,
    }

    public class Battle : BaseBattle<Battle.KEvent, Entity, Entity.State, Entity.State.Builder>  {
        private BattleQueue<Entity> _queue;
        private Entity _currentEntity;
        private readonly Entity _entity1;
        private readonly Entity _entity2;
        private State _battleState = State.WaitingForStart;
        private AttackSummary _attackToParry;

        public Battle(Entity entity1, Entity entity2, IRandom random = null) : base(new List<Entity> { entity1, entity2 }, random) {
            _entity1 = entity1;
            _entity2 = entity2;
            _queue = new BattleQueue<Entity>(_entities, QueuePriority.Low);
        }

        enum State {
            WaitingForStart,
            WaitingForMove,
            WaitingForParryChoice,
            BattleOver
        }

        public override void StartBattle() {
            // Determine first player.
            int bailCheck = 0;
            const int MAX_CHECKS = 50;
            while (bailCheck < MAX_CHECKS) {
                foreach (var x in _entities) {
                    x.SetInitiative(x.EntityState.Attributes.AgilityDice.Roll(_random));
                }
                SendEvent(new EventStartRoll(States()));
                if (_entity1.EntityState.Initiative != _entity2.EntityState.Initiative) break;
                bailCheck++;
            }
            if (bailCheck == MAX_CHECKS) {
                Debug.LogWarning("Took too many tries to roll initiative. Choosing randomly.");
                bool p1FirstEmergency = _random.NextBool();
                _entity1.SetInitiative(p1FirstEmergency ? 2 : 1);
                _entity2.SetInitiative(p1FirstEmergency ? 1 : 2);
                SendEvent(new EventStartRoll(States()));
            }
            bool p1First = _entity1.EntityState.Initiative > _entity2.EntityState.Initiative;
            _entity1.SetTurnDelay(p1First ? 0 : 1);
            _entity2.SetTurnDelay(p1First ? 1 : 0);

            GetNextPlayer();
        }

        void GetNextPlayer() {
            if (CheckBattleOver()) {
                return;
            }
            _currentEntity = _queue.NextEntity();

            var ticks = _currentEntity.TimeToNextTurn;
            foreach (Entity ent in _entities) {
                ent.TimePassed(ticks);
            }

            _battleState = State.WaitingForMove;
            if (_currentEntity.EntityState.NextMoveIsCharge) {
                HandleChargeFinish(_currentEntity);
            } else {
                SendEvent(new EventTurn(States(), _currentEntity));
            }
        }

        public void MakeMove(Entity.State state, Move move) {
            MakeMove(GetPlayer(state), move);
        }

        /// <summary>
        /// Handles attack and returns true if next player should be pulled, false if player action is already requested.
        /// </summary>
        /// <param name="attacker"></param>
        /// <param name="move"></param>
        /// <returns></returns>
        bool HandleAttack(Entity attacker, Move move) {
            Entity defender = OtherEntity(attacker);

            var agilityScore = attacker.EntityState.Attributes.AgilityDice.RollDetailed(_random);
            var atkRawDamage = attacker.EntityState.Attributes.AttackDice.RollDetailed(_random);
            DiceResult atkBonusDamage = null;
            if (move == Move.Charge) {
                atkBonusDamage = attacker.EntityState.Attributes.DefendDice.RollDetailed(_random);
            }
            AttackSummary summary = new AttackSummary(agilityScore, atkRawDamage, atkBonusDamage);

            SendEvent(new EventAttackRoll(States(), attacker, move, summary));
            if (summary.Miss) {
                SendEvent(new EventMiss(States(), attacker, defender, summary));
                return true;
            } else {
                if (summary.Crit || defender.EntityState.NextMoveIsCharge || move == Move.Charge) {
                    var reason = move == Move.Charge ? EventNoParryOpportunity.ParryReason.AttackWasCharge
                        : (defender.EntityState.NextMoveIsCharge ? EventNoParryOpportunity.ParryReason.DefenderIsCharging
                            : EventNoParryOpportunity.ParryReason.AttackWasCritical);
                    SendEvent(new EventNoParryOpportunity(States(), defender, reason));
                    defender.TakeDamage(summary);
                    SendEvent(new EventHit(States(), attacker, defender, summary));
                    return true;
                } else {
                    // Parryable attack.
                    bool canDefenderParry = defender.EntityState.Attributes.ParryDice.MaxRoll >= atkRawDamage.FinalResult;
                    _battleState = State.WaitingForParryChoice;
                    _currentEntity = defender;
                    _attackToParry = summary;
                    SendEvent(new EventParryOpportunity(States(), attacker, defender, atkRawDamage.FinalResult, canDefenderParry));
                    return false;
                }
            }
        }

        void HandleChargeFinish(Entity entity) {
            entity.DidCharge();
            if (HandleAttack(entity, Move.Charge)) {
                GetNextPlayer();
            }
        }

        private Entity OtherEntity(Entity entity) {
            return entity == _entity1 ? _entity2 : _entity1;
        }

        public void MakeMove(Entity entity, Move move) {
            if (BattleOver) {
                Debug.LogWarning("Trying to make move while battle is over.");
                return;
            }
            if (entity != _currentEntity) {
                Debug.LogWarning($"The entity who attempted to move ({entity.Name}) is not the active one ({_currentEntity.Name})");
                return;
            }
            if (!entity.EntityState.ValidMoves.Contains(move)) {
                Debug.LogWarning($"Invalid move {move}. Changing it to attack");
                move = Move.Attack;
            }

            entity.AddTurnDelay();

            switch (move) {
                case Move.Attack:
                    if (HandleAttack(entity, Move.Attack)) {
                        GetNextPlayer();
                    }
                    break;
                case Move.Defend:
                    Dice defendDice = entity.EntityState.Attributes.DefendDice;
                    var defendAmount = defendDice.RollDetailed(_random);
                    entity.SetDefense(defendAmount.FinalResult);
                    SendEvent(new EventRaiseDefense(States(), entity, defendAmount));
                    GetNextPlayer();
                    break;
                case Move.Charge:
                    int defenseLost = entity.EntityState.Defense;
                    entity.SetCharging();
                    SendEvent(new EventChargeBegin(States(), entity, defenseLost));
                    GetNextPlayer();
                    break;
                default:
                    Debug.LogWarning($"Unrecognized move: {move}. Moving to next player.");
                    GetNextPlayer();
                    break;
            }
        }

        public void MakeParryChoice(Entity.State state, bool parry) {
            MakeParryChoice(GetPlayer(state), parry);
        }

        public void MakeParryChoice(Entity defender, bool parry) {
            if (BattleOver) {
                Debug.LogWarning("Trying to make parry decision while battle is over.");
                return;
            }
            if (defender != _currentEntity) {
                Debug.LogWarning($"The entity who attempted to parry ({defender.Name}) is not the active one ({_currentEntity.Name})");
                return;
            }
            if (_attackToParry == null) {
                Debug.LogWarning("No move available to parry!");
                return;
            }

            if (!parry) {
                defender.TakeDamage(_attackToParry);
                SendEvent(new EventHit(States(), OtherEntity(defender), defender, _attackToParry));
            } else {
                DiceResult parryResult = defender.EntityState.Attributes.ParryDice.RollDetailed(_random);
                SendEvent(new EventParryResult(States(), defender, _attackToParry, parryResult));
                if (_attackToParry.IsParriedBy(parryResult)) {
                    // No damage taken, no turn delay.
                } else {
                    // Defender takes damage, turn is skipped.
                    defender.TakeDamage(_attackToParry);
                    defender.AddTurnDelay();
                    SendEvent(new EventHit(States(), OtherEntity(defender), defender, _attackToParry));
                }
            }
            _attackToParry = null;
            GetNextPlayer();
        }

        bool CheckBattleOver() {
            if (BattleOver) return true;
            if (_entity1.EntityState.HP <= 0 || _entity2.EntityState.HP <= 0) {
                var winner = _entity1.EntityState.HP > 0 ? _entity1 : _entity2;
                var loser = winner == _entity1 ? _entity2 : _entity1;
                SendEvent(new EventBattleOver(States(), winner, loser));
                _battleState = State.BattleOver;
            }
            return BattleOver;
        }

        public bool BattleOver {
            get => _battleState == State.BattleOver;
        }

        public class KEvent : BaseEvent {
            public KEvent(Entity.State[] snapshots) : base(snapshots) {}
        }

        public class EventBattleStart : KEvent {
            public EventBattleStart(Entity.State[] snapshots) : base(snapshots) {}
        }

        public class EventStartRoll : KEvent {
            public EventStartRoll(Entity.State[] snapshots) : base(snapshots) {}
        }

        public class EventTurn : KEvent {
            public readonly Entity.State ActivePlayer;

            public EventTurn(Entity.State[] snapshots, Entity activePlayer) : base(snapshots) {
                ActivePlayer = activePlayer.EntityState;
            }
        }

        public class EventNoParryOpportunity : KEvent {
            public readonly Entity.State PersonWhoCouldNotParry;
            public readonly ParryReason Reason;

            public enum ParryReason {
                AttackWasCharge,
                AttackWasCritical,
                DefenderIsCharging,
            }

            public EventNoParryOpportunity(Entity.State[] snapshots, Entity defender, ParryReason reason) : base(snapshots) {
                PersonWhoCouldNotParry = defender.EntityState;
                Reason = reason;
            }
        }

        public class EventParryOpportunity : KEvent {
            public readonly Entity.State Attacker;
            public readonly Entity.State Defender;
            public readonly bool CouldParry;
            public readonly int NumberToHit;

            public EventParryOpportunity(Entity.State[] snapshots, Entity attacker, Entity defender, int numberToHit, bool couldParry) : base(snapshots) {
                Attacker = attacker.EntityState;
                Defender = defender.EntityState;
                CouldParry = couldParry;
                NumberToHit = numberToHit;
            }
        }

        public class EventMoveUsed : KEvent {
            public readonly Entity.State Mover;
            public readonly Move Move;

            public EventMoveUsed(Entity.State[] snapshots, Entity mover, Move move) : base(snapshots) {
                Mover = mover.EntityState;
                Move = move;
            }
        }

        public class EventHit : KEvent {
            public readonly Entity.State Attacker;
            public readonly Entity.State Defender;
            public readonly AttackSummary AttackSummary;

            public EventHit(Entity.State[] snapshots, Entity attacker, Entity defender, AttackSummary summary) : base(snapshots) {
                Attacker = attacker.EntityState;
                Defender = defender.EntityState;
                AttackSummary = summary;
            }
        }

        public class EventMiss : KEvent {
            public readonly Entity.State Attacker;
            public readonly Entity.State Defender;
            public readonly AttackSummary AttackSummary;

            public EventMiss(Entity.State[] snapshots, Entity attacker, Entity defender, AttackSummary attackSummary) : base(snapshots) {
                Attacker = attacker.EntityState;
                Defender = defender.EntityState;
                AttackSummary = attackSummary;
            }
        }

        public class EventAttackRoll : KEvent {
            public readonly Entity.State Attacker;
            public readonly Move Move;
            public readonly AttackSummary AttackSummary;

            public EventAttackRoll(Entity.State[] snapshots, Entity attacker, Move move, AttackSummary summary) : base(snapshots) {
                Attacker = attacker.EntityState;
                Move = move;
                AttackSummary = summary;
            }
        }

        public class EventChargeBegin : KEvent {
            public readonly Entity.State Charger;
            public readonly int DefenseLost;

            public EventChargeBegin(Entity.State[] snapshots, Entity charger, int defenseLost) : base(snapshots) {
                Charger = charger.EntityState;
                DefenseLost = defenseLost;
            }
        }

        public class EventRaiseDefense : KEvent {
            public readonly Entity.State Defender;
            public DiceResult Amount;

            public EventRaiseDefense(Entity.State[] snapshots, Entity defender, DiceResult amount) : base(snapshots) {
                Defender = defender.EntityState;
                Amount = amount;
            }
        }

        public class EventParryResult : KEvent {
            public readonly Entity.State Defender;
            public AttackSummary AttackSummary;
            public DiceResult ParryResult;
            public bool Successful {  get => AttackSummary.IsParriedBy(ParryResult); }

            public EventParryResult(Entity.State[] snapshots, Entity defender, AttackSummary attackSummary, DiceResult parryResult) : base(snapshots) {
                Defender = defender.EntityState;
                AttackSummary = attackSummary;
                ParryResult = parryResult;
            }

        }

        public class EventLost : KEvent {
            public readonly Entity.State Entity;

            public EventLost(Entity.State[] snapshots, Entity entity) : base(snapshots) {
                Entity = entity.EntityState;
            }
        }

        public class EventBattleOver : KEvent {
            public readonly Entity.State Winner;
            public readonly Entity.State Loser;

            public EventBattleOver(Entity.State[] snapshots, Entity winner, Entity loser) : base(snapshots) {
                Winner = winner.EntityState;
                Loser = loser.EntityState;
            }
        }

        public class AttackSummary {
            public readonly Move Move;
            public readonly DiceResult Agility;
            public readonly DiceResult Attack;
            public readonly DiceResult ChargeBonus;

            public bool Miss {
                get => Agility.FinalResult == 1;
            }

            public bool Crit {
                get => Agility.FinalResult == Agility.Dice.MaxRoll;
            }

            public int TotalDamage {
                get => Attack.FinalResult + (ChargeBonus == null ? 0 : ChargeBonus.FinalResult);
            }

            public AttackSummary(DiceResult agility, DiceResult attack, DiceResult chargeBonus) {
                Agility = agility;
                Attack = attack;
                ChargeBonus = chargeBonus;
                Move = ChargeBonus != null ? Move.Charge : Move.Attack;
            }

            public bool IsParriedBy(DiceResult parryResult) {
                return Move == Move.Attack && parryResult.FinalResult >= TotalDamage;
            }

            public override string ToString() {
                if (ChargeBonus == null) {
                    return Attack.ToString();
                } else {
                    return $"{Attack} + {ChargeBonus}";
                }
            }
        }
    }
}