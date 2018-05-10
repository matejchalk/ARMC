/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

namespace ARMC
{
    /// <summary>
    /// Interface for automata/transducer labels.
    /// </summary>
	public interface ILabel<SYMBOL>
	{
        /// <summary>
        /// Set of symbols contained in label.
        /// </summary>
		Set<SYMBOL> Symbols { get; }
	}
}

