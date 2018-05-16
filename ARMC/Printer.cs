/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Automata;

namespace ARMC
{
	public enum PrintFormat { DOT, TIMBUK, FSA, FSM }

    /// <summary>
    /// Automaton/transducer printer methods.
    /// </summary>
	public static class Printer<SYMBOL>
	{
        /// <summary>
        /// Prints automaton/transducer to standard output.
        /// </summary>
        /// <param name="m">Automaton/transducer.</param>
        /// <param name="format">Format of output.</param>
        /// <param name="sort">Sort states, moves, etc.?</param>
        public static void PrintAutomaton(ISSAutomaton<SYMBOL> m, PrintFormat format, bool sort = true)
		{
            StreamWriter file = new StreamWriter(Console.OpenStandardOutput());
            TextWriter stdout = Console.Out;
            Console.SetOut(file);

            try {
                PrintAutomaton(file, m, format, sort);
            } finally {
                file.Close();
                Console.SetOut(stdout);
            }

		}

        /// <summary>
        /// Prints automaton/transducer to file.
        /// </summary>
        /// <param name="fileName">Path to output file.</param>
        /// <param name="m">Automaton/transducer.</param>
        /// <param name="format">Format of output.</param>
        /// <param name="sort">Sort states, moves, etc.?</param>
        /// <param name="stateSymbolsFileName">Path to state symbols file (optional and only used with FSM format).</param>
        /// <param name="inputSymbolsFileName">Path to input arc symbols file (optional and only used with FSM format).</param>
        /// <param name="outputSymbolsFileName">Path to output arc symbols file (optional and only used with a transducer in FSM format).</param>
        public static void PrintAutomaton(
            string fileName, ISSAutomaton<SYMBOL> m, PrintFormat format, bool sort = true,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
		{
            using (var file = new StreamWriter(fileName)) {
                PrintAutomaton(file, m, format, sort);
            }
		}

        /// <summary>
        /// Prints automaton/transducer to open stream writer.
        /// </summary>
        /// <param name="file">Stream writer (caller must close).</param>
        /// <param name="m">Automaton/transducer.</param>
        /// <param name="format">Format of output.</param>
        /// <param name="sort">Sort states, moves, etc.?</param>
        /// <param name="stateSymbolsFileName">Path to state symbols file (optional and only used with FSM format).</param>
        /// <param name="inputSymbolsFileName">Path to input arc symbols file (optional and only used with FSM format).</param>
        /// <param name="outputSymbolsFileName">Path to output arc symbols file (optional and only used with a transducer in FSM format).</param>
		public static void PrintAutomaton(
            StreamWriter file, ISSAutomaton<SYMBOL> m, PrintFormat format, bool sort = true,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
		{
            // make list to enable ordering
            List<int> states = m.States.ToList();
            List<SYMBOL> alphabet = m.Alphabet.ToList();
            List<Move<ILabel<SYMBOL>>> moves = m.GenericMoves.Distinct().ToList();
			int initialState = m.InitialState;
            List<int> finalStates = m.FinalStates.ToList();

			if (sort) {
				alphabet.Sort();
				if (m.StateNames == null) {  // sort states by number
					states.Sort();
					finalStates.Sort();
				} else {  // sort states by name
					Comparer<int> comparer = Comparer<int>.Create((s1, s2) => string.Compare(m.StateNames[s1], m.StateNames[s2]));
					states.Sort(comparer);
					finalStates.Sort(comparer);
				}
			}

			Dictionary<int,string> stateNames;
			if (m.StateNames == null) {  // if no state names, provide defaults
				stateNames = new Dictionary<int,string>(states.Count);
				foreach (int state in states) {
					stateNames[state] = (format == PrintFormat.DOT) ? 
						string.Format("q_{0}{1}{2}", '{', state, '}') :  // DOT can display number as subscript
						string.Format("q{0}", state);
				}
			} else {
				stateNames = m.StateNames;
			}

            Action< StreamWriter, AutomatonType, string, Dictionary<int,string>,
				    PredicateAlgebra<SYMBOL>, List<int>, List<SYMBOL>,
                    List<Move<ILabel<SYMBOL>>>, int, List<int>,
                    string, string, string > print;

			switch (format) {
			case PrintFormat.DOT:
				print = PrintAutomatonDot;
				break;
			case PrintFormat.TIMBUK:
				print = PrintAutomatonTimbuk;
                break;
            case PrintFormat.FSA:
                print = PrintAutomatonFSA;
                break;
            case PrintFormat.FSM:
                print = PrintAutomatonFSM;
                break;
			default:
				throw new ArgumentOutOfRangeException("Invalid print format value");
			}

            // print in given format
            print(
                file, m.Type, m.Name, stateNames,
                new PredicateAlgebra<SYMBOL>(m.Alphabet), states, alphabet,
                moves, initialState, finalStates,
                stateSymbolsFileName, inputSymbolsFileName, outputSymbolsFileName
            );
		}

        /* format spec at http://www.graphviz.org/doc/info/lang.html */
        private static void PrintAutomatonDot(
            StreamWriter file, AutomatonType type, string name, Dictionary<int,string> stateNames,
            PredicateAlgebra<SYMBOL> algebra, List<int> states, List<SYMBOL> alphabet,
            List<Move<ILabel<SYMBOL>>> moves, int initialState, List<int> finalStates,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
		{
            // group labels for moves with same source and target state (reduce edges in graph)
            var transGroups = moves.GroupBy(
                move => new Tuple<int,int>(move.SourceState, move.TargetState),
                move => move.Label,
                (key, labels) => new Tuple<int,List<ILabel<SYMBOL>>,int>(key.Item1, labels.ToList(), key.Item2)
            );

            // create subscripts and superscripts based on LaTeX-like markup in state names
            // e.g. q_0 becomes <q<sub>0</sub>>, q_{42} becomes <q<sub>42</sub>>, q_M^2 becomes <q<sub>M</sub><sup>2</sup>>
			var regexSubSingle = new Regex(@"_([^{])");
			var regexSubGroup = new Regex(@"_{([^}]*)}");
			var regexSuperSingle = new Regex(@"\^([^{])");
			var regexSuperGroup = new Regex(@"\^{([^}]*)}");
            Func<int,string> stateToHTML = delegate(int state) {
                string stateName = stateNames[state];
				stateName = regexSubSingle.Replace(stateName, "<sub>$1</sub>");
				stateName = regexSubGroup.Replace(stateName, "<sub>$1</sub>");
				stateName = regexSuperSingle.Replace(stateName, "<sup>$1</sup>");
				stateName = regexSuperGroup.Replace(stateName, "<sup>$1</sup>");
				return stateName;
			};

            Func<SYMBOL, string> printSymbol = symbol => symbol.ToString()
                 .Replace("<", "&lt;").Replace(">", "&gt;")
                 .Replace("[", "&#91;").Replace("]", "&#93;");

            Func<ILabel<SYMBOL>,string> printPredicate = ilabel => {
                var predicate = (Predicate<SYMBOL>)ilabel;
                if (predicate == null)
                    return "&epsilon;";
                if (algebra.InclusiveSet(predicate).Count == 1)
                    return string.Format("<i>{0}</i>", printSymbol(algebra.InclusiveSet(predicate).First()));
                string typeSymbol = (predicate.Type == PredicateType.In) ? "&isin;" : "&notin;";  // unicode math set symbols
                // sort symbols and put in italics
                var symbols = new List<SYMBOL>(predicate.Set);
                symbols.Sort();
                var symbolsFormatted = new List<string>(symbols.Select(symbol => string.Format("<i>{0}</i>", symbol)));
                Func<int,int,int> ceilDiv = (x, y) => (x - 1) / y + 1;
                int groupSize = 5;
                if (symbols.Count > 1) {
                    while (ceilDiv(symbols.Count, groupSize - 1) == ceilDiv(symbols.Count, groupSize))
                        groupSize--;
                }
                int i = 0;
                string joined = string.Join(",<br/>", symbols
                    .Select(printSymbol)
                    .Select(symbol => string.Format("<i>{0}</i>", symbol))
                    .GroupBy(symbol => i++ / groupSize)
                    .Select(group => string.Join(", ", group.ToList()))
                );
                return typeSymbol + "{" + joined + "}";
            };
            Func<ILabel<SYMBOL>,string> printLabel = ilabel => {
                var label = (Label<SYMBOL>)ilabel;
                return label.IsIdentity ? 
                    string.Format("{0}<b>/</b>&#x1d704;", printPredicate(label.Input)) :
                    string.Format("{0}<b>/</b>{1}", printPredicate(label.Input), printPredicate(label.Output));
            };
            Func<ILabel<SYMBOL>,string> printILabel = (type == AutomatonType.SSA) ?
                printPredicate : printLabel;

			file.WriteLine("digraph {");
			file.WriteLine("    rankdir=LR;");  // left-to-right direction more readable for automata
            if (name != null) {  // print automaton name
                file.WriteLine("    label=<{0}:>;", name);
                file.WriteLine("    labelloc=top;");
                file.WriteLine("    labeljust=left;");
            }
            // final states have double circle
            file.Write("    node [shape=doublecircle];");
            foreach (int finalState in finalStates)
                file.Write(" {0};", finalState);
            file.WriteLine();
            file.WriteLine("    node [shape=circle];");
            // invisible zero-width dummy node as source of arrow to initial state
			file.WriteLine("    dummy_node [style=invis,width=0,fixedsize=true,label=\"\"];");
            file.WriteLine("    dummy_node -> {0} [len=0.2,penwidth=2.0];", initialState);
            foreach (Tuple<int,List<ILabel<SYMBOL>>,int> trans in transGroups) {
				int sourceState = trans.Item1;
                List<ILabel<SYMBOL>> labels = trans.Item2;
				int targetState = trans.Item3;
                file.WriteLine("    {0} -> {1} [label=<{2}>];",
                    sourceState, targetState, string.Join(",<br/>", labels.Select(printILabel))
				);
			}
			foreach (int state in states)
                file.WriteLine("    {0} [label=<{1}>];", state, stateToHTML(state));
			file.WriteLine("}");
			file.WriteLine();
		}

        /* file format compatible with https://github.com/Miskaaa/symboliclib */
        private static void PrintAutomatonTimbuk(
            StreamWriter file, AutomatonType type, string name, Dictionary<int,string> stateNames,
            PredicateAlgebra<SYMBOL> algebra, List<int> states, List<SYMBOL> alphabet,
            List<Move<ILabel<SYMBOL>>> moves, int initialState, List<int> finalStates,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
        {
            // all actual symbols have arity 1 ("start" symbol x has arity 0)
            var labelList = new List<string>(alphabet.Select(symbol => string.Format("{0}:1", symbol)));

            Func<ILabel<SYMBOL>,string> printPredicate = ilabel => {
                var predicate = (Predicate<SYMBOL>)ilabel;
                if (predicate == null)
                    return "";
                if (predicate.Type == PredicateType.In)
                    return string.Format("in{0}{1}{2}", '{', string.Join(",", predicate.Set), '}');
                else
                    return string.Format("not_in{0}{1}{2}", '{', string.Join(",", predicate.Set), '}');
            };
            Func<ILabel<SYMBOL>,string> printLabel = ilabel => {
                var label = (Label<SYMBOL>)ilabel;
                return label.IsIdentity ?
                    string.Format("@{0}/@{0}", printPredicate(label.Input)) :
                    string.Format("{0}/{1}", printPredicate(label.Input), printPredicate(label.Output));
            };
            Func<ILabel<SYMBOL>,string> printILabel = (type == AutomatonType.SSA) ?
                printPredicate : printLabel;

			file.WriteLine(string.Format("Ops x:0 {0}", string.Join(" ", labelList)));
			file.WriteLine();
            file.WriteLine(string.Format("Automaton {0} @{1}", name ?? "M", type == AutomatonType.SSA ? "INFA" : "INT"));
            file.WriteLine(string.Format("States {0}", string.Join(" ", states.Select(state => stateNames[state]))));
            file.WriteLine(string.Format("Final States {0}", string.Join(" ", finalStates.Select(state => stateNames[state]))));
			file.WriteLine("Transitions");
            file.WriteLine(string.Format("x() -> {0}", stateNames[initialState]));
            foreach (Move<ILabel<SYMBOL>> move in moves)
				file.WriteLine(string.Format("\"{0}\"({1}) -> {2}",
                    printILabel(move.Label), stateNames[move.SourceState], stateNames[move.TargetState]
				));
			file.WriteLine();
		}

        /* FSA format spec at http://www.let.rug.nl/~vannoord/Fsa/Manual/node5.html#anc1 */
        private static void PrintAutomatonFSA(
            StreamWriter file, AutomatonType type, string name, Dictionary<int,string> stateNames,
            PredicateAlgebra<SYMBOL> algebra, List<int> states, List<SYMBOL> alphabet,
            List<Move<ILabel<SYMBOL>>> moves, int initialState, List<int> finalStates,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
        {
            var transitions = new List<Move<ILabel<SYMBOL>>>();  // non-epsilon moves
            var jumps = new List<Tuple<int,int>>();  // epsilon moves
            foreach (Move<ILabel<SYMBOL>> move in moves) {
                if (move.IsEpsilon) {
                    jumps.Add(new Tuple<int,int>(move.SourceState, move.TargetState));
                } else {
                    transitions.Add(move);
                }
            }

            Func<ILabel<SYMBOL>,string> printPredicate = ilabel => {
                var predicate = (Predicate<SYMBOL>)ilabel;
                if (predicate == null)  // epsilon (only for transducers)
                    return "[]";
                // add quotes if necessary
                Func<SYMBOL,string> sanitizeSymbol = symbol => {
                    string s = symbol.ToString();
                    if (char.IsUpper(s[0]) || s.Any(ch => char.IsWhiteSpace(ch)) || (s[0] == '0' && s.Length > 1))
                        return "'" + s + "'";
                    return s;
                };
                // `in([a])` may just be written as `a`
                if (algebra.InclusiveSet(predicate).Count == 1)
                    return sanitizeSymbol(algebra.InclusiveSet(predicate).First());
                string typeName = (predicate.Type == PredicateType.In) ? "in" : "not_in";
                List<string> symbols = predicate.Set.Select(sanitizeSymbol).ToList();
                symbols.Sort();
                return string.Format("{0}([{1}])", typeName, string.Join(",", symbols));
            };
            Func<ILabel<SYMBOL>,string> printLabel = ilabel => {
                var label = (Label<SYMBOL>)ilabel;
                return label.IsIdentity ? 
                    string.Format("$@({0})/$@({0})", printPredicate(label.Input)) :
                    string.Format("{0}/{1}", printPredicate(label.Input), printPredicate(label.Output));
            };
            Func<ILabel<SYMBOL>,string> printILabel = (type == AutomatonType.SSA) ?
                printPredicate : printLabel;

            file.WriteLine("%% {0} {1}", (type == AutomatonType.SSA) ? "Recognizer" : "Transducer", name ?? "");
            file.WriteLine("%% Automatically generated by ARMC.");
            file.WriteLine("%% For more info, cf. http://www.let.rug.nl/~vannoord/Fsa/");
            file.WriteLine();
            file.WriteLine("fa(");
            if (type == AutomatonType.SSA)
                file.WriteLine("    r(fsa_preds),");
            else
                file.WriteLine("    t(fsa_preds,fsa_preds),");
            file.WriteLine("    % number of states");
            file.WriteLine("    {0},", states.Count);
            file.WriteLine("    % start states");
            file.WriteLine("    [ {0} ],", initialState);
            file.WriteLine("    % final states");
            // put up to 10 final states on one line
            int i = 0;
            FSAWriteList(
                file, 
                finalStates.GroupBy(state => i++ / 10).Select(group => group.ToList()),
                stateList => string.Join(",", stateList)
            );
            file.WriteLine("    ],");
            file.WriteLine("    % moves");
            FSAWriteList(file, transitions, (move => string.Format("trans({0},{1},{2})", move.SourceState, printILabel(move.Label), move.TargetState)));
            file.WriteLine("    % jumps");
            FSAWriteList(file, jumps, (jump => string.Format("jump({0},{1})", jump.Item1, jump.Item2)), false);
            file.WriteLine(").");
            file.WriteLine();
        }

        /* write out prolog list line by line (making sure all but the last line are followed by a comma) */
        private static void FSAWriteList<T>(StreamWriter file, IEnumerable<T> items, Func<T,string> stringify, bool addComma = true)
        {
            if (items.Count() == 0) {
                file.WriteLine("    []{0}", addComma ? "," : "");
            } else {
                file.WriteLine("    [");
                foreach (T item in items.Take(items.Count() - 1))
                    file.WriteLine("        {0},", stringify(item));
                file.WriteLine("        {0}", stringify(items.Last()));
                file.WriteLine("    ]{0}", addComma ? "," : "");
            }
        }

        /* format spec at http://web.eecs.umich.edu/~radev/NLP-fall2015/resources/fsm_archive/fsm.5.html */
        private static void PrintAutomatonFSM(
            StreamWriter file, AutomatonType type, string name, Dictionary<int,string> stateNames,
            PredicateAlgebra<SYMBOL> algebra, List<int> states, List<SYMBOL> alphabet,
            List<Move<ILabel<SYMBOL>>> moves, int initialState, List<int> finalStates,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
        {
            // map symbols to numeric IDs
            var symbolDict = new Dictionary<SYMBOL,int>(alphabet.Count);
            int id = 0;
            foreach (SYMBOL symbol in alphabet)
                symbolDict[symbol] = id++;

            // print state/symbol as number if corresponding symbols file unspecified,
            // otherwise print as string and map it to number in symbols file

            Action<Move<ILabel<SYMBOL>>,bool> printPredicateMove = (move, symbolFile) => {
                int sourceState = move.SourceState;
                var predicate = (Predicate<SYMBOL>)move.Label;
                int targetState = move.TargetState;
                if (predicate == null) {
                    file.WriteLine("{0} {1} 0",
                        (stateSymbolsFileName == null) ? sourceState.ToString() : stateNames[sourceState],
                        (stateSymbolsFileName == null) ? targetState.ToString() : stateNames[targetState]);
                } else {
                    foreach (SYMBOL symbol in algebra.InclusiveSet(predicate))
                        file.WriteLine("{0} {1} {2}",
                            (stateSymbolsFileName == null) ? sourceState.ToString() : stateNames[sourceState],
                            (stateSymbolsFileName == null) ? targetState.ToString() : stateNames[targetState],
                            symbolFile ? symbol.ToString() : symbolDict[symbol].ToString());
                }
            };
            Action<Move<ILabel<SYMBOL>>> printInputPredicateMove = (move => printPredicateMove(move, (inputSymbolsFileName != null)));
            Action<Move<ILabel<SYMBOL>>> printLabelMove = move => {
                int sourceState = move.SourceState;
                var label = (Label<SYMBOL>)move.Label;
                int targetState = move.TargetState;
                if (label.IsIdentity) {
                    foreach (SYMBOL symbol in algebra.InclusiveSet(label.Input))
                        file.WriteLine("{0} {1} {2} {3}",
                            (stateSymbolsFileName == null) ? sourceState.ToString() : stateNames[sourceState],
                            (stateSymbolsFileName == null) ? targetState.ToString() : stateNames[targetState],
                            (inputSymbolsFileName == null) ? symbolDict[symbol].ToString() : symbol.ToString(),
                            (outputSymbolsFileName == null) ? symbolDict[symbol].ToString() : symbol.ToString());
                } else {
                    // FIXME: epsilon
                    foreach (SYMBOL input in algebra.InclusiveSet(label.Input))
                        foreach (SYMBOL output in algebra.InclusiveSet(label.Output))
                            file.WriteLine("{0} {1} {2} {3}",
                                (stateSymbolsFileName == null) ? sourceState.ToString() : stateNames[sourceState],
                                (stateSymbolsFileName == null) ? targetState.ToString() : stateNames[targetState],
                                (inputSymbolsFileName == null) ? symbolDict[input].ToString() : input.ToString(),
                                (outputSymbolsFileName == null) ? symbolDict[output].ToString() : output.ToString());
                }
            };
            Action<Move<ILabel<SYMBOL>>> printMove = (type == AutomatonType.SSA) ?
                printInputPredicateMove : printLabelMove;

            // print moves
            foreach (Move<ILabel<SYMBOL>> move in moves)
                printMove(move);
            // print final states
            foreach (int finalState in states)
                file.WriteLine((stateSymbolsFileName == null) ? finalState.ToString() : stateNames[finalState]);

            if (stateSymbolsFileName != null) {
                var stateFile = new StreamWriter(stateSymbolsFileName);
                try {
                    foreach (KeyValuePair<int,string> item in stateNames)
                        stateFile.WriteLine("{0} {1}", item.Value, item.Key);
                } finally {
                    stateFile.Close();
                }
            }

            string[] symbolsFileNames = (type == AutomatonType.SSA) ?
                new string[] { inputSymbolsFileName } :
                new string[] { inputSymbolsFileName, outputSymbolsFileName };
            foreach (string symbolsFileName in symbolsFileNames) {
                if (symbolsFileName != null) {
                    var symbolsFile = new StreamWriter(symbolsFileName);
                    try {
                        foreach (KeyValuePair<SYMBOL,int> item in symbolDict)
                            symbolsFile.WriteLine("{0} {1}", item.Key, item.Value);
                    } finally {
                        symbolsFile.Close();
                    }
                }
            }
        }
	}
}

