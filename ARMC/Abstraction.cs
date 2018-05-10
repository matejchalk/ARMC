/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

namespace ARMC
{
    /// <summary>
    /// Abstraction base.
    /// </summary>
    public abstract class Abstraction<SYMBOL>
    {
        /// <summary>
        /// Checks if states are considered equivalent.
        /// </summary>
        /// <param name="m">Automaton.</param>
        /// <param name="q1">Automaton state.</param>
        /// <param name="q2">Automaton state.</param>
        /// <returns><c>true</c>, if states are equivalent, <c>false</c> otherwise.</returns>
        public abstract bool StatesAreEquivalent(SSA<SYMBOL> m, int q1, int q2);

        /// <summary>
        /// Refine the abstraction based on diverging automata from spurious counterexample.
        /// </summary>
        /// <param name="m">Automaton M_k.</param>
        /// <param name="x">Automaton X_k.</param>
        public abstract void Refine(SSA<SYMBOL> m, SSA<SYMBOL> x);

        /// <summary>
        /// Collapse the automaton, i.e. overapproximate its language.
        /// </summary>
        /// <param name="m">Automaton.</param>
        /// <returns>The collapsed automaton.</returns>
        public virtual SSA<SYMBOL> Collapse(SSA<SYMBOL> m)
        {
            return m.Collapse(StatesAreEquivalent);
        }

        internal abstract void Print(string dir, ARMC<SYMBOL> armc);
    }
}

