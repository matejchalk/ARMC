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
	public enum PredicateType {In, NotIn};

    /// <summary>
    /// Predicate class, used as label in SSA moves.
    /// </summary>
	public class Predicate<SYMBOL> : ILabel<SYMBOL>
	{
        /// <summary>
        /// Type of predicate ("in" or "not in").
        /// </summary>
		public PredicateType Type { get; set; }

        /// <summary>
        /// Set.
        /// </summary>
		public Set<SYMBOL> Set { get; set; }

		public Set<SYMBOL> Symbols
		{
			get { return Set; }
		}

        /// <summary>
        /// Constructs predicate.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <param name="set">Set.</param>
		public Predicate(PredicateType type, Set<SYMBOL> set)
		{
			this.Type = type;
			this.Set = set;
		}

        /// <summary>
        /// Constructs predicate of type "in".
        /// </summary>
        /// <param name="set">Set.</param>
		public Predicate(Set<SYMBOL> set)
			: this(PredicateType.In, set)
		{
        }

        /// <summary>
        /// Constructs predicate with single symbol in set.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <param name="symbol">Symbol.</param>
		public Predicate(PredicateType type, SYMBOL symbol)
		{
			this.Type = type;
			this.Set = new Set<SYMBOL>();
			this.Set.Add(symbol);
		}

        /// <summary>
        /// Construct predicate accepting only a single symbol.
        /// </summary>
        /// <param name="symbol">Symbol.</param>
		public Predicate(SYMBOL symbol)
			: this(PredicateType.In, symbol)
		{
        }

        /// <summary>
        /// Construct predicate accepting no symbols.
        /// </summary>
		public Predicate()
			: this(PredicateType.In, new Set<SYMBOL>())
		{
		}

		public static Predicate<SYMBOL> operator&(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
		{
			if (p1.Type == PredicateType.In) {
				if (p2.Type == PredicateType.In) {
					return new Predicate<SYMBOL>(PredicateType.In, p1.Set & p2.Set);
				} else {
					return new Predicate<SYMBOL>(PredicateType.In, p1.Set - p2.Set);
				}
			} else {
				if (p2.Type == PredicateType.In) {
					return new Predicate<SYMBOL>(PredicateType.In, p2.Set - p1.Set);
				} else {
					return new Predicate<SYMBOL>(PredicateType.NotIn, p1.Set | p2.Set);
				}
			}
		}

		public static Predicate<SYMBOL> operator|(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
		{
			if (p1.Type == PredicateType.In) {
				if (p2.Type == PredicateType.In) {
					return new Predicate<SYMBOL>(PredicateType.In, p1.Set | p2.Set);
				} else {
					return new Predicate<SYMBOL>(PredicateType.NotIn, p2.Set - p1.Set);
				}
			} else {
				if (p2.Type == PredicateType.In) {
					return new Predicate<SYMBOL>(PredicateType.NotIn, p1.Set - p2.Set);
				} else {
					return new Predicate<SYMBOL>(PredicateType.NotIn, p1.Set & p2.Set);
				}
			}
		}

        public static Predicate<SYMBOL> operator*(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
        {
            return p1 & p2;
        }

        public static Predicate<SYMBOL> operator+(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
        {
            return p1 | p2;
        }

		public static Predicate<SYMBOL> operator~(Predicate<SYMBOL> predicate)
		{
			PredicateType type = (predicate.Type == PredicateType.In) ?
				PredicateType.NotIn : PredicateType.In;
			return new Predicate<SYMBOL>(type, new Set<SYMBOL>(predicate.Set));
		}

		public static Predicate<SYMBOL> operator-(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
		{
			return p1 & ~p2;
		}

		public static Predicate<SYMBOL> operator^(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
		{
			return (p1 - p2) | (p2 - p1);
        }

        /// <summary>
        /// Universally true predicate.
        /// </summary>
        public static Predicate<SYMBOL> True
        {
            get
            { 
                return new Predicate<SYMBOL>(PredicateType.NotIn, new Set<SYMBOL>());
            }
        }

        /// <summary>
        /// Universally false predicate.
        /// </summary>
        public Predicate<SYMBOL> False
        {
            get
            { 
                return new Predicate<SYMBOL>(PredicateType.In, new Set<SYMBOL>());
            }
        }

		public override string ToString()
		{
            if (Type == PredicateType.In) {
                if (Set.Count == 1)
                    return Set.First().ToString();
                return string.Format("in{0}", Set);
            }
            return string.Format("not_in{0}", Set);
		}

        public override bool Equals(object obj)
        {
            Predicate<SYMBOL> predicate = obj as Predicate<SYMBOL>;
            if (predicate == null)
                return false;
            return this.Type == predicate.Type && this.Set == predicate.Set;
        }

        public override int GetHashCode()
        {
            return (int)Type ^ Set.GetHashCode();
        }
    }

    /// <summary>
    /// Boolean algebra operating over predicates.
    /// </summary>
	public class PredicateAlgebra<SYMBOL> : IBooleanAlgebra<Predicate<SYMBOL>>
	{
        /// <summary>
        /// Alphabet.
        /// </summary>
        public Set<SYMBOL> Alphabet { get; internal set; }

        private MintermGenerator<Predicate<SYMBOL>> mtg;

        /// <summary>
        /// Constructs predicate algebra with empty alphabet.
        /// </summary>
		public PredicateAlgebra()
			: this(new Set<SYMBOL>())
		{
		}

        /// <summary>
        /// Constructs predicate algebra.
        /// </summary>
        /// <param name="alphabet">Alphabet.</param>
		public PredicateAlgebra(Set<SYMBOL> alphabet)
		{
			this.Alphabet = alphabet;
			this.mtg = new MintermGenerator<Predicate<SYMBOL>>(this);
        }

        /// <summary>
        /// Gets set of symbols that match the predicate.
        /// </summary>
        /// <param name="predicate">Predicate.</param>
        /// <returns>The set.</returns>
        public Set<SYMBOL> InclusiveSet(Predicate<SYMBOL> predicate)
        {
            if (predicate.Type == PredicateType.NotIn)
                return Alphabet - predicate.Set;
            return predicate.Set;
        }

		public Predicate<SYMBOL> True
		{
            get { return new Predicate<SYMBOL>(PredicateType.NotIn, new Set<SYMBOL>()); }
		}

		public Predicate<SYMBOL> False
		{
            get {  return new Predicate<SYMBOL>(PredicateType.In, new Set<SYMBOL>()); }
		}

        public Predicate<SYMBOL> MkAnd(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
        {
            return p1 & p2;
        }

        public Predicate<SYMBOL> MkOr(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
        {
            return p1 | p2;
        }

        public Predicate<SYMBOL> MkAnd(params Predicate<SYMBOL>[] predicates)
        {
            return predicates.Aggregate(True, MkAnd);
        }

        public Predicate<SYMBOL> MkOr(params Predicate<SYMBOL>[] predicates)
        {
            return predicates.Aggregate(False, MkOr);
        }

        public Predicate<SYMBOL> MkAnd(IEnumerable<Predicate<SYMBOL>> predicates)
        {
            return predicates.Aggregate(True, MkAnd);
        }

        public Predicate<SYMBOL> MkOr(IEnumerable<Predicate<SYMBOL>> predicates)
        {
            return predicates.Aggregate(False, MkOr);
        }

        public Predicate<SYMBOL> MkNot(Predicate<SYMBOL> predicate)
        {
            return ~predicate;
        }

        public Predicate<SYMBOL> MkDiff(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
        {
            return p1 - p2;
        }

        public Predicate<SYMBOL> MkSymmetricDifference(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
        {
            return p1 ^ p2;
        }

		public bool IsSatisfiable(Predicate<SYMBOL> predicate)
		{
			if (predicate.Type == PredicateType.In) {
				return !((predicate.Set & Alphabet).IsEmpty);
			} else {
				return !((Alphabet - predicate.Set).IsEmpty);
			}
		}

		public bool AreEquivalent(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
		{
			if (p1.Type == p2.Type) {
				return (p1.Set == p2.Set);
			} else {
				return (p1.Set == Alphabet - p2.Set);
			}
		}

		public bool CheckImplication(Predicate<SYMBOL> p1, Predicate<SYMBOL> p2)
		{
			if (p1.Type == PredicateType.In) {
				if (p2.Type == PredicateType.In) {
					return (p1.Set <= p2.Set);
				} else {
					return (p1.Set <= Alphabet - p2.Set);
				}
			} else {
				if (p2.Type == PredicateType.In) {
					return (p1.Set >= Alphabet - p2.Set);
				} else {
					return (p1.Set >= p2.Set);
				}
			}
		}

		public bool IsExtensional
		{
			get { return false; }
		}

		public Predicate<SYMBOL> Simplify(Predicate<SYMBOL> predicate)
		{
			if (predicate.Set.Count > (Alphabet - predicate.Set).Count) {
				PredicateType type = (predicate.Type == PredicateType.In) ?
					PredicateType.NotIn : PredicateType.In;
				return new Predicate<SYMBOL>(type, Alphabet - predicate.Set);
			} else {
				return predicate;
			}
		}

		public bool IsAtomic
		{
			get { return true; }
		}

		public Predicate<SYMBOL> GetAtom(Predicate<SYMBOL> psi)
		{
			if (IsSatisfiable(psi)) {
				return new Predicate<SYMBOL>(psi.Type, new Set<SYMBOL>(Alphabet));
			} else {
				return False;
			}
		}

		public bool EvaluateAtom(Predicate<SYMBOL> atom, Predicate<SYMBOL> psi)
		{
			throw new NotImplementedException();
		}

		public IEnumerable<Tuple<bool[], Predicate<SYMBOL>>> GenerateMinterms(params Predicate<SYMBOL>[] constraints)
		{
			return mtg.GenerateMinterms(constraints);
		}

		public override bool Equals(object obj)
		{
			PredicateAlgebra<SYMBOL> algebra = obj as PredicateAlgebra<SYMBOL>;
			if (obj != null)
                return (this.Alphabet == algebra.Alphabet);
			return false;
		}

		public override int GetHashCode()
		{
			return Alphabet.GetHashCode();
		}

		public override string ToString()
		{
			return string.Format("[PredicateAlgebra: Alphabet={0}]", Alphabet);
		}
	}
}

