/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Automata;

namespace ARMC
{
    /// <summary>
    /// Simple Symbolic Automaton.
    /// </summary>
    /// <typeparam name="SYMBOL">Type of symbol in alphabet.</typeparam>
	public class SSA<SYMBOL> : ISSAutomaton<SYMBOL>
    {
        private Automaton<Predicate<SYMBOL>> automaton;
        public string Name { get; set; }

        public AutomatonType Type {
            get { return AutomatonType.SSA; }
        }

        private Dictionary<int, string> stateNames;
        /// <value>Dictionary storing names of states.</value>
		public Dictionary<int, string> StateNames {
            get { return stateNames; }
            set {
                if (value != null) {
                    var states1 = new Set<int>(States);
                    var states2 = new Set<int>(value.Keys);
                    var names = new Set<string>(value.Values);
                    if (states1 > states2 || states2.Count != names.Count)
                        throw AutomatonException.InvalidStateNames(Type);
                }
                stateNames = value;
            }
        }

        private static Dictionary<Set<SYMBOL>, PredicateAlgebra<SYMBOL>> algebras =
            new Dictionary<Set<SYMBOL>, PredicateAlgebra<SYMBOL>>(new SetEqualityComparer<SYMBOL>());

        public PredicateAlgebra<SYMBOL> Algebra
        {
            get { return (PredicateAlgebra<SYMBOL>)this.automaton.Algebra; }
        }

        public Set<SYMBOL> Alphabet
        {
            get { return this.Algebra.Alphabet; }
            internal set {
                PredicateAlgebra<SYMBOL> algebra;
                if (!algebras.TryGetValue(value, out algebra))
                    algebra = algebras[value] = new PredicateAlgebra<SYMBOL>(value);
                this.automaton = Automaton<Predicate<SYMBOL>>.Create(
                    algebra, InitialState, FinalStates, Moves
                );
            }
        }

        /// <summary>
        /// Determines if this automaton accepts no inputs.
        /// </summary>
        /// <value><c>true</c> if language of automaton is empty; otherwise, <c>false</c>.</value>
		public bool IsEmpty
        {
            get { return this.automaton.IsEmpty; }
        }

        /// <summary>
        /// Determines if this automaton is determinstic.
        /// </summary>
        /// <value><c>true</c> if automaton is deterministic; otherwise, <c>false</c>.</value>
        public bool IsDeterministic
        {
            get { return this.automaton.IsDeterministic; }
        }

        #region constructors

        /// <summary>
        /// Creates a new simple symbolic automaton.
        /// </summary>
        /// <param name="initialState">Initial state.</param>
        /// <param name="finalStates">Final states.</param>
        /// <param name="moves">Moves (aka transitions).</param>
        /// <param name="alphabet">Alphabet (derived from transition labels if <c>null</c>).</param>
        /// <param name="name">Name (optional).</param>
        /// <param name="stateNames">State names (optional).</param>
        public SSA(int initialState, IEnumerable<int> finalStates, IEnumerable<Move<Predicate<SYMBOL>>> moves,
            Set<SYMBOL> alphabet = null, string name = null, Dictionary<int, string> stateNames = null)
        {
            Set<SYMBOL> symbols = moves.Aggregate(
                new Set<SYMBOL>(),
                (syms, move) => move.IsEpsilon ? syms : (syms | move.Label.Symbols)
            );
            if (alphabet == null) {
                alphabet = symbols;
            } else if (symbols > alphabet) {
                throw AutomatonException.UnknownSymbolsInTransitions(Type);
            }

            PredicateAlgebra<SYMBOL> algebra;
            if (!algebras.TryGetValue(alphabet, out algebra))
                algebra = algebras[alphabet] = new PredicateAlgebra<SYMBOL>(alphabet);

            this.automaton = Automaton<Predicate<SYMBOL>>.Create(
                algebra, initialState, finalStates, moves,
                eliminateUnrreachableStates: true, eliminateDeadStates: true
            );
            this.Name = name;
            this.StateNames = stateNames;
        }

        private SSA(Automaton<Predicate<SYMBOL>> automaton)
        {
            this.automaton = automaton;
        }

        /// <summary>
        /// Creates an SSA by parsing text file in Timbuk, FSA, or FSM format.
        /// </summary>
        /// <remarks>
        /// Determines file format from file extension or file contents.
        /// </remarks>
        /// <param name="fileName">Path to automaton file.</param>
        /// <param name="stateSymbolsFileName">Path to state symbols file (optional and only used with FSM format).</param>
        /// <param name="inputSymbolsFileName">Path to input arc symbols file (optional and only used with FSM format).</param>
        public static SSA<SYMBOL> Parse(
            string fileName,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null)
        {
            int initialState;
            Set<int> finalStates;
            Set<Move<ILabel<SYMBOL>>> moves;
            string name;
            Set<SYMBOL> alphabet;
            Dictionary<int, string> stateNames;

            Parser<SYMBOL>.ParseAutomaton(fileName, AutomatonType.SSA,
                out initialState, out finalStates, out moves, out alphabet, out name, out stateNames,
                stateSymbolsFileName, inputSymbolsFileName
            );

            var moves1 = moves.Select(move => new Move<Predicate<SYMBOL>>(move.SourceState, move.TargetState, (Predicate<SYMBOL>)move.Label));

            return new SSA<SYMBOL>(initialState, finalStates, moves1, alphabet, name, stateNames);
        }

        #endregion

        /// <summary>
        /// Prints SSA in specified format to standard output.
        /// </summary>
        /// <param name="format">Text format of automaton.</param>
        /// <param name="sort">If set to <c>true</c>, will sort states, transitions, etc.</param>
        public void Print(PrintFormat format, bool sort = true)
        {
            Printer<SYMBOL>.PrintAutomaton(this, format, sort);
        }

        /// <summary>
        /// Prints SSA in specified format to file.
        /// </summary>
        /// <param name="fileName">Path to output file.</param>
        /// <param name="format">Text format of automaton.</param>
        /// <param name="sort">If set to <c>true</c>, will sort states, transitions, etc.</param>
        /// <param name="stateSymbolsFileName">Path to state symbols file (optional and only used with FSM format).</param>
        /// <param name="inputSymbolsFileName">Path to input arc symbols file (optional and only used with FSM format).</param>
        public void Print(
            string fileName, PrintFormat format, bool sort = true,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null)
        {
            Printer<SYMBOL>.PrintAutomaton(fileName, this, format, sort, stateSymbolsFileName, inputSymbolsFileName);
        }

        /// <summary>
        /// Prints SSA in specified format to open stream writer.
        /// </summary>
        /// <param name="file">Stream writer (caller must close).</param>
        /// <param name="format">Text format of automaton.</param>
        /// <param name="sort">If set to <c>true</c>, will sort states, transitions, etc.</param>
        /// <param name="stateSymbolsFileName">Path to state symbols file (optional and only used with FSM format).</param>
        /// <param name="inputSymbolsFileName">Path to input arc symbols file (optional and only used with FSM format).</param>
        public void Print(
            StreamWriter file, PrintFormat format, bool sort = true,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null)
        {
            Printer<SYMBOL>.PrintAutomaton(file, this, format, sort, stateSymbolsFileName, inputSymbolsFileName);
        }

        #region set-like methods

        /// <summary>
        /// Constructs SSA accepting intersection of L(M1) and L(M2).
        /// </summary>
        public static SSA<SYMBOL> Product(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return new SSA<SYMBOL>(Automaton<Predicate<SYMBOL>>.MkProduct(m1.automaton, m2.automaton));
        }

        /// <summary>
        /// Constructs SSA accepting union of L(M1) and L(M2).
        /// </summary>
        public static SSA<SYMBOL> Sum(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return new SSA<SYMBOL>(Automaton<Predicate<SYMBOL>>.MkSum(m1.automaton, m2.automaton));
        }

        /// <summary>
        /// Constructs SSA accepting L(this) minus L(M2).
        /// </summary>
		public SSA<SYMBOL> Minus(SSA<SYMBOL> m)
        {
            return new SSA<SYMBOL>(this.automaton.Minus(m.automaton));
        }

        /// <summary>
        /// Constructs SSA accepting complement of L(this).
        /// </summary>
        public SSA<SYMBOL> Complement()
        {
            return new SSA<SYMBOL>(this.automaton.Complement());
        }

        /// <summary>
        /// Checks if L(M1) equals L(M2).
        /// </summary>
        public static bool Equivalent(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return m1.automaton.IsEquivalentWith(m2.automaton);
        }

        /// <summary>
        /// Checks if L(M1) equals L(M2). If not, produces witness.
        /// </summary>
        public static bool Equivalent(SSA<SYMBOL> m1, SSA<SYMBOL> m2, out List<Predicate<SYMBOL>> witness)
        {
            return m1.automaton.IsEquivalentWith(m2.automaton, out witness);
        }

        /// <summary>
        /// Checks if L(this) is a subset of L(M).
        /// </summary>
		public bool IsSubsetOf(SSA<SYMBOL> m)
        {
            List<Predicate<SYMBOL>> witness;
            return IsSubsetOf(m, out witness);
        }

        /// <summary>
        /// Checks if L(this) is a subset of L(M). If not, produces witness.
        /// </summary>
        public bool IsSubsetOf(SSA<SYMBOL> m, out List<Predicate<SYMBOL>> witness)
        {
            return !(Automaton<Predicate<SYMBOL>>.CheckDifference(this.automaton, m.automaton, 0, out witness));
        }

        /// <summary>
        /// Checks if L(this) is a superset of L(M).
        /// </summary>
        public bool IsSupersetOf(SSA<SYMBOL> m)
        {
            return m.IsSubsetOf(this);
        }

        /// <summary>
        /// Checks if L(this) is a superset of L(M). If not, produces witness.
        /// </summary>
        public bool IsSupersetOf(SSA<SYMBOL> m, out List<Predicate<SYMBOL>> witness)
        {
            return m.IsSubsetOf(this, out witness);
        }

        /// <summary>
        /// Checks if L(this) is a proper subset of L(M).
        /// </summary>
        public bool IsProperSubsetOf(SSA<SYMBOL> m)
        {
            return this.IsSubsetOf(m) && !m.IsSubsetOf(this);
        }

        /// <summary>
        /// Checks if L(this) is a proper superset of L(M).
        /// </summary>
        public bool IsProperSupersetOf(SSA<SYMBOL> m)
        {
            return m.IsSubsetOf(this) && !this.IsSubsetOf(m);
        }

        #endregion

        #region operators

        /// <summary>
        /// Constructs SSA accepting intersection of L(M1) and L(M2).
        /// </summary>
        public static SSA<SYMBOL> operator *(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return Product(m1, m2);
        }

        /// <summary>
        /// Constructs SSA accepting union of L(M1) and L(M2).
        /// </summary>
        public static SSA<SYMBOL> operator +(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return Sum(m1, m2);
        }

        /// <summary>
        /// Constructs SSA accepting L(M1) minus L(M2).
        /// </summary>
        public static SSA<SYMBOL> operator -(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return m1.Minus(m2);
        }

        /// <summary>
        /// Constructs SSA accepting complement of L(M).
        /// </summary>
        public static SSA<SYMBOL> operator ~(SSA<SYMBOL> m)
        {
            return m.Complement();
        }

        /// <summary>
        /// Constructs SSA accepting intersection of L(M1) and L(M2).
        /// </summary>
        public static SSA<SYMBOL> operator &(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return Product(m1, m2);
        }

        /// <summary>
        /// Constructs SSA accepting union of L(M1) and L(M2).
        /// </summary>
        public static SSA<SYMBOL> operator |(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return Sum(m1, m2);
        }

        /// <summary>
        /// Checks if L(M1) equals L(M2).
        /// </summary>
        /// <remarks>
        /// Compares references if either SSA is null.
        /// </remarks>
		public static bool operator ==(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            if (object.ReferenceEquals(m1, null) || object.ReferenceEquals(m2, null))
                return object.ReferenceEquals(m1, m2);
            return SSA<SYMBOL>.Equivalent(m1, m2);
        }

        /// <summary>
        /// Checks if L(M1) does not equal L(M2).
        /// </summary>
        /// <remarks>
        /// Compares references if either SSA is null.
        /// </remarks>
        public static bool operator !=(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            if (object.ReferenceEquals(m1, null) || object.ReferenceEquals(m2, null))
                return !object.ReferenceEquals(m1, m2);
            return !SSA<SYMBOL>.Equivalent(m1, m2);
        }

        /// <summary>
        /// Checks if L(M1) is a subset of L(M2).
        /// </summary>
        public static bool operator <=(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return m1.IsSubsetOf(m2);
        }

        /// <summary>
        /// Checks if L(M1) is a superset of L(M2).
        /// </summary>
        public static bool operator >=(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return m1.IsSupersetOf(m2);
        }

        /// <summary>
        /// Checks if L(M1) is a proper subset of L(M2).
        /// </summary>
        public static bool operator <(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return m1.IsProperSubsetOf(m2);
        }

        /// <summary>
        /// Checks if L(M1) is a proper superset of L(M2).
        /// </summary>
        public static bool operator >(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            return m1.IsProperSupersetOf(m2);
        }

        #endregion

        #region automaton state access

        /// <value>Initial state.</value>
        public int InitialState
        {
            get { return automaton.InitialState; }
        }

        /// <value>All final states.</value>
        public IEnumerable<int> FinalStates
        {
            get { return automaton.GetFinalStates(); }
        }

        /// <value>All states.</value>
        public IEnumerable<int> States
        {
            get { return automaton.States; }
        }

        /// <summary>
        /// Check if the given state is final.
        /// </summary>
        /// <param name="state">State.</param>
		public bool IsFinalState(int state)
        {
            return automaton.IsFinalState(state);
        }

        /// <summary>
        /// All moves.
        /// </summary>
        public IEnumerable<Move<Predicate<SYMBOL>>> Moves
        {
            get { return automaton.GetMoves(); }
        }

        public IEnumerable<Move<ILabel<SYMBOL>>> GenericMoves
        {
            get {
                return Moves.Select(move => new Move<ILabel<SYMBOL>>(move.SourceState, move.TargetState, move.Label));
            }
        }

        /// <summary>
        /// Gets all moves from given state.
        /// </summary>
        /// <param name="sourceState">Source state.</param>
		public IEnumerable<Move<Predicate<SYMBOL>>> GetMovesFrom(int sourceState)
        {
            return automaton.GetMovesFrom(sourceState);
        }

        /// <summary>
        /// Gets all moves to given state.
        /// </summary>
        /// <param name="targetState">Target state.</param>
		public IEnumerable<Move<Predicate<SYMBOL>>> GetMovesTo(int targetState)
        {
            return automaton.GetMovesTo(targetState);
        }

        #endregion

        #region other automata methods

        /// <summary>
        /// Constructs an equivalent automaton that does not contain epsilon moves.
        /// </summary>
        public SSA<SYMBOL> RemoveEpsilons()
        {
            return new SSA<SYMBOL>(this.automaton.RemoveEpsilons());
        }

        /// <summary>
        /// Constructs an equivalent automaton that is deterministic.
        /// </summary>
        public SSA<SYMBOL> Determinize()
        {
            return new SSA<SYMBOL>(this.automaton.Determinize());
        }

        /// <summary>
        /// Constructs an equivalent automaton with labels matching every input in every state.
        /// </summary>
        public SSA<SYMBOL> MakeTotal()
        {
            return new SSA<SYMBOL>(this.automaton.MakeTotal());
        }

        /// <summary>
        /// Constructs an equivalent automaton where every pair of states is distinguishable.
        /// </summary>
        /// <remarks>
        /// Accepts non-deterministic automata too, does not perform determinization.
        /// </remarks>
        public SSA<SYMBOL> Minimize()
        {
            return new SSA<SYMBOL>(this.automaton.Minimize());
        }

        /// <summary>
        /// Constructs an automaton accepting the reverse language.
        /// </summary>
        public SSA<SYMBOL> Reverse()
        {
            return new SSA<SYMBOL>(this.automaton.Reverse());
        }

        /// <summary>
        /// Constructs an automaton accepting prefixes of all words in this language.
        /// </summary>
        public SSA<SYMBOL> PrefixLanguage()
        {
            return new SSA<SYMBOL>(this.automaton.PrefixLanguage());
        }

        /// <summary>
        /// Constructs an automaton accepting suffixes of all words in this language.
        /// </summary>
        public SSA<SYMBOL> SuffixLanguage()
        {
            return new SSA<SYMBOL>(this.automaton.SuffixLanguage());
        }

        /// <summary>
        /// Checks if the intersection of L(M1) and L(M2) is an empty language.
        /// </summary>
        public static bool ProductIsEmpty(SSA<SYMBOL> m1, SSA<SYMBOL> m2)
        {
            List<Predicate<SYMBOL>> witness;
            return SSA<SYMBOL>.ProductIsEmpty(m1, m2, out witness);
        }

        /// <summary>
        /// Checks if the intersection of L(M1) and L(M2) is an empty language. If not, produces witness.
        /// </summary>
        public static bool ProductIsEmpty(SSA<SYMBOL> m1, SSA<SYMBOL> m2, out List<Predicate<SYMBOL>> witness)
        {
            return !Automaton<Predicate<SYMBOL>>.CheckProduct(m1.automaton, m2.automaton, 0, out witness);
        }

        /// <summary>
        /// Normalizes state numbers to form unbroken sequence ascending from initial state to final states.
        /// </summary>
        /// <param name="initialState">The number for the initial state (sequence will ascend from here).</param>
        public SSA<SYMBOL> Normalize(int initialState = 0)
        {
            int id = initialState;
            Dictionary<int,int> stateDict = States
                .OrderBy(state => state == InitialState ? -1 : Convert.ToInt32(IsFinalState(state)))
                .ToDictionary(state => state, state => id++);

            return new SSA<SYMBOL>(
                stateDict[InitialState],
                FinalStates.Select(state => stateDict[state]),
                Moves.Select(move => new Move<Predicate<SYMBOL>>(
                    stateDict[move.SourceState], stateDict[move.TargetState], move.Label)),
                Alphabet,
                Name,
                StateNames == null ? null : StateNames.ToDictionary(item => stateDict[item.Key], item => item.Value)
            );
        }

        public ISSAutomaton<SYMBOL> GenericNormalize()
        {
            return (ISSAutomaton<SYMBOL>)Normalize();
        }

        public SSA<SYMBOL> Collapse(Func<SSA<SYMBOL>,int,int,bool> statesAreEquivalent)
        {
            /* map state to equivalency class representative */
            var stateMap = new Dictionary<int,int>();
            foreach (int state in States) {
                foreach (int repr in stateMap.Values.Distinct()) {  // search equivalency classes
                    if (statesAreEquivalent(this, state, repr)) {
                        stateMap[state] = repr;  // add to equivalency class
                        break;
                    }
                }
                if (!stateMap.ContainsKey(state))  // equivalency class not found
                    stateMap[state] = state;  // new equivalency class
            }

            int initialState = stateMap[InitialState];
            var finalStates = new Set<int>(FinalStates.Select(fs => stateMap[fs]));
            var moves = new Set<Move<Predicate<SYMBOL>>>(Moves.Select(move =>
                new Move<Predicate<SYMBOL>>(stateMap[move.SourceState], stateMap[move.TargetState], move.Label)
            ));
            var alphabet = new Set<SYMBOL>(Alphabet);

            return new SSA<SYMBOL>(initialState, finalStates, moves, alphabet);
        }

        public SSA<SYMBOL> ForwardStateLanguage(int state)
        {
            if (!States.Contains(state))
                throw SSAException.StateNotInStates();

            return new SSA<SYMBOL>(state, FinalStates, Moves, Alphabet, null, StateNames);
        }

        public SSA<SYMBOL> BackwardStateLanguage(int state)
        {
            if (!States.Contains(state))
                throw SSAException.StateNotInStates();

            return new SSA<SYMBOL>(InitialState, new int[] {state}, Moves, Alphabet, null, StateNames);
        }

        public SSA<SYMBOL> BoundedLanguage(int n)
        {
            var m = new SSA<SYMBOL>(
                0,
                Enumerable.Range(0, n + 1),
                Enumerable.Range(0, n).Select(s => new Move<Predicate<SYMBOL>>(s, s + 1, Predicate<SYMBOL>.True)),
                new Set<SYMBOL>(Alphabet)
            );

            return Product(this, m);
        }

        public SSA<SYMBOL> BoundedForwardStateLanguage(int state, int n)
        {
            return ForwardStateLanguage(state).BoundedLanguage(n);
        }

        public SSA<SYMBOL> BoundedBackwardStateLanguage(int state, int n)
        {
            return BackwardStateLanguage(state).BoundedLanguage(n);
        }

        public SSA<SYMBOL> ForwardTraceLanguage(int state)
        {
            return ForwardStateLanguage(state).PrefixLanguage();
        }

        public SSA<SYMBOL> BackwardTraceLanguage(int state)
        {
            return BackwardStateLanguage(state).PrefixLanguage();
        }

        public SSA<SYMBOL> BoundedForwardTraceLanguage(int state, int n)
        {
            return ForwardTraceLanguage(state).BoundedLanguage(n);
        }

        public SSA<SYMBOL> BoundedBackwardTraceLanguage(int state, int n)
        {
            return BackwardTraceLanguage(state).BoundedLanguage(n);
        }

		#endregion

		public override bool Equals(object obj)
		{
			SSA<SYMBOL> m = obj as SSA<SYMBOL>;
			if (m != null)
				return this.automaton.Equals(m.automaton);
			return this.automaton.Equals(obj);
		}

		public override int GetHashCode()
		{
			int hashCode = this.automaton.GetHashCode();
            if (Name != null)
                hashCode ^= Name.GetHashCode();
            if (StateNames != null)
                hashCode ^= StateNames.GetHashCode();
            return hashCode;
		}

		public override string ToString()
		{
            string repr = "SSA: q0=" + InitialState.ToString() + " F=" + new Set<int>(FinalStates).ToString() + " ";
            foreach (Move<Predicate<SYMBOL>> move in Moves)
                repr += "; " + move.SourceState + "(" + (move.IsEpsilon ? "" : move.Label.ToString()) +
                        ") -> " + move.TargetState;
            return repr;
		}
	}
}

