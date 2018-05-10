/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Microsoft.Automata;

namespace ARMC
{
    /// <summary>
    /// Abstraction based on predicate languages.
    /// </summary>
    public class PredicateAbstraction<SYMBOL> : Abstraction<SYMBOL>
    {
        private Set<SSA<SYMBOL>> predicateAutomata;
        private bool forward;
        private Config.PredHeuristic? heuristic;
        private Set<int> ignoredLabels;

        /// <summary>
        /// Gets the automata used to form predicates.
        /// </summary>
        public Set<SSA<SYMBOL>> PredicateAutomata { get { return new Set<SSA<SYMBOL>>(predicateAutomata); } }

        public PredicateAbstraction(Config config, SSA<SYMBOL> init = null, SSA<SYMBOL> bad = null, SST<SYMBOL>[] taus = null)
        {
            var initPreds = new List<SSA<SYMBOL>>();

            /* add Init? */
            if (config.InitialPredicate == Config.InitPred.Init || config.InitialPredicate == Config.InitPred.Both)
                initPreds.Add(init ?? SSA<SYMBOL>.Parse(config.InitFilePath));
            /* add Bad? */
            if (config.InitialPredicate == Config.InitPred.Bad || config.InitialPredicate == Config.InitPred.Both)
                initPreds.Add(bad ?? SSA<SYMBOL>.Parse(config.BadFilePath));

            /* add transducer domains and/or ranges? */
            SST<SYMBOL>[] ssts = taus ?? config.TauFilePaths.Select(path => SST<SYMBOL>.Parse(path)).ToArray();
            if (config.IncludeGuard) {
                foreach (SST<SYMBOL> sst in ssts)
                    initPreds.Add(sst.Domain());
            }
            if (config.IncludeAction) {
                foreach (SST<SYMBOL> sst in ssts)
                    initPreds.Add(sst.Range());
            }

            /* ensure that the automata contain no epsilon transitions and have disjunct sets of states */
            predicateAutomata = new Set<SSA<SYMBOL>>();
            int offset = 0;
            foreach (SSA<SYMBOL> pred in initPreds) {
                SSA<SYMBOL> normPred = pred.RemoveEpsilons().Normalize(offset);
                predicateAutomata.Add(normPred);
                offset += normPred.States.Count();
            }

            forward = (config.LanguageDirection == Config.Direction.Forward);
            heuristic = config.Heuristic;
            ignoredLabels = new Set<int>();  // remains empty if no heuristic used
        }

        /* Collapse method ignores this in favour of more efficient alternative */
        public override bool StatesAreEquivalent(SSA<SYMBOL> m, int q1, int q2)
        {
            Func<int,SSA<SYMBOL>> stateLang = forward ? (Func<int,SSA<SYMBOL>>)m.ForwardStateLanguage : m.BackwardStateLanguage;
            SSA<SYMBOL> m1 = stateLang(q1);
            SSA<SYMBOL> m2 = stateLang(q2);

            foreach (SSA<SYMBOL> pa in predicateAutomata) {
                stateLang = forward ? (Func<int,SSA<SYMBOL>>)pa.ForwardStateLanguage : pa.BackwardStateLanguage;
                foreach (int state in pa.States) {
                    SSA<SYMBOL> p = stateLang(state);
                    if (SSA<SYMBOL>.ProductIsEmpty(p, m1) != SSA<SYMBOL>.ProductIsEmpty(p, m2))
                        return false;
                }
            }

            return true;
        }

        public override void Refine(SSA<SYMBOL> m, SSA<SYMBOL> x)
        {
            int offset = predicateAutomata.Sum(pred => pred.States.Count());
            x = x.RemoveEpsilons().Normalize(offset);

            predicateAutomata.Add(x);

            if (heuristic.HasValue) {
                var xStates = new Set<int>(x.States);

                /* find important states (appear in labels) */
                Dictionary<int,Set<int>> labels = MakeLabels(m);
                var importantStates = new Set<int>();
                foreach (int mState in m.States) {
                    foreach (int xState in labels[mState]) {
                        if (xState < offset || xState >= xStates.Count + offset)
                            continue;
                        importantStates.Add(xState);
                    }
                }

                if (((Config.PredHeuristic)heuristic) == Config.PredHeuristic.KeyStates) {
                    /* try to find one key state among important states */
                    foreach (int state in importantStates) {
                        /* try ignoring all but one state */
                        ignoredLabels += xStates;
                        ignoredLabels.Remove(state);
                        /* check if the collapsed automaton still intersects */
                        if (SSA<SYMBOL>.ProductIsEmpty(Collapse(m), x))
                            return;
                        /* failed, restore temporarily ignored states */
                        ignoredLabels -= xStates;
                    }

                    /* couldn't find just one key state, try to find two */
                    foreach (int state1 in importantStates) {
                        foreach (int state2 in importantStates.Where(s => s != state1)) {
                            ignoredLabels += xStates;
                            ignoredLabels.Remove(state1);
                            ignoredLabels.Remove(state2);
                            if (SSA<SYMBOL>.ProductIsEmpty(Collapse(m), x))
                                return;
                            ignoredLabels -= xStates;
                        }
                    }

                    /* fall back on important states heuristic */
                }

                /* ignore all unimportant states */
                ignoredLabels += xStates - importantStates;
            }
        }

        public override SSA<SYMBOL> Collapse(SSA<SYMBOL> m)
        {
            m = m.RemoveEpsilons();

            Dictionary<int,Set<int>> labels = MakeLabels(m);

            if (heuristic.HasValue) {
                /* remove ignored states from labels */
                foreach (int state in new List<int>(labels.Keys))
                    labels[state] -= ignoredLabels;
            }

            return m.Collapse((_, q1, q2) => labels[q1] == labels[q2]);
        }

        /* label automaton states by predicate states whose forward/backward languages intersect */
        private Dictionary<int,Set<int>> MakeLabels(SSA<SYMBOL> m)
        {
            var labels = new Dictionary<int,Set<int>>(m.States.Count());
            foreach (int state in m.States)
                labels[state] = new Set<int>();
            var stack = new Stack<Tuple<int,int>>();

            foreach (SSA<SYMBOL> pred in predicateAutomata)
                if (m.Algebra.Alphabet != pred.Algebra.Alphabet)
                    throw SSAException.IncompatibleAlphabets();

            Func<int, IEnumerable<Move<Predicate<SYMBOL>>>> getMoves = forward ?
                (Func<int, IEnumerable<Move<Predicate<SYMBOL>>>>)m.GetMovesTo : m.GetMovesFrom;

            foreach (SSA<SYMBOL> pred in predicateAutomata) {
                if (forward) {
                    foreach (int mState in m.FinalStates)
                        foreach (int pState in pred.FinalStates)
                            stack.Push(new Tuple<int,int>(mState, pState));
                } else {
                    stack.Push(new Tuple<int,int>(m.InitialState, pred.InitialState));
                }

                while (stack.Count > 0) {
                    var pair = stack.Pop();
                    int mState = pair.Item1;
                    int pState = pair.Item2;
                    labels[mState].Add(pair.Item2);

                    foreach (var mMove in getMoves(mState)) {
                        int state = forward ? mMove.SourceState : mMove.TargetState;
                        foreach (var pMove in (forward ? pred.GetMovesTo(pState) : pred.GetMovesFrom(pState))) {
                            if (!m.Algebra.IsSatisfiable(mMove.Label & pMove.Label))
                                continue;
                            int stateLabel = forward ? pMove.SourceState : pMove.TargetState;
                            if (!labels[state].Contains(stateLabel))
                                stack.Push(new Tuple<int,int>(state, stateLabel));
                        }
                    }
                }
            }

            return labels;
        }

        internal override void Print(string dir, ARMC<SYMBOL> armc)
        {
            string predDir = Path.Combine(dir, "predicate-automata");
            Directory.CreateDirectory(predDir);
            int pi = 0;
            foreach (SSA<SYMBOL> p in predicateAutomata)
                armc.PrintAutomaton(p, predDir, "P" + (pi++).ToString(), false);
        }
    }
}

