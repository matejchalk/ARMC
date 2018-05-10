/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;
using System.Collections.Generic;
using Microsoft.Automata;

namespace ARMC
{
	public enum AutomatonType { SSA, SST }

    /// <summary>
    /// Interface for simple symbolic automata/transducers.
    /// </summary>
	public interface ISSAutomaton<SYMBOL>
	{
        /// <summary>
        /// Automaton type (SSA or SST).
        /// </summary>
		AutomatonType Type { get; }

        /// <summary>
        /// Name of automaton/transducer (optional).
        /// </summary>
		string Name { get; set; }

        /// <summary>
        /// Names of each state (optional).
        /// </summary>
		Dictionary<int,string> StateNames { get; set; }

        /// <summary>
        /// Alphabet.
        /// </summary>
		Set<SYMBOL> Alphabet { get; }

        /// <summary>
        /// Initial state.
        /// </summary>
		int InitialState { get; }

        /// <summary>
        /// Final states.
        /// </summary>
		IEnumerable<int> FinalStates { get; }

        /// <summary>
        /// All states.
        /// </summary>
		IEnumerable<int> States { get; }

        /// <summary>
        /// Checks if given state is a final state.
        /// </summary>
        /// <param name="state">State.</param>
        /// <returns><c>true</c>, if state in final states, <c>false</c> otherwise.</returns>
		bool IsFinalState(int state);

        /// <summary>
        /// All automaton/transducer moves, using a generic label interface.
        /// </summary>
        IEnumerable<Move<ILabel<SYMBOL>>> GenericMoves { get; }

        /// <summary>
        /// Normalize the automaton/transducer states form unbroken sequence.
        /// </summary>
        ISSAutomaton<SYMBOL> GenericNormalize();
	}
}

