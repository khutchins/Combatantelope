using Pomerandomian;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Combatantelope.WindUp {
    public enum BattleStrategy {
        UseFirst,
        Cycle,
        NaiveOptimization,
        LessNaiveOptimization,
        OptimizeAgainstCurrentMove,
        MaybeBetterAI,
        MaybeBetterAI2,
    }

    public class EntityAttributes {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly int MaxHP;
        public readonly BattleStrategy Strategy;
        public readonly Move[] Moves;

        public EntityAttributes(string id, string name, string description, int maxHP, BattleStrategy strategy, Move[] moves) {
            Id = id;
            Name = name;
            Description = description;
            MaxHP = maxHP;
            Strategy = strategy;
            Moves = moves;
        }
    }
}