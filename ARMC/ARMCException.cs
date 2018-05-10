/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;

namespace ARMC
{
    /// <summary>
    /// Base class for ARMC exceptions.
    /// </summary>
    public class ARMCException : Exception
    {
        public ARMCException(string message)
            : base(message)
        {
        }

        public static ARMCException InitialPropertyViolation()
            => new ARMCException("property violated in initial configurations");

        public static ARMCException Timeout()
            => new ARMCException("timeout");
    }

    public class AutomatonException : ARMCException
    {
        public AutomatonException(AutomatonType type, string message)
            : base(string.Format("{0}: {1}", type.ToString(), message))
        {
        }

        public static AutomatonException InvalidStateNames(AutomatonType type)
            => new AutomatonException(type, "invalid state names");
    
        public static AutomatonException UnknownSymbolsInTransitions(AutomatonType type)
            => new AutomatonException(type, "transitions contain symbols not in alphabet");
    }

    public class SSAException : AutomatonException
    {
        public SSAException(string message)
            : base(AutomatonType.SSA, message)
        {
        }

        public static SSAException StateNotInStates()
            => new SSAException("state not in states");

        public static SSAException IncompatibleAlphabets()
            => new SSAException("incompatible alphabets");
    }

    public class SSTException : AutomatonException
    {
        public SSTException(string message)
            : base(AutomatonType.SST, message)
        {
        }

        public static SSTException IncompatibleAlphabets()
            => new SSTException("incompatible alphabets");

        public static SSTException NoSSTsInUnion()
            => new SSTException("transducer union without parameters");
    }

    public class ConfigException : ARMCException
    {
        public ConfigException(string message)
            : base(string.Format("Config: {0}", message))
        {
        }

        public static ConfigException BadFileFormat()
            => new ConfigException("bad file format");

        public static ConfigException UnknownProperty(string name)
            => new ConfigException(string.Format("unknown property '{0}'", name));

        public static ConfigException DuplicateProperty(string name)
            => new ConfigException(string.Format("duplicate property '{0}'", name));

        public static ConfigException BadPropertyValue(string name)
            => new ConfigException(string.Format("bad value for property '{0}'", name));

        public static ConfigException AbstractionNotChosen()
            => new ConfigException("abstraction technique not selected (must be precisely one)");

        public static ConfigException MissingProperty(string name)
            => new ConfigException(string.Format("missing property '{0}'", name));
    }

    public class ParserException : ARMCException
    {
        public ParserException(AutomatonType type, string message)
            : base(string.Format("{0} parser: {1}", type.ToString(), message))
        {
        }

        public static ParserException UnknownFormat(AutomatonType type)
            => new ParserException(type, "unknown automaton format");
    }

    public class TimbukParserException : ParserException
    {
        public TimbukParserException(AutomatonType type, string message)
            : base(type, string.Format("Timbuk: {0}", message))
        {
        }

        public static TimbukParserException InvalidFormat(AutomatonType type)
            => new TimbukParserException(type, "invalid automaton format");

        public static TimbukParserException TreeAutomataNotSupported(AutomatonType type)
            => new TimbukParserException(type, "tree automata not supported");

        public static TimbukParserException DuplicateLabelDecl(AutomatonType type)
            => new TimbukParserException(type, "duplicate label declaration");

        public static TimbukParserException DuplicateState(AutomatonType type)
            => new TimbukParserException(type, "duplicate state");

        public static TimbukParserException DuplicateFinalState(AutomatonType type)
            => new TimbukParserException(type, "duplicate final state");

        public static TimbukParserException NoStartSymbol(AutomatonType type)
            => new TimbukParserException(type, "no start symbol (arity 0) specified");

        public static TimbukParserException NoInitialState(AutomatonType type)
            => new TimbukParserException(type, "no initial state specified");

        public static TimbukParserException UnknownFinalState(AutomatonType type)
            => new TimbukParserException(type, "final state not in states");

        public static TimbukParserException UnknownSymbol(AutomatonType type)
            => new TimbukParserException(type, "unknown symbol in transition");

        public static TimbukParserException UnknownState(AutomatonType type)
            => new TimbukParserException(type, "unknown state in transition");

        public static TimbukParserException InvalidIdentityLabel(AutomatonType type)
            => new TimbukParserException(type, "mismatched predicates in identity label");

        public static TimbukParserException InvalidTransducerLabel(AutomatonType type)
            => new TimbukParserException(type, "invalid transducer label format");
    }

    public class FSAParserException : ParserException
    {
        public FSAParserException(AutomatonType type, string message)
            : base(type, string.Format("FSA: {0}", message))
        {
        }

        public static FSAParserException InvalidFormat(AutomatonType type)
            => new FSAParserException(type, "invalid automaton format");

        public static FSAParserException UnsupportedPredicateModule(AutomatonType type)
            => new FSAParserException(type, "predicate module not supported (must be fsa_preds or fsa_frozen)");

        public static FSAParserException DuplicateStartState(AutomatonType type)
            => new FSAParserException(type, "duplicate start state");

        public static FSAParserException DuplicateFinalState(AutomatonType type)
            => new FSAParserException(type, "duplicate final state");

        public static FSAParserException NoStartState(AutomatonType type)
            => new FSAParserException(type, "no start state");

        public static FSAParserException StateCountMismatch(AutomatonType type)
            => new FSAParserException(type, "contradictory number of states");

        public static FSAParserException InvalidPredicate(AutomatonType type)
            => new FSAParserException(type, "predicate invalid for given module");

        public static FSAParserException InvalidIdentityLabel(AutomatonType type)
            => new FSAParserException(type, "invalid transducer identity label");
    }

    public class FSMParserException : ParserException
    {
        public FSMParserException(AutomatonType type, string message)
            : base(type, string.Format("FSM: {0}", message))
        {
        }

        public static FSMParserException InvalidFormat(AutomatonType type)
            => new FSMParserException(type, "invalid automaton format");

        public static FSMParserException InvalidStateSymbolsFile(AutomatonType type)
            => new FSMParserException(type, "invalid state symbols file format");

        public static FSMParserException InvalidArcSymbolsFile(AutomatonType type)
            => new FSMParserException(type, "invalid arc symbols file format");

        public static FSMParserException UnknownStateSymbol(AutomatonType type)
            => new FSMParserException(type, "state not declared in symbols file");

        public static FSMParserException UnknownArcSymbol(AutomatonType type)
            => new FSMParserException(type, "arc symbol not declared in symbols file");

        public static FSMParserException NoTransitions(AutomatonType type)
            => new FSMParserException(type, "no transitions - cannot determine initial state");
    }
}

