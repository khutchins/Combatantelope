using System.Linq;

namespace Combatantelope.WindUp {
    public class AIStrategy : IActionHandler {
        private int _lastMove = -1;
        private BattleStrategy _strategy;

        public AIStrategy(BattleStrategy battleStrategy) {
            _strategy = battleStrategy;
        }

        public void Reset() {
            _lastMove = -1;
        }

        public Move ChooseMove(Battle battle, Battle.EventTurn tevent) {
            Entity.State npc = tevent.Snapshots.Where(x => x.Attributes.Id == tevent.AwaitingPlayer.Attributes.Id).FirstOrDefault();
            Entity.State other = tevent.Snapshots.Where(x => x.Attributes.Id != tevent.AwaitingPlayer.Attributes.Id).FirstOrDefault();

            Move[] moves = UsableMoves(npc.Attributes.Moves);
            Move move;
            switch (_strategy) {
                case BattleStrategy.UseFirst:
                default:
                    move = moves[0];
                    break;
                case BattleStrategy.Cycle:
                    move = moves[_lastMove % moves.Length];
                    _lastMove = (_lastMove + 1) % moves.Length;
                    break;
                case BattleStrategy.NaiveOptimization:
                    move = FindBest(moves, npc, other, NaiveHeuristic);
                    break;
                case BattleStrategy.LessNaiveOptimization:
                    move = FindBest(moves, npc, other, LessNaiveHeuristic);
                    break;
                case BattleStrategy.OptimizeAgainstCurrentMove:
                    move = FindBest(moves, npc, other, PreferGuaranteedHit);
                    break;
                case BattleStrategy.MaybeBetterAI:
                    move = FindBest(moves, npc, other, TimeAware);
                    break;
                case BattleStrategy.MaybeBetterAI2:
                    move = FindBest(moves, npc, other, TryForShield);
                    break;
            }
            return move;
        }

        private Move FindBest(Move[] moves, Entity.State attacker, Entity.State defender, System.Func<Move, Entity.State, Entity.State, float> heuristic) {
            Move best = moves[0];
            float bestScore = heuristic(moves[0], attacker, defender);
            for (int i = 1; i < moves.Length; i++) {
                float score = heuristic(moves[i], attacker, defender);
                if (score > bestScore) {
                    best = moves[i];
                    bestScore = score;
                }
            }
            return best;
        }

        /// <summary>
        /// Tries to optimize for simple damage dealt.
        /// </summary>
        private float NaiveHeuristic(Move move, Entity.State attacker, Entity.State defend) {
            return Battle.ComputeDamage(move, defend.ActiveMove);
        }

        private float EstimatedDamage(Move move, Entity.State attacker, Entity.State defend) {
            int baseDamage = Battle.ComputeDamage(move, defend.ActiveMove);
            int damage = baseDamage;
            if (baseDamage > 0) {
                switch (move.Attr.Attribute) {
                    case Move.Attribute.Bleed:
                        damage += 10;
                        break;
                    case Move.Attribute.Poison:
                        damage += 10;
                        break;
                }
            }
            return damage;
        }

        private float LessNaiveHeuristic(Move move, Entity.State attacker, Entity.State defend) {
            return EstimatedDamage(move, attacker, defend);
        }

        private float PreferGuaranteedHit(Move move, Entity.State attacker, Entity.State defend) {
            bool willHitThisMove = Battle.WillHitBeforeMoveChange(move, defend);
            float damage = EstimatedDamage(move, attacker, defend);

            if (willHitThisMove && damage > 0) {
                damage += 30;
            }
            return damage;
        }

        private float TimeAware(Move move, Entity.State attacker, Entity.State defend) {
            bool willHitThisMove = Battle.WillHitBeforeMoveChange(move, defend);
            float baseDamage = EstimatedDamage(move, attacker, defend);
            float score = baseDamage;
            score += move.MoveBattleStats.Defense / 2f;
            score -= move.MoveBattleStats.Delay;

            if (move.Attr.Attribute == Move.Attribute.Reflect) score += move.MoveBattleStats.Effect / 2f;
            if (move.Attr.Attribute == Move.Attribute.Vampiric) score += baseDamage / 2f;
            if (move.Attr.Attribute == Move.Attribute.Stun && move.Attr is EffectAttr eat) score += eat.Effect / 2f;

            if (willHitThisMove && baseDamage > 0) {
                score += 30;
            }
            score -= Battle.ComputeReflect(move, defend.ActiveMove);
            return score;
        }

        private float TryForShield(Move move, Entity.State attacker, Entity.State defend) {
            bool willHitThisMove = Battle.WillHitBeforeMoveChange(move, defend);
            float baseDamage = EstimatedDamage(move, attacker, defend);
            float score = baseDamage;
            float healAmt = move.Attr.Attribute == Move.Attribute.Heal
                ? System.Math.Min(attacker.Attributes.MaxHP - attacker.HP, move.MoveBattleStats.Effect)
                : 0;

            // Defense modifiers
            if (willHitThisMove) { } // This attack will hit before it can shield.

            else {
                // A guess for if we don't know the move
                float maybeDamage = move.MoveBattleStats.Defense / 2f;
                // will shield
                if (defend.ActiveMove != null) {
                    maybeDamage = System.Math.Min(defend.ActiveMove.MoveBattleStats.Effect, move.MoveBattleStats.Defense);
                }
                score += maybeDamage;
                if (move.Attr.Attribute == Move.Attribute.Reflect) score += maybeDamage / 2f;
            }
            score -= move.MoveBattleStats.Delay / 2f;
            if (move.Attr.Attribute == Move.Attribute.Stun && move.Attr is EffectAttr eat) score += eat.Effect / 2f;
            if (move.Attr.Attribute == Move.Attribute.Heal) score += healAmt;

            if (willHitThisMove && (baseDamage > 0 || healAmt > 0)) {
                score += 30;
            }
            score -= Battle.ComputeReflect(move, defend.ActiveMove);
            return score;
        }

        private Move[] UsableMoves(Move[] moves) {
            return moves.Where(x => x.MoveBattleStats != null).ToArray();
        }
    }
}