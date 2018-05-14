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
    /// Simple Symbolic Transducer.
    /// </summary>
    /// <typeparam name="SYMBOL">Type of symbol in alphabet.</typeparam>
	public class SST<SYMBOL> : ISSAutomaton<SYMBOL>
	{
		private Automaton<Label<SYMBOL>> automaton;
		public string Name { get; set; }

		public AutomatonType Type
		{
			get { return AutomatonType.SST; }
		}

        private Dictionary<int,string> stateNames;
        /// <value>Dictionary storing names of states.</value>
		public Dictionary<int,string> StateNames
		{ 
			get { return stateNames; }
			set
            {
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

		public LabelAlgebra<SYMBOL> Algebra
		{
			get { return (LabelAlgebra<SYMBOL>)this.automaton.Algebra; }
		}

		public Set<SYMBOL> Alphabet
		{
			get { return this.Algebra.Alphabet; }
            internal set { this.Algebra.Alphabet = value; }
		}

        /// <summary>
        /// Creates a new simple symbolic transducer.
        /// </summary>
        /// <param name="initialState">Initial state.</param>
        /// <param name="finalStates">Final states.</param>
        /// <param name="moves">Moves (aka transitions).</param>
        /// <param name="alphabet">Alphabet (derived from transition labels if <c>null</c>).</param>
        /// <param name="name">Name (optional).</param>
        /// <param name="stateNames">State names (optional).</param>
		public SST(int initialState, IEnumerable<int> finalStates, IEnumerable<Move<Label<SYMBOL>>> moves,
			Set<SYMBOL> alphabet = null, string name = null, Dictionary<int,string> stateNames = null)
        {
            Set<SYMBOL> symbols = moves.Aggregate(
                new Set<SYMBOL>(),
                (syms, move) => syms | move.Label.Symbols
            );
            if (alphabet == null) {
                alphabet = symbols;
            } else if (symbols > alphabet) {
                throw AutomatonException.UnknownSymbolsInTransitions(Type);
            }
			this.automaton = Automaton<Label<SYMBOL>>.Create(
				new LabelAlgebra<SYMBOL>(alphabet), initialState, finalStates, moves,
				eliminateUnrreachableStates: true, eliminateDeadStates: true
            );
            this.Name = name;
            this.StateNames = stateNames;
		}

		private SST(Automaton<Label<SYMBOL>> automaton)
		{
			this.automaton = automaton;
		}

        /// <summary>
        /// Creates an SST by parsing text file in Timbuk, FSA, or FSM format.
        /// </summary>
        /// <remarks>
        /// Determines file format from file extension or file contents.
        /// </remarks>
        /// <param name="fileName">Path to automaton file.</param>
        /// <param name="stateSymbolsFileName">Path to state symbols file (optional and only used with FSM format).</param>
        /// <param name="inputSymbolsFileName">Path to input arc symbols file (optional and only used with FSM format).</param>
        /// <param name="outputSymbolsFileName">Path to output arc symbols file (optional and only used with FSM format).</param>
        public static SST<SYMBOL> Parse(
            string fileName,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
		{
			int initialState;
			Set<int> finalStates;
			Set<Move<ILabel<SYMBOL>>> moves;
			string name;
			Set<SYMBOL> alphabet;
			Dictionary<int,string> stateNames;

			Parser<SYMBOL>.ParseAutomaton(fileName, AutomatonType.SST,
				out initialState, out finalStates, out moves, out alphabet, out name, out stateNames,
                stateSymbolsFileName, inputSymbolsFileName, outputSymbolsFileName
            );

            var moves1 = moves.Select(move => new Move<Label<SYMBOL>>(move.SourceState, move.TargetState, (Label<SYMBOL>)move.Label));

			return new SST<SYMBOL>(initialState, finalStates, moves1, alphabet, name, stateNames);
        }

        /// <summary>
        /// Prints SST in specified format to standard output.
        /// </summary>
        /// <param name="format">Text format of automaton.</param>
        /// <param name="sort">If set to <c>true</c>, will sort states, transitions, etc.</param>
        public void Print(PrintFormat format, bool sort = true)
        {
            Printer<SYMBOL>.PrintAutomaton(this, format, sort);
        }

        /// <summary>
        /// Prints SST in specified format to file.
        /// </summary>
        /// <param name="fileName">Path to output file.</param>
        /// <param name="format">Text format of automaton.</param>
        /// <param name="sort">If set to <c>true</c>, will sort states, transitions, etc.</param>
        /// <param name="stateSymbolsFileName">Path to state symbols file (optional and only used with FSM format).</param>
        /// <param name="inputSymbolsFileName">Path to input arc symbols file (optional and only used with FSM format).</param>
        /// <param name="outputSymbolsFileName">Path to output arc symbols file (optional and only used with FSM format).</param>
        public void Print(
            string fileName, PrintFormat format, bool sort = true,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
		{
            Printer<SYMBOL>.PrintAutomaton(
                fileName, this, format, sort,
                stateSymbolsFileName, inputSymbolsFileName, outputSymbolsFileName
            );
        }

        /// <summary>
        /// Prints SST in specified format to open stream writer.
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

        /// <summary>
        /// Constructs transducer that models the inverse relation.
        /// </summary>
		public SST<SYMBOL> Invert()
		{
			var result = new SST<SYMBOL>(automaton.RelpaceAllGuards(label => label.Invert()));
            if (Name != null) {
                string marker = "<sup>-1</sup>";
                if (Name.EndsWith(marker))
                    result.Name = Name.Substring(0, Name.Length - marker.Length);
                else
                    result.Name = Name + marker;
            }
            if (StateNames != null)
                result.StateNames = new Dictionary<int,string>(StateNames);
            return result;
		}

        /// <summary>
        /// Applies this transducer to automaton M in order to create a new automaton
        /// that accepts the translations of all words in L(M).
        /// </summary>
		public SSA<SYMBOL> Apply(SSA<SYMBOL> m)
		{
            if (this.Algebra.Alphabet != m.Algebra.Alphabet)
                throw SSTException.IncompatibleAlphabets();

            m = m.RemoveEpsilons();
				
			Automaton<Label<SYMBOL>> tau = this.automaton;
			var stack = new Stack<Tuple<int,int>>();
			var finalStates = new List<int>();
			var moves = new List<Move<Predicate<SYMBOL>>>();
			var stateDict = new Dictionary<Tuple<int,int>,int>();
			int id = 0;

			var init = new Tuple<int,int>(tau.InitialState, m.InitialState);
			stateDict[init] = id++;  // initial state will be 0
			stack.Push(init);

            Action<Tuple<int,int>, Tuple<int,int>, Predicate<SYMBOL>> addMove = (sourcePair, targetPair, label) => {
                int targetState;
                if (!stateDict.TryGetValue(targetPair, out targetState)) {
                    stateDict[targetPair] = targetState = id++;
                    stack.Push(targetPair);
                }
                moves.Add(new Move<Predicate<SYMBOL>>(stateDict[sourcePair], targetState, label));
            };

			while (stack.Count > 0) {
				Tuple<int,int> sourcePair = stack.Pop();
				int tauState = sourcePair.Item1;
				int mState = sourcePair.Item2;

				foreach (Move<Label<SYMBOL>> tauMove in tau.GetMovesFrom(tauState)) {
                    if (tauMove.Label.Input == null) {
                        addMove(
                            sourcePair,
                            new Tuple<int,int>(tauMove.TargetState, mState),
                            tauMove.Label.IsIdentity ? null : tauMove.Label.Output
                        );
                        continue;
                    }

                    foreach (Move<Predicate<SYMBOL>> mMove in m.GetMovesFrom(mState)) {
                        if (!m.Algebra.IsSatisfiable(tauMove.Label.Input & mMove.Label))
                            continue;
                        Predicate<SYMBOL> newLabel;
                        if (tauMove.Label.IsIdentity) {
                            newLabel = tauMove.Label.Input & mMove.Label;
                        } else {
                            newLabel = tauMove.Label.Output;
                            if (newLabel != null && !m.Algebra.IsSatisfiable(newLabel))
                                continue;
                        }
                        addMove(
                            sourcePair,
                            new Tuple<int, int>(tauMove.TargetState, mMove.TargetState),
                            newLabel
                        );
                    }
				}
			}

            foreach (var pair in stateDict) {
                if (tau.IsFinalState(pair.Key.Item1) && m.IsFinalState(pair.Key.Item2))
                    finalStates.Add(pair.Value);
            }

            return new SSA<SYMBOL>(0, finalStates, moves, m.Algebra.Alphabet);
		}

        /// <summary>
        /// Compose transducers T1 and T2, resulting in transducer equivalent to applying T2 after T1.
        /// </summary>
		public static SST<SYMBOL> Compose(SST<SYMBOL> tau1, SST<SYMBOL> tau2)
		{
            if (tau1.Algebra != tau2.Algebra)
                throw SSTException.IncompatibleAlphabets();

			LabelAlgebra<SYMBOL> algebra = tau1.Algebra;
			var stack = new Stack<Tuple<int,int>>();
			var finalStates = new List<int>();
			var moves = new List<Move<Label<SYMBOL>>>();
			var stateDict = new Dictionary<Tuple<int,int>,int>();
			int id = 0;

			var init = new Tuple<int,int>(tau1.InitialState, tau2.InitialState);
			stateDict[init] = id++;
			stack.Push(init);

			while (stack.Count > 0) {
				Tuple<int,int> sourcePair = stack.Pop();
				int state1 = sourcePair.Item1;
				int state2 = sourcePair.Item2;

				foreach (Move<Label<SYMBOL>> move1 in tau1.GetMovesFrom(state1)) {
					foreach (Move<Label<SYMBOL>> move2 in tau2.GetMovesFrom(state2)) {
						Label<SYMBOL> newLabel = algebra.Combine(move1.Label, move2.Label);
						if (!algebra.IsSatisfiable(newLabel))
							continue;

						var targetPair = new Tuple<int,int>(move1.TargetState, move2.TargetState);
						int targetState;
						if (!stateDict.TryGetValue(targetPair, out targetState)) {
							stateDict[targetPair] = targetState = id++;
							stack.Push(targetPair);
							if (tau1.IsFinalState(move1.TargetState) && tau2.IsFinalState(move2.TargetState))
								finalStates.Add(targetState);
						}
						moves.Add(new Move<Label<SYMBOL>>(stateDict[sourcePair], targetState, newLabel));
					}
				}
			}

			return new SST<SYMBOL>(0, finalStates, moves);
		}

        public static SST<SYMBOL> Union(params SST<SYMBOL>[] taus)
        {
            if (taus.Length == 0)
                throw SSTException.NoSSTsInUnion();
            if (taus.Length == 1)
                return taus[0];

            var finalStates = new List<int>(taus.Sum(tau => tau.FinalStates.Count()));
            var moves = new List<Move<Label<SYMBOL>>>(taus.Length + taus.Sum(tau => tau.Moves.Count()));
            var alphabet = taus.Select(tau => tau.Alphabet).Aggregate(Set<SYMBOL>.Union);

            int offset = 0;
            Func<int,int> translate = state => offset + 1 + state;
            foreach (SST<SYMBOL> tau in taus.Select(tau => tau.Normalize())) {
                finalStates.AddRange(tau.FinalStates.Select(translate));
                moves.Add(new Move<Label<SYMBOL>>(0, translate(tau.InitialState), new Label<SYMBOL>(null, null)));
                moves.AddRange(tau.Moves.Select(move => new Move<Label<SYMBOL>>(
                    translate(move.SourceState), translate(move.TargetState), move.Label
                )));
                offset += tau.States.Count();
            }

            return new SST<SYMBOL>(0, finalStates, moves, alphabet);
        }

        /// <summary>
        /// Construct SSA accepting domain of this transducer.
        /// </summary>
        public SSA<SYMBOL> Domain()
        {
            var finalStates = new Set<int>(FinalStates);
            var moves = new Set<Move<Predicate<SYMBOL>>>(Moves.Select(move => 
                new Move<Predicate<SYMBOL>>(move.SourceState, move.TargetState, move.Label.Input)
            ));
            var alphabet = new Set<SYMBOL>(Alphabet);
            string name = Name == null ? null : string.Format("dom({0})", Name);
            Dictionary<int,string> stateNames = StateNames == null ? null : new Dictionary<int,string>(StateNames);

            return new SSA<SYMBOL>(InitialState, finalStates, moves, alphabet, name, stateNames);
        }

        /// <summary>
        /// Construct SSA accepting range of this transducer.
        /// </summary>
        public SSA<SYMBOL> Range()
        {
            var finalStates = new Set<int>(FinalStates);
            var moves = new Set<Move<Predicate<SYMBOL>>>(Moves.Select(move => 
                new Move<Predicate<SYMBOL>>(move.SourceState, move.TargetState, move.Label.IsIdentity ? move.Label.Input : move.Label.Output)
            ));
            var alphabet = new Set<SYMBOL>(Alphabet);
            string name = Name == null ? null : string.Format("ran({0})", Name);
            Dictionary<int,string> stateNames = StateNames == null ? null : new Dictionary<int,string>(StateNames);

            return new SSA<SYMBOL>(InitialState, finalStates, moves, alphabet, name, stateNames);
        }

        /// <summary>
        /// Normalizes state numbers to form sequence 0,1,2,... (initial state will be 0).
        /// </summary>
        public SST<SYMBOL> Normalize()
        {
            int id = 0;
            Dictionary<int, int> stateDict = States
                .OrderBy(state => state == InitialState ? -1 : Convert.ToInt32(IsFinalState(state)))
                .ToDictionary(state => state, state => id++);

            return new SST<SYMBOL>(
                stateDict[InitialState],
                FinalStates.Select(state => stateDict[state]),
                Moves.Select(move => new Move<Label<SYMBOL>>(
                    stateDict[move.SourceState], stateDict[move.TargetState], move.Label)),
                Alphabet,
                Name,
                StateNames == null ? null : StateNames.ToDictionary(item => stateDict[item.Key], item => item.Value)
            );
        }

        public ISSAutomaton<SYMBOL> GenericNormalize()
            => (ISSAutomaton<SYMBOL>)Normalize();

        /// <summary>
        /// Constructs transducer that models the inverse relation.
        /// </summary>
		public static SST<SYMBOL> operator-(SST<SYMBOL> tau)
        {
            return tau.Invert();
        }

        /// <summary>
        /// Compose transducers T1 and T2, resulting in transducer equivalent to applying T2 after T1.
        /// </summary>
		public static SST<SYMBOL> operator+(SST<SYMBOL> tau1, SST<SYMBOL> tau2)
        {
            return Compose(tau1, tau2);
        }

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
        /// Gets all moves.
        /// </summary>
        public IEnumerable<Move<Label<SYMBOL>>> Moves
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
		public IEnumerable<Move<Label<SYMBOL>>> GetMovesFrom(int sourceState)
		{
			return automaton.GetMovesFrom(sourceState);
		}

        /// <summary>
        /// Gets all moves to given state.
        /// </summary>
        /// <param name="targetState">Target state.</param>
		public IEnumerable<Move<Label<SYMBOL>>> GetMovesTo(int targetState)
		{
			return automaton.GetMovesTo(targetState);
        }

        public override string ToString()
        {
            string repr = "SST: q0=" + InitialState.ToString() + " F=" + new Set<int>(FinalStates).ToString() + " ";
            foreach (Move<Label<SYMBOL>> move in Moves)
                repr += "; " + move.SourceState + "(" + (move.IsEpsilon ? "" : move.Label.ToString()) +
                        ") -> " + move.TargetState;
            return repr;
        }
	}
}

