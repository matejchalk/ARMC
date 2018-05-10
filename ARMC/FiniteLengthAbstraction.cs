/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;
using System.Linq;
using System.IO;

namespace ARMC
{
    /// <summary>
    /// Abstraction based on finite length languages.
    /// </summary>
    public class FiniteLengthAbstraction<SYMBOL> : Abstraction<SYMBOL>
    {
        private int bound;
        private readonly bool forward;
        private readonly bool trace;
        private readonly Config.BoundInc boundInc;
        private readonly bool halveBoundInc;

        /// <summary>
        /// Gets current bound value.
        /// </summary>
        public int Bound { get { return bound; } }

        public FiniteLengthAbstraction(Config config, SSA<SYMBOL> init = null, SSA<SYMBOL> bad = null, SST<SYMBOL>[] tau = null)
        {
            if (config.InitialBound == Config.InitBound.One) {
                bound = 1;
            } else {
                SSA<SYMBOL> ssa;
                if (config.InitialBound == Config.InitBound.Init) {
                    ssa = init ?? SSA<SYMBOL>.Parse(config.InitFilePath);
                } else {
                    ssa = bad ?? SSA<SYMBOL>.Parse(config.BadFilePath);
                }
                bound = ssa.States.Count();
                if (config.HalveInitialBound)
                    bound /= 2;
            }

            forward = (config.LanguageDirection == Config.Direction.Forward);
            trace = config.TraceLanguages;
            boundInc = config.BoundIncrement;
            halveBoundInc = config.HalveBoundIncrement;
        }

        public override bool StatesAreEquivalent(SSA<SYMBOL> m, int q1, int q2)
        {
            Func<int,int,SSA<SYMBOL>> boundedLang = trace ?
                (forward ? (Func<int,int,SSA<SYMBOL>>)m.BoundedForwardTraceLanguage : m.BoundedBackwardTraceLanguage) :
                (forward ? (Func<int,int,SSA<SYMBOL>>)m.BoundedForwardStateLanguage : m.BoundedBackwardStateLanguage);

            return (boundedLang(q1, bound) == boundedLang(q2, bound));
        }

        public override void Refine(SSA<SYMBOL> m, SSA<SYMBOL> x)
        {
            int inc;
            if (boundInc == Config.BoundInc.One) {
                inc = 1;
            } else {
                SSA<SYMBOL> ssa;
                if (boundInc == Config.BoundInc.M) {
                    ssa = m;
                } else {
                    ssa = x;
                }
                inc = ssa.States.Count();
                if (halveBoundInc)
                    inc /= 2;
            }

            bound += inc;
        }

        internal override void Print(string dir, ARMC<SYMBOL> armc)
        {
            using (var file = new StreamWriter(Path.Combine(dir, "bound")))
                file.WriteLine(bound);
        }
    }
}

