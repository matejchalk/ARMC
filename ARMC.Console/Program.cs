/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;
using System.Linq;
using NDesk.Options;
using System.IO;

namespace ARMC
{
    class MainClass
    {
        public static int Main(string[] args)
        {
            bool showHelp = false;
            bool generateConfig = false;
            string configFileName = null;
            string initFileName = null;
            string badFileName = null;
            string tauFileName = null;
            var opts = new OptionSet() {
                {"c|config=", "{PATH} to configuration file (default: armc.properties)", v => configFileName = v},
                {"i|init=", "{PATH} to automaton encoding initial configurations", v => initFileName = v},
                {"b|bad=", "{PATH} to automaton encoding bad configurations", v => badFileName = v},
                {"t|tau=", "{PATH} to transducer encoding transition(s)", v => tauFileName = v},
                {"g|generate-config", "generate configuration file and exit", v => generateConfig = (v != null)},
                {"h|help", "show this message and exit", v => showHelp = (v != null)}
            };

            try {
                opts.Parse(args);
            } catch (OptionException exc) {
                Console.Error.WriteLine("Error - {0}", exc.Message);
                Console.Error.WriteLine("Try `--help` for more information.");
                return 1;
            }

            if (showHelp) {
                opts.WriteOptionDescriptions(Console.Out);
                return 0;
            }

            if (generateConfig) {
                new Config().Write("armc.properties");
                return 0;
            }

            try {
                Config config = new Config(configFileName ?? "armc.properties");
                config.InitFilePath = initFileName ?? config.InitFilePath;
                config.BadFilePath = badFileName ?? config.BadFilePath;
                if (tauFileName != null)
                    config.TauFilePaths = new string[] { tauFileName };
                if (config.InitFilePath == "" || config.BadFilePath == "" || config.TauFilePaths.Any(fp => fp == "")) {
                    Console.Error.WriteLine("Error - missing automata or transducer(s)");
                    return 1;
                }

                var armc = new ARMC<string>(config);

                Counterexample<string> counterexample;
                bool verified = armc.Verify(out counterexample);

                if (verified) {
                    Console.WriteLine("Property holds.");
                } else {
                    string ceDir = Path.Combine(config.OutputDirectory, "armc-counterexample");
                    Directory.CreateDirectory(ceDir);
                    armc.PrintCounterexample(counterexample, ceDir);
                    Console.WriteLine("Property does not hold (see {0}{1}).", ceDir, Path.DirectorySeparatorChar);
                }
            } catch (Exception exc) {
                Console.Error.WriteLine("Error - {0}", exc.Message);
                return 1;
            }

            return 0;
		}
	}
}
