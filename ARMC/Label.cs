/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Automata;

namespace ARMC
{
    /// <summary>
    /// Label class, used for SST labels.
    /// </summary>
	public class Label<SYMBOL> : ILabel<SYMBOL>
	{
        /// <summary>
        /// Input predicate (or <c>null</c> if epsilon).
        /// </summary>
        public Predicate<SYMBOL> Input { get; }

        /// <summary>
        /// Output predicate (or <c>null</c> if epsilon or is identity label).
        /// </summary>
		public Predicate<SYMBOL> Output { get; }

        /// <summary>
        /// Identity label indicator.
        /// </summary>
        public bool IsIdentity { get; }

		public Set<SYMBOL> Symbols
		{
			get {
                var symbols = new Set<SYMBOL>();
                if (Input != null)
                    symbols |= Input.Symbols;
                if (Output != null)
                    symbols |= Output.Symbols;
                return symbols;
            }
        }

        /// <summary>
        /// Constructs label.
        /// </summary>
        /// <param name="input">Input predicate.</param>
        /// <param name="output">Output predicate.</param>
        public Label(Predicate<SYMBOL> input, Predicate<SYMBOL> output)
        {
            Input = input;
            Output = output;
            IsIdentity = false;
        }

        /// <summary>
        /// Construct identity label.
        /// </summary>
        /// <param name="input">Input predicate.</param>
		public Label(Predicate<SYMBOL> input)
		{
			Input = input;
			Output = null;
            IsIdentity = true;
		}

        /// <summary>
        /// Invert label.
        /// </summary>
        /// <returns>Inverted label.</returns>
		public Label<SYMBOL> Invert()
		{
            if (IsIdentity)
                return new Label<SYMBOL>(Input);
			return new Label<SYMBOL>(Output, Input);
		}

        public override string ToString()
		{
			if (IsIdentity)
				return string.Format("@{0}", Input);
			return string.Format("{0}/{1}", Input, Output);
        }

        public override bool Equals(object obj)
        {
            Label<SYMBOL> label = obj as Label<SYMBOL>;
            if (label == null)
                return false;
            return this.IsIdentity == label.IsIdentity
                       && (this.Input == null ? label.Input == null : this.Input.Equals(label.Input))
                       && (this.Output == null ? label.Output == null : this.Output.Equals(label.Output));
        }

        public override int GetHashCode()
        {
            int hashCode = IsIdentity.GetHashCode();
            if (Input != null)
                hashCode ^= Input.GetHashCode();
            if (Output != null)
                hashCode ^= Output.GetHashCode();
            return hashCode;
        }
	}

    /// <summary>
    /// Boolean algebra operating over transducer labels.
    /// </summary>
	public class LabelAlgebra<SYMBOL> : IBooleanAlgebra<Label<SYMBOL>>
	{
		private PredicateAlgebra<SYMBOL> pa;
		private MintermGenerator<Label<SYMBOL>> mtg;


        /// <summary>
        /// Construct label algebra.
        /// </summary>
        /// <param name="alphabet">Alphabet.</param>
		public LabelAlgebra(Set<SYMBOL> alphabet)
		{
			this.pa = new PredicateAlgebra<SYMBOL>(alphabet);
			this.mtg = new MintermGenerator<Label<SYMBOL>>(this);
		}

        /// <summary>
        /// Alphabet.
        /// </summary>
		public Set<SYMBOL> Alphabet
		{
			get { return pa.Alphabet; }
            internal set { pa.Alphabet = value; }
		}

        /// <summary>
        /// Combines labels.
        /// </summary>
        /// <param name="l1">Label.</param>
        /// <param name="l2">Label.</param>
        /// <returns>Composed label (universally false if labels incompatible).</returns>
		public Label<SYMBOL> Combine(Label<SYMBOL> l1, Label<SYMBOL> l2)
		{
			Predicate<SYMBOL> output1 = (l1.IsIdentity ? l1.Input : l1.Output);
			Predicate<SYMBOL> output2 = (l2.IsIdentity ? l2.Input : l2.Output);

			if (!pa.IsSatisfiable(output1 & l2.Input))
				return False;

			if (l1.IsIdentity || l2.IsIdentity)
				return new Label<SYMBOL>(l1.Input & output2);
			return new Label<SYMBOL>(l1.Input, l2.Output);
		}

		public Label<SYMBOL> True
		{
			get
			{ 
				return new Label<SYMBOL>(pa.True, pa.True);
			}
		}

		public Label<SYMBOL> False
		{
			get
			{ 
				return new Label<SYMBOL>(pa.False, pa.False);
			}
		}
			
		public Label<SYMBOL> MkAnd(Label<SYMBOL> l1, Label<SYMBOL> l2)
		{
			if (l1.IsIdentity || l2.IsIdentity)
				return new Label<SYMBOL>(pa.MkAnd(l1.Input, l2.Input));
			return new Label<SYMBOL>(pa.MkAnd(l1.Input, l2.Input), pa.MkAnd(l1.Output, l2.Output));
		}

		public Label<SYMBOL> MkOr(Label<SYMBOL> l1, Label<SYMBOL> l2)
		{
			if (l1.IsIdentity || l2.IsIdentity)
				return new Label<SYMBOL>(pa.MkOr(l1.Input, l2.Input));
			return new Label<SYMBOL>(pa.MkOr(l1.Input, l2.Input), pa.MkOr(l1.Output, l2.Output));
		}

		public Label<SYMBOL> MkAnd(params Label<SYMBOL>[] labels)
            => labels.Aggregate(True, MkAnd);

        public Label<SYMBOL> MkOr(params Label<SYMBOL>[] labels)
            => labels.Aggregate(False, MkOr);

        public Label<SYMBOL> MkAnd(IEnumerable<Label<SYMBOL>> labels)
            => labels.Aggregate(True, MkAnd);

        public Label<SYMBOL> MkOr(IEnumerable<Label<SYMBOL>> labels)
            => labels.Aggregate(False, MkOr);

		public Label<SYMBOL> MkNot(Label<SYMBOL> label)
		{
			if (label.IsIdentity)
				return new Label<SYMBOL>(pa.MkNot(label.Input));
			return new Label<SYMBOL>(pa.MkNot(label.Input), pa.MkNot(label.Output));
		}

		public Label<SYMBOL> MkDiff(Label<SYMBOL> l1, Label<SYMBOL> l2)
		{
			return MkAnd(l1, MkNot(l2));
		}

		public Label<SYMBOL> MkSymmetricDifference(Label<SYMBOL> l1, Label<SYMBOL> l2)
		{
			return MkOr(MkDiff(l1, l2), MkDiff(l2, l1));
		}

		public bool IsSatisfiable(Label<SYMBOL> label)
		{
			if (label.IsIdentity)
				return pa.IsSatisfiable(label.Input);
			return pa.IsSatisfiable(label.Input) && pa.IsSatisfiable(label.Output);
		}

		public bool AreEquivalent(Label<SYMBOL> l1, Label<SYMBOL> l2)
		{
			if (l1.IsIdentity != l2.IsIdentity) {
				Set<SYMBOL> i1 = pa.InclusiveSet(l1.Input);
				Set<SYMBOL> i2 = pa.InclusiveSet(l2.Input);
				Set<SYMBOL> o = pa.InclusiveSet(l1.IsIdentity ? l2.Output : l1.Output);
				return (i1.Count == 1 && i2.Count == 1 && o.Count == 1 && i1 == i2 && i2 == o);
			}
			if (l1.IsIdentity)  // l2.IsIdentity implicit
				return pa.AreEquivalent(l1.Input, l2.Input);
			return pa.AreEquivalent(l1.Input, l2.Input) && pa.AreEquivalent(l1.Output, l2.Output);
		}

		public bool CheckImplication(Label<SYMBOL> l1, Label<SYMBOL> l2)
		{
			throw new NotImplementedException();
		}

		public bool IsExtensional
		{
			get { return false; }
		}

		public Label<SYMBOL> Simplify(Label<SYMBOL> predicate)
		{
			throw new NotImplementedException();
		}

		public bool IsAtomic
		{
			get { throw new NotImplementedException(); }
		}

		public Label<SYMBOL> GetAtom(Label<SYMBOL> psi)
		{
			throw new NotImplementedException();
		}

		public bool EvaluateAtom(Label<SYMBOL> atom, Label<SYMBOL> psi)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<Tuple<bool[], Label<SYMBOL>>> GenerateMinterms(params Label<SYMBOL>[] constraints)
		{
			return mtg.GenerateMinterms(constraints);
		}

		public override bool Equals(object obj)
		{
			LabelAlgebra<SYMBOL> algebra = obj as LabelAlgebra<SYMBOL>;
			if (obj != null)
                return (this.pa.Equals(algebra.pa));
			return false;
		}

		public override int GetHashCode()
		{
			return pa.Alphabet.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("[LabelAlgebra: Alphabet={0}]", pa.Alphabet);
		}
	}
}

