namespace Combatantelope.WindUp {

    public class MoveStacks {
        public readonly int StacksToApply;
        public readonly int DamageOnApply;
    }

    public class MoveBattleStats {
        public readonly int Effect;
        public readonly int Defense;
        public readonly int Delay;
    }

    public class Move {
        public readonly string ID;
        public readonly string Name;

        /// <summary>
        /// If the move can be used in battle, the general statistics for it. If not set,
        /// the move cannot be used in battle.
        /// </summary>
        public readonly MoveBattleStats MoveBattleStats;
        public Attribute Attr;

        [Header("Attribute-Specific Attributes")]
        [ShowIf("AppliesStacks")]
        public int StacksToApply;
        [ShowIf("AppliesStacks")]
        public int DamageOnApply;
        [ShowIf("AppliesOnTick")]
        public int StackFrequency;
        [ShowIf("IncreasesMaxHP")]
        public int MaxHPBoostAmount;
        [ShowIf("HasAttrEffect")]
        public int AttrEffect;

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
            // While in the dodge period, no damage is taken. (Unimplemented)
            Dodge,
            // Makes the victim's next move miss. Their damage is permanently increase by 2x.
            Taunt,
            // Each stack signals a doubling of damage.
            Taunted,
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
            get => Attr == Attribute.Stun;
        }

        public bool IncreasesMaxHP {
            get => Attr == Attribute.MaxHPBoost;
        }

        public bool AppliesStacks {
            get => Attr == Attribute.Bleed || Attr == Attribute.Poison || Attr == Attribute.Taunt;
        }

        public bool AppliesOnTick {
            get => Attr == Attribute.Poison;
        }

        private string EffectName() {
            return Attr switch {
                Attribute.None => "",
                Attribute.Piercing => "Pi",
                Attribute.Heal => "He",
                Attribute.Bleed => "Bd",
                Attribute.Poison => "Po",
                Attribute.Dodge => "Do",
                Attribute.Vampiric => "Va",
                Attribute.Taunt => "Ta",
                Attribute.Reflect => "Rf",
                Attribute.Stun => "St",
                _ => ""
            };
        }

        private string VitalStats() {
            if (Attr == Attribute.MaxHPBoost) {
                return $"+{MaxHPBoostAmount} Max HP";
            }
            string attr = EffectName();
            attr = attr.Length == 0 ? "" : $"/{attr}";
            return $"{Attack}/{Defense}/{Delay}{attr}";
        }

        public string MoveText(bool haveParens) {
            if (haveParens) return $"{Name} ({VitalStats()})";
            else return $"{Name} {VitalStats()}";
        }
    }
}