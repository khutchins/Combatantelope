using Pomerandomian;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combatantelope.Kerfuffle {
    public class EntityAttributes {
        public readonly string Id;
        public readonly string Name;
        public readonly int MaxHP;
        public readonly Dice AttackDice;
        public readonly Dice DefendDice;
        public readonly Dice AgilityDice;
        public readonly Dice ParryDice;

        public EntityAttributes(string id, string name, int maxHP, Dice attack, Dice defense, Dice agility, Dice parry) {
            Id = id;
            Name = name;
            MaxHP = maxHP;
            AttackDice = attack;
            DefendDice = defense;
            AgilityDice = agility;
            ParryDice = parry;
        }
    }
}