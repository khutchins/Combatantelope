namespace Combatantelope.WindUp {

    

    public class Move {
        public readonly string ID;
        public readonly string Name;

        /// <summary>
        /// If the move can be used in battle, the general statistics for it. If not set,
        /// the move cannot be used in battle.
        /// </summary>
        public readonly MoveBattleStats MoveBattleStats;
        public readonly Attr Attr;

        public Move(string iD, string name, MoveBattleStats moveBattleStats, Attr attr) {
            ID = iD;
            Name = name;
            MoveBattleStats = moveBattleStats;
            Attr = attr;
        }

        public enum Attribute {
            None,
            // Move pierces defense.
            Piercing,
            // Move heals self.
            Heal,
            // If this move deals damage, the victim takes X damage over their next Y actions.
            Bleed,
            // If this move deals damage, the victim takes X damage every Y ticks for Z ticks.
            Poison,
            // Half of damage dealt is returned to the attacker.
            Vampiric,
            // All damage blocked by this is reflected to the attacker.
            Reflect,
            // Increases max HP.
            MaxHPBoost,
            // Delays opponent's move by X ticks.
            Stun
        }

        public override string ToString() {
            return $"{Name} - {VitalStats()}";
        }

        public bool HasAttrEffect {
            get => Attr is EffectAttr;
        }

        public bool IncreasesMaxHP {
            get => Attr.Attribute == Attribute.MaxHPBoost;
        }

        public bool AppliesStacks {
            get => Attr is StackAttr;
        }

        public bool AppliesOnTick {
            get => Attr is TickStackAttr;
        }

        private string EffectName() {
            return Attr.Attribute switch {
                Attribute.None => "",
                Attribute.Piercing => "Pi",
                Attribute.Heal => "He",
                Attribute.Bleed => "Bd",
                Attribute.Poison => "Po",
                Attribute.Vampiric => "Va",
                Attribute.Reflect => "Rf",
                Attribute.Stun => "St",
                _ => ""
            };
        }

        private string VitalStats() {
            if (Attr.Attribute == Attribute.MaxHPBoost && Attr is EffectAttr eat) {
                return $"+{eat.Effect} Max HP";
            }
            string attr = EffectName();
            attr = attr.Length == 0 ? "" : $"/{attr}";
            return $"{MoveBattleStats.Effect}/{MoveBattleStats.Defense}/{MoveBattleStats.Delay}{attr}";
        }

        public string MoveText(bool haveParens) {
            if (haveParens) return $"{Name} ({VitalStats()})";
            else return $"{Name} {VitalStats()}";
        }

        public class Builder {
            private string _id;
            private string _name;
            private MoveBattleStats _battleStats;
            private Attr _attr;

            public Builder(string id, string name) {
                _id = id;
                _name = name;
                _attr = new NoAttr();
            }

            public Builder SetBattleStats(int effect, int defense, int delay) {
                _battleStats = new MoveBattleStats(effect, defense, delay);
                return this;
            }

            public Builder SetAttr(Attr attr) {
                _attr = attr;
                return this;
            }

            public Move Build() {
                return new Move(this._id, _name, _battleStats, _attr);
            }
        }
    }

    public class MoveStacks {
        public readonly int StacksToApply;
        public readonly int DamageOnApply;
    }

    public class MoveBattleStats {
        public readonly int Effect;
        public readonly int Defense;
        public readonly int Delay;

        public MoveBattleStats(int effect, int defense, int delay) {
            Effect = effect;
            Defense = defense;
            Delay = delay;
        }
    }

    public abstract class Attr {
        public readonly Move.Attribute Attribute;

        protected Attr(Move.Attribute attribute) {
            Attribute = attribute;
        }
    }

    public class NoAttr : Attr {
        public NoAttr() : base(Move.Attribute.None) {}
    }

    public abstract class EffectAttr : Attr {
        public readonly int Effect;

        protected EffectAttr(Move.Attribute attr, int effect) : base(attr) {
            Effect = effect;
        }
    }

    public abstract class StackAttr : Attr {
        public readonly int StacksToApply;
        public readonly int DamageOnApply;

        protected StackAttr(Move.Attribute attr, int stacks, int damageOnApply) : base(attr) {
            StacksToApply = stacks;
            DamageOnApply = damageOnApply;
        }
    }

    public abstract class TickStackAttr : StackAttr {
        public readonly int TickFrequency;

        protected TickStackAttr(Move.Attribute attr, int stacks, int damageOnApply, int tickFrequency) : base(attr, stacks, damageOnApply) {
            TickFrequency = tickFrequency;
        }
    }

    public class PierceAttr : Attr {
        public PierceAttr() : base(Move.Attribute.Piercing) { }
    }

    public class HealAttr : Attr {
        public HealAttr() : base(Move.Attribute.Heal) { }
    }

    public class VampireAttr : Attr {
        public VampireAttr() : base(Move.Attribute.Vampiric) { }
    }

    public class ReflectAttr : Attr {
        public ReflectAttr() : base(Move.Attribute.Reflect) { }
    }

    public class StunAttr : EffectAttr { 
        public StunAttr(int effect) : base(Move.Attribute.Stun, effect) { }
    }

    public class MaxHPAttr : EffectAttr {
        public MaxHPAttr(int effect) : base(Move.Attribute.MaxHPBoost, effect) { }
    }

    public class PoisonAttr : TickStackAttr {
        public PoisonAttr(int stacks, int damageOnApply, int tickFrequency) : base(Move.Attribute.Poison, stacks, damageOnApply, tickFrequency) { }
    }

    public class BleedAttr : StackAttr {
        public BleedAttr(int stacks, int damageOnApply) : base(Move.Attribute.Bleed, stacks, damageOnApply) { }
    }
}