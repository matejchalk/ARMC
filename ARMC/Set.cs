/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System.Collections.Generic;
using System.Linq;

namespace ARMC
{
    /// <summary>
    /// Set class (enables operator use).
    /// </summary>
	public class Set<T> : HashSet<T>
	{
		public bool IsEmpty
		{
			get { return (this.Count == 0); }
		}

		public Set()
			: base()
		{
		}

		public Set(Set<T> s)
			: base(s)
		{
		}

		public Set(IEnumerable<T> e)
			: base(e)
		{
		}

		public static Set<T> Union(Set<T> s1, Set<T> s2)
		{
			Set<T> res = new Set<T>(s1);
			res.UnionWith(s2);
			return res;
		}

		public static Set<T> Intersection(Set<T> s1, Set<T> s2)
		{
			Set<T> res = new Set<T>(s1);
			res.IntersectWith(s2);
			return res;
		}

		public static Set<T> Subtraction(Set<T> s1, Set<T> s2)
		{
			Set<T> res = new Set<T>(s1);
			res.ExceptWith(s2);
			return res;
		}

		public static Set<T> SymmetricDifference(Set<T> s1, Set<T> s2)
		{
			Set<T> res = new Set<T>(s1);
			res.SymmetricExceptWith(s2);
			return res;
		}

        public static Set<T> operator+(Set<T> s1, Set<T> s2)
        {
            return Union(s1, s2);
        }

        public static Set<T> operator*(Set<T> s1, Set<T> s2)
        {
            return Intersection(s1, s2);
        }

        public static Set<T> operator-(Set<T> s1, Set<T> s2)
        {
            return Subtraction(s1, s2);
        }

        public static Set<T> operator|(Set<T> s1, Set<T> s2)
        {
            return Union(s1, s2);
        }

        public static Set<T> operator&(Set<T> s1, Set<T> s2)
        {
            return Intersection(s1, s2);
        }

        public static Set<T> operator^(Set<T> s1, Set<T> s2)
        {
            return SymmetricDifference(s1, s2);
        }

		public static bool operator==(Set<T> s1, Set<T> s2)
		{
			if (object.ReferenceEquals(s1, null) || object.ReferenceEquals(s2, null))
				return (object.ReferenceEquals(s1, null) && object.ReferenceEquals(s2, null));
			return s1.SetEquals(s2);
		}

		public static bool operator!=(Set<T> s1, Set<T> s2)
		{
			if (object.ReferenceEquals(s1, null) || object.ReferenceEquals(s2, null))
				return !(object.ReferenceEquals(s1, null) && object.ReferenceEquals(s2, null));
			return !(s1.SetEquals(s2));
		}

        public static bool operator<=(Set<T> s1, Set<T> s2)
        {
            return s1.IsSubsetOf(s2);
        }

        public static bool operator>=(Set<T> s1, Set<T> s2)
        {
            return s1.IsSupersetOf(s2);
        }

        public static bool operator<(Set<T> s1, Set<T> s2)
        {
            return s1.IsProperSubsetOf(s2);
        }

        public static bool operator>(Set<T> s1, Set<T> s2)
        {
            return s1.IsProperSupersetOf(s2);
        }

        public override bool Equals(object obj)
        {
            Set<T> set = obj as Set<T>;
            if (set == null)
                return false;
            return this == set;
        }

		public override int GetHashCode()
        {
            return this.Aggregate(0, (acc, elem) => acc ^ elem.GetHashCode());
        }

		public override string ToString()
		{
			return string.Format("{0}{1}{2}", '{', string.Join(",", this), '}');
		}
	}

    public class SetEqualityComparer<T> : EqualityComparer<Set<T>>
    {
        public override bool Equals(Set<T> s1, Set<T> s2)
        {
            return (s1 == s2);
        }

        public override int GetHashCode(Set<T> s)
        {
            return s.Aggregate(0, (hc, elem) => hc ^ elem.GetHashCode());
        }
    }
}

