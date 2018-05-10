/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Automata;

namespace ARMC
{
	public static class Parser<SYMBOL>
    {
		private delegate void ParseAction(string fileName, AutomatonType type,
			out int initialState, out Set<int> finalStates, out Set<Move<ILabel<SYMBOL>>> moves,
            out Set<SYMBOL> alphabet, out string name, out Dictionary<int,string> stateNames,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null);

        /* convert more readable regex patterns to valid regexes */
        internal static string _(string pattern)
        {
            pattern = Regex.Replace(pattern, @"  ", @"\s+");  // 2 spaces means some whitespace
            return Regex.Replace(pattern, @" ", @"\s*");  // 1 space means optional whitespace
        }

        private static SYMBOL StringToSymbol(string str)
        {
            return (SYMBOL)Convert.ChangeType(str, typeof(SYMBOL));
        }

        /// <summary>
        /// Extracts automaton/trasducer constructor parameters by parsing text file.
        /// </summary>
        /// <remarks>
        /// Determines file format from file extension or file contents.
        /// </remarks>
        /// <param name="fileName">File name.</param>
        /// <param name="type">Expected type (automaton or transducer).</param>
        /// <param name="initialState">Initial state.</param>
        /// <param name="finalStates">Final states.</param>
        /// <param name="moves">Moves.</param>
        /// <param name="alphabet">Alphabet (may be <c>null</c>).</param>
        /// <param name="name">Name (may be <c>null</c>).</param>
        /// <param name="stateNames">State names (may be <c>null</c>).</param>
        /// <param name="stateSymbolsFileName">Path to state symbols file (optional and only used with FSM format).</param>
        /// <param name="inputSymbolsFileName">Path to input arc symbols file (optional and only used with FSM format).</param>
        /// <param name="outputSymbolsFileName">Path to output arc symbols file (optional and only used with a transducer in FSM format).</param>
        public static void ParseAutomaton(
			string fileName, AutomatonType type,
			out int initialState, out Set<int> finalStates, out Set<Move<ILabel<SYMBOL>>> moves,
			out Set<SYMBOL> alphabet, out string name, out Dictionary<int,string> stateNames,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
		{
			string input = File.ReadAllText(fileName);
			ParseAction parse = null;
			bool deriveName = false;  // derive automaton name from file name?
            string extension = Path.GetExtension(fileName);

            // try guessing format from file extension
            switch (extension) {
			case "timbuk":
            case "tmb":
				parse = ParseAutomatonTimbuk;
				break;
            case "fsa":
			case "pl":
                parse = ParseAutomatonFSA;
				deriveName = true;
				break;
            case "fsm":
                parse = ParseAutomatonFSM;
                deriveName = true;
                break;
			}

			if (parse == null) {  // try guessing format from file contents
				var regexTimbuk = new Regex(@"^Ops.*Automaton.*States.*Final States.*Transitions", RegexOptions.Singleline);
				var regexFSA = new Regex(@"fa\(.*\)", RegexOptions.Singleline);
                var regexFSM = new Regex(@"^[\d\s.]*$");  // only if no symbol files specified, otherwise assume FSM

                if (regexTimbuk.IsMatch(input)) {
					parse = ParseAutomatonTimbuk;
				} else if (regexFSA.IsMatch(input)) {
					parse = ParseAutomatonFSA;
					deriveName = true;
                } else if ((stateSymbolsFileName ?? inputSymbolsFileName ?? outputSymbolsFileName) != null 
                            || regexFSM.IsMatch(input)) {
                    parse = ParseAutomatonFSM;
                    deriveName = true;
                }
			}

            if (parse == null)
                throw PrinterException.UnknownFormat(type);

            parse(
                input, type, 
                out initialState, out finalStates, out moves, out alphabet, out name, out stateNames,
                stateSymbolsFileName, inputSymbolsFileName, outputSymbolsFileName
            );

            if (deriveName)
                name = Path.GetFileNameWithoutExtension(fileName);
		}

		public static void ParseAutomatonTimbuk(
			string input, AutomatonType type,
			out int initialState, out Set<int> finalStates, out Set<Move<ILabel<SYMBOL>>> moves,
            out Set<SYMBOL> alphabet, out string name, out Dictionary<int,string> stateNames,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
		{
			string labelDeclPattern = @"[^:]+:\d+";
			string labelListPattern = string.Format(@"(?:({0})\s+)*", labelDeclPattern);
			string stateListPattern = @"(?:(\w+)  )*";
			string transitionPattern = @".*? \( (?:\w+(?: , \w+)*)? \) -> \w+";
			string transitionListPattern = string.Format(@"(?:({0})  )*", transitionPattern);
			string automatonPattern = string.Format(
				@"Automaton  (.*?)  States  {0}Final States  {0}Transitions  {1}", 
				stateListPattern, transitionListPattern
			);
			string filePattern = string.Format(@"^Ops  {0}  {1} $", labelListPattern, automatonPattern);

			var fileRegex = new Regex(_(filePattern));
			Match match = fileRegex.Match(input);

            if (!match.Success)
                throw FSAPrinterException.InvalidFormat(type);

			alphabet = new Set<SYMBOL>();
            SYMBOL startSymbol = default(SYMBOL);  // meaningless assignment
            bool foundStartSymbol = false;
			foreach (Capture capture in match.Groups[1].Captures) {
				string[] parts = capture.Value.Split(':');
                SYMBOL symbol = StringToSymbol(parts[0]);
				int arity = int.Parse(parts[1]);

				switch (arity) {
                case 0:
                    if (foundStartSymbol)
                        throw FSAPrinterException.DuplicateLabelDecl(type);
                    startSymbol = symbol;
                    foundStartSymbol = true;
					break;
				case 1:
					if (alphabet.Contains(symbol))
                        throw FSAPrinterException.DuplicateLabelDecl(type);
					alphabet.Add(symbol);
					break;
                default:
                    throw FSAPrinterException.TreeAutomataNotSupported(type);
				}
			}
            if (!foundStartSymbol)
                throw FSAPrinterException.NoStartSymbol(type);

			name = match.Groups[2].Value;

			var stateDict = new Dictionary<string,int>(match.Groups[3].Captures.Count);
			int id = 0;
			var states = new Set<int>();
			finalStates = new Set<int>();
			moves = new Set<Move<ILabel<SYMBOL>>>();

			foreach (Capture capture in match.Groups[3].Captures) {
				string stateName = capture.Value;
                if (stateDict.ContainsKey(stateName))
                    throw FSAPrinterException.DuplicateState(type);
				int state = id++;
				stateDict[stateName] = state;
				states.Add(state);
			}

			foreach (Capture capture in match.Groups[4].Captures) {
				string stateName = capture.Value;
				int state;
                if (!stateDict.TryGetValue(stateName, out state))
                    throw FSAPrinterException.UnknownFinalState(type);
                if (finalStates.Contains(state))
                    throw FSAPrinterException.DuplicateFinalState(type);
				finalStates.Add(state);
			}

            Func<string,ILabel<SYMBOL>> parsePredicate = predString => {
                if (predString == "")
                    return null;

                var regex = new Regex(@"^(in|not_in)\{(.*)\}$");
                Match predMatch = regex.Match(predString);

                if (predMatch.Success) {
                    PredicateType predType = predMatch.Groups[1].Value == "in" ?
                        PredicateType.In : PredicateType.NotIn;
                    var symbols = predMatch.Groups[2].Value == "" ?
                       new Set<SYMBOL>() : 
                       new Set<SYMBOL>(predMatch.Groups[2].Value.Split(',').Select(s => StringToSymbol(s)));
                    return new Predicate<SYMBOL>(predType, symbols);
                }

                return new Predicate<SYMBOL>(StringToSymbol(predString));
            };
            Func<string,ILabel<SYMBOL>> parseLabel = labelString => {
                string[] parts = labelString.Split('/');
                switch (parts.Length) {
                case 1:
                    return new Label<SYMBOL>((Predicate<SYMBOL>)parsePredicate(parts[0]));
                case 2:
                    if (parts.All(s => s.StartsWith("@"))) {
                        var inputPred = (Predicate<SYMBOL>)parsePredicate(parts[0].Substring(1));
                        var outputPred = (Predicate<SYMBOL>)parsePredicate(parts[1].Substring(1));
                        if (inputPred == null ? outputPred != null : !inputPred.Equals(outputPred))
                            throw FSAPrinterException.InvalidIdentityLabel(type);
                        return new Label<SYMBOL>(inputPred);
                    }
                    return new Label<SYMBOL>(
                        (Predicate<SYMBOL>)parsePredicate(parts[0]), 
                        (Predicate<SYMBOL>)parsePredicate(parts[1])
                    );
                default:
                    throw FSAPrinterException.InvalidTransducerLabel(type);
                }
            };

            Func<string,ILabel<SYMBOL>> parseILabel = (type == AutomatonType.SSA) ?
                parsePredicate : parseLabel;

			transitionPattern = @"(.*) \( (\w*) \) -> (\w+)";
			var transitionRegex = new Regex(_(transitionPattern));
			bool foundInitialState = false;
			initialState = 0;  // meaningless, but prevents compilation error

			foreach (Capture capture in match.Groups[5].Captures) {
				Match transMatch = transitionRegex.Match(capture.Value);

                if (!transMatch.Success)
                    throw FSAPrinterException.UnknownSymbol(type);

				if (transMatch.Groups[2].Value == "") {
                    if (!StringToSymbol(transMatch.Groups[1].Value).Equals(startSymbol))
                        throw FSAPrinterException.UnknownSymbol(type);

                    if (!stateDict.TryGetValue(transMatch.Groups[3].Value, out initialState))
                        throw FSAPrinterException.UnknownState(type);

					foundInitialState = true;
					continue;
				}

				ILabel<SYMBOL> label = parseILabel(transMatch.Groups[1].Value);
				int sourceState, targetState;
				if (!stateDict.TryGetValue(transMatch.Groups[2].Value, out sourceState) ||
                    !stateDict.TryGetValue(transMatch.Groups[3].Value, out targetState))
                    throw FSAPrinterException.UnknownState(type);
                
                if (label.Symbols > alphabet)
                    throw FSAPrinterException.UnknownSymbol(type);

				moves.Add(new Move<ILabel<SYMBOL>>(sourceState, targetState, label));
			}

            if (!foundInitialState)
                throw FSAPrinterException.NoInitialState(type);

			stateNames = new Dictionary<int,string>(stateDict.Count);
			foreach (KeyValuePair<string,int> item in stateDict)
				stateNames[item.Value] = item.Key;
		}

		// FSA format spec at http://www.let.rug.nl/~vannoord/Fsa/Manual/node5.html#anc1
		public static void ParseAutomatonFSA(
			string input, AutomatonType type,
			out int initialState, out Set<int> finalStates, out Set<Move<ILabel<SYMBOL>>> moves,
            out Set<SYMBOL> alphabet, out string name, out Dictionary<int,string> stateNames,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
		{
			var regexInlineComments = new Regex(@"%.*(?=\n)");
			var regexBlockComments = new Regex(@"/\*.*?\*/", RegexOptions.Singleline);
			input = regexInlineComments.Replace(input, "");
			input = regexBlockComments.Replace(input, "");

			string prologListFormatString = @"\[(?: {0} ,? )*\]";

			string symbolsPattern = (type == AutomatonType.SSA) ?
                @"r \( (\w+) \)" : @"t \( (\w+ , \w+) \)";
			string statesPattern = @"(\d+)";
			string startsPattern = string.Format(prologListFormatString, @"(\d+)");
			string finalsPattern = string.Format(prologListFormatString, @"(\d+)");
            string atomPattern = @"(?:'[^']*'|[\w<>|[\]{}!?+*/#$%@=-]+)";
            string predicatePattern = string.Format(@"(?:{0}|(?:not_)?in \( {1} \))",
                atomPattern, string.Format(prologListFormatString, atomPattern));
			string transsPattern = string.Format(prologListFormatString,
				string.Format(@"trans \( (\d+) , ({0}) , (\d+) \)",
                    (type == AutomatonType.SSA) ? predicatePattern : 
                        string.Format(@"{0} \/ {0}|(?:\$@|'\$@') \( {0} \) \/ (?:\$@|'\$@') \( {0} \)",
                            string.Format(@"(?:{0}|\[ \])", predicatePattern))));
			string jumpsPattern = string.Format(prologListFormatString, @"jump \( (\d+) , (\d+) \)");
			string filePattern = string.Format(@"fa \( {0} , {1} , {2} , {3} , {4} , {5} \) \.",
				symbolsPattern, statesPattern, startsPattern, finalsPattern, transsPattern, jumpsPattern);

			var fileRegex = new Regex(_(filePattern));
			Match match = fileRegex.Match(input);

            if (!match.Success)
                throw FSAParserException.InvalidFormat(type);

            Func<string,bool> isFsaFrozen = module => {
                switch (module) {
                case "fsa_preds":
                    return false;
                case "fsa_frozen":
                    return true;
                default:
                    throw FSAParserException.UnsupportedPredicateModule(type);
                }
            };
            bool inputSymbolAsIs;
            bool outputSymbolAsIs = true;  // meaningless assignment
            if (type == AutomatonType.SSA) {
                inputSymbolAsIs = isFsaFrozen(match.Groups[1].Value);
            } else {
                string[] parts = Regex.Split(match.Groups[1].Value, _(@" , "));
                inputSymbolAsIs = isFsaFrozen(parts[0]);
                outputSymbolAsIs = isFsaFrozen(parts[1]);
            }

			int stateCount = int.Parse(match.Groups[2].Value);

			var stateDict = new Dictionary<int,int>(stateCount);
			int id = 0;

			var startStates = new Set<int>();
			foreach (Capture capture in match.Groups[3].Captures) {
				int stateNum = int.Parse(capture.Value);
				int state;
				if (!stateDict.TryGetValue(stateNum, out state))
                    stateDict[stateNum] = state = id++;
                if (startStates.Contains(state))
                    throw FSAParserException.DuplicateStartState(type);
				startStates.Add(state);
			}

			finalStates = new Set<int>();
			foreach (Capture capture in match.Groups[4].Captures) {
				int stateNum = int.Parse(capture.Value);
				int state;
				if (!stateDict.TryGetValue(stateNum, out state))
                    stateDict[stateNum] = state = id++;
                if (finalStates.Contains(state))
                    throw FSAParserException.DuplicateFinalState(type);
				finalStates.Add(state);
            }

            var asIsPredicateRegex = new Regex(atomPattern);
            var inNotInPredicateRegex = new Regex(_(string.Format(
                @"(in|not_in) \( {0} \)", string.Format(prologListFormatString, string.Format(@"({0})", atomPattern))
            )));
            Func<string,string> stripQuotes = atom =>
                atom[0] == '\'' && atom[atom.Length - 1] == '\'' ? atom.Substring(1, atom.Length - 2) : atom;
            Func<string,bool,ILabel<SYMBOL>> parsePredicate = (predString, asIs) => {
                if (asIs) {
                    if (!asIsPredicateRegex.IsMatch(predString))
                        throw FSAParserException.InvalidPredicate(type);
                    return new Predicate<SYMBOL>(StringToSymbol(stripQuotes(predString)));
                } else {
                    Match predMatch = inNotInPredicateRegex.Match(predString);
                    if (predMatch.Success) {
                        var symbols = new Set<SYMBOL>();
                        foreach (Capture capture in predMatch.Groups[2].Captures)
                            symbols.Add(StringToSymbol(stripQuotes(capture.Value)));
                        return new Predicate<SYMBOL>(
                            (predMatch.Groups[1].Value == "in") ? PredicateType.In : PredicateType.NotIn,
                            symbols
                        );
                    } else {
                        if (!asIsPredicateRegex.IsMatch(predString))
                            throw FSAParserException.InvalidPredicate(type);
                        return new Predicate<SYMBOL>(StringToSymbol(stripQuotes(predString)));
                    }
                }
            };
            Func<string,ILabel<SYMBOL>> parseInputPredicate = (predString => parsePredicate(predString, inputSymbolAsIs));
            Func<string,ILabel<SYMBOL>> parseLabel = labelString => {
                string[] parts = labelString.Split('/');
                var predicates = new Predicate<SYMBOL>[2];
                bool isIdentity = false;
                var emptyListRegex = new Regex(_(@"^\[ \]$"));
                var identityRegex = new Regex(_(string.Format(@"^\$@ \( ({0}) \)$", predicatePattern)));
                for (int i = 0; i < 2; i++) {
                    if (emptyListRegex.IsMatch(parts[i])) {
                        predicates[i] = null;
                    } else {
                        Match identityMatch = identityRegex.Match(parts[i]);
                        string predString;
                        if (identityMatch.Success) {
                            isIdentity = true;
                            predString = identityMatch.Groups[1].Value;
                        } else {
                            predString = parts[i];
                        }
                        predicates[i] = (Predicate<SYMBOL>)parsePredicate(predString, (i == 0) ? inputSymbolAsIs : outputSymbolAsIs);
                    }
                }
                if (isIdentity) {
                    if (predicates[0] == null) {
                        if (predicates[1] != null)
                            throw FSAParserException.InvalidIdentityLabel(type);
                    } else {
                        if (predicates[1] == null)
                            throw FSAParserException.InvalidIdentityLabel(type);
                        if (predicates[0].Type == predicates[1].Type && predicates[0].Set != predicates[1].Set)
                            throw FSAParserException.InvalidIdentityLabel(type);
                    }
                    return new Label<SYMBOL>(predicates[0]);
                }
                return new Label<SYMBOL>(predicates[0], predicates[1]);
            };
            Func<string,ILabel<SYMBOL>> parseILabel = (type == AutomatonType.SSA) ?
                parseInputPredicate : parseLabel;

			moves = new Set<Move<ILabel<SYMBOL>>>();

			int transitionCount = match.Groups[5].Captures.Count;
			for (int i = 0; i < transitionCount; i++) {
				int sourceStateNum = int.Parse(match.Groups[5].Captures[i].Value);
				ILabel<SYMBOL> label = parseILabel(match.Groups[6].Captures[i].Value);
				int targetStateNum = int.Parse(match.Groups[7].Captures[i].Value);
				int sourceState, targetState;
				if (!stateDict.TryGetValue(sourceStateNum, out sourceState))
                    stateDict[sourceStateNum] = sourceState = id++;
				if (!stateDict.TryGetValue(targetStateNum, out targetState))
                    stateDict[targetStateNum] = targetState = id++;
				moves.Add(new Move<ILabel<SYMBOL>>(sourceState, targetState, label));
			}

			int jumpCount = match.Groups[8].Captures.Count;
			for (int i = 0; i < jumpCount; i++) {
				int sourceStateNum = int.Parse(match.Groups[8].Captures[i].Value);
				int targetStateNum = int.Parse(match.Groups[9].Captures[i].Value);
				int sourceState, targetState;
				if (!stateDict.TryGetValue(sourceStateNum, out sourceState))
                    stateDict[sourceStateNum] = sourceState = id++;
				if (!stateDict.TryGetValue(targetStateNum, out targetState))
                    stateDict[sourceStateNum] = targetState = id++;
				moves.Add(Move<ILabel<SYMBOL>>.Epsilon(sourceState, targetState));
			}

            if (stateDict.Count > stateCount)
                throw FSAParserException.StateCountMismatch(type);

			initialState = 0;  // meaningless, prevents compiler error
			if (startStates.Count < 1) {
                throw FSAParserException.NoStartState(type);
			} else if (startStates.Count == 1) {
                initialState = startStates.First();
			} else {
				initialState = id++;
				for (int i = 0; ; i++) {
					if (!stateDict.ContainsKey(i)) {
						stateDict[i] = initialState;
						break;
					}
				}
				foreach (int startState in startStates)
					moves.Add(Move<ILabel<SYMBOL>>.Epsilon(initialState, startState));
			}

			alphabet = null;
			name = null;
			stateNames = null;
		}

        // format spec at http://web.eecs.umich.edu/~radev/NLP-fall2015/resources/fsm_archive/fsm.5.html
        public static void ParseAutomatonFSM(
            string input, AutomatonType type,
            out int initialState, out Set<int> finalStates, out Set<Move<ILabel<SYMBOL>>> moves,
            out Set<SYMBOL> alphabet, out string name, out Dictionary<int,string> stateNames,
            string stateSymbolsFileName = null, string inputSymbolsFileName = null, string outputSymbolsFileName = null)
        {
            string statePattern = (stateSymbolsFileName == null) ? @"\d+" : @"[^\s]+";
            string inputSymbolPattern = (inputSymbolsFileName == null) ? @"\d+" : @"[^\s]+";
            string outputSymbolPattern = (outputSymbolsFileName == null) ? @"\d+" : @"[^\s]+";
            var transitionRegex = new Regex(_(string.Format(
                (type == AutomatonType.SSA) ? 
                @" ({0})  ({0})  ({1})(?:  \d*\.\d*)? " :
                @" ({0})  ({0})  ({1})  ({2})(?:  \d*\.\d*)? ", 
                statePattern, inputSymbolPattern, outputSymbolPattern
            )));
            var finalStateRegex = new Regex(_(string.Format(@" ({0})(?:  \d*\.\d*)? ", statePattern)));
            var blankLineRegex = new Regex(@"\s*");
            var symbolRegex = new Regex(_(@" ([^\s]+)  (\d+) "));
            Match match;

            Dictionary<string,int> stateDict = null;
            if (stateSymbolsFileName != null) {
                stateDict = new Dictionary<string,int>();
                foreach (string stateLine in File.ReadLines(stateSymbolsFileName)) {
                    match = symbolRegex.Match(stateLine);
                    if (match.Success) {
                        stateDict[match.Groups[1].Value] = int.Parse(match.Groups[2].Value);
                    } else if (!blankLineRegex.IsMatch(stateLine)) {
                        throw FSMParserException.InvalidStateSymbolsFile(type);
                    }
                }
            }

            alphabet = null;
            string[] symbolsFileNames = (type == AutomatonType.SSA) ?
                new string[] { inputSymbolsFileName } :
                new string[] { inputSymbolsFileName, outputSymbolsFileName };
            foreach (string symbolsFileName in symbolsFileNames) {
                if (symbolsFileName != null) {
                    alphabet = alphabet ?? new Set<SYMBOL>();
                    foreach (string symbolLine in File.ReadLines(symbolsFileName)) {
                        match = symbolRegex.Match(symbolLine);
                        if (match.Success) {
                            alphabet.Add(StringToSymbol(match.Groups[1].Value));
                        } else if (!blankLineRegex.IsMatch(symbolLine)) {
                            throw FSMParserException.InvalidArcSymbolsFile(type);
                        }
                    }
                }
            }

            moves = new Set<Move<ILabel<SYMBOL>>>();
            finalStates = new Set<int>();
            initialState = 0;  // meaningless, prevents compiler error

            Func<string,Predicate<SYMBOL>> parsePredicate = (symbol =>
                (symbol == "0") ? null : new Predicate<SYMBOL>(StringToSymbol(symbol))
            );
            bool isFirstTransition = true;
            var reader = new StringReader(input);
            string line;
            while ((line = reader.ReadLine()) != null) {
                match = transitionRegex.Match(line);
                if (match.Success) {
                    int sourceState, targetState;
                    if (stateSymbolsFileName == null) {
                        sourceState = int.Parse(match.Groups[1].Value);
                        targetState = int.Parse(match.Groups[2].Value);
                    } else {
                        if (!stateDict.TryGetValue(match.Groups[1].Value, out sourceState) ||
                            !stateDict.TryGetValue(match.Groups[2].Value, out targetState))
                            throw FSMParserException.UnknownStateSymbol(type);
                    }
                    
                    if (inputSymbolsFileName != null && !alphabet.Contains(StringToSymbol(match.Groups[3].Value)))
                        throw FSMParserException.UnknownArcSymbol(type);
                    ILabel<SYMBOL> label;
                    var inputPredicate = parsePredicate(match.Groups[3].Value);
                    if (type == AutomatonType.SST) {
                        if (outputSymbolsFileName != null && !alphabet.Contains(StringToSymbol(match.Groups[4].Value)))
                            throw FSMParserException.UnknownArcSymbol(type);
                        var outputPredicate = parsePredicate(match.Groups[4].Value);
                        label = new Label<SYMBOL>(inputPredicate, outputPredicate);
                    } else {
                        label = inputPredicate;
                    }

                    moves.Add(new Move<ILabel<SYMBOL>>(sourceState, targetState, label));

                    if (isFirstTransition) {
                        initialState = sourceState;
                        isFirstTransition = false;
                    }
                } else {
                    match = finalStateRegex.Match(line);
                    if (!match.Success) {
                        if (blankLineRegex.IsMatch(line))  // permit blank line
                            continue;
                        throw FSMParserException.InvalidFormat(type);
                    }

                    int finalState = int.Parse(match.Groups[1].Value);
                    finalStates.Add(finalState);
                }
            }

            if (isFirstTransition)
                throw FSMParserException.NoTransitions(type);

            name = null;

            stateNames = null;
            if (stateDict != null) {
                stateNames = new Dictionary<int,string>(stateDict.Count);
                foreach (KeyValuePair<string,int> item in stateDict)
                    stateNames[item.Value] = item.Key;
            }
        }
	}
}

