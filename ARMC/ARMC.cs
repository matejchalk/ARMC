/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace ARMC
{
    /// <summary>
    /// Abstract regular model checking.
    /// </summary>
	public class ARMC<SYMBOL>
	{
        private readonly SSA<SYMBOL> init;
        private readonly SSA<SYMBOL> bad;
        private readonly SST<SYMBOL> tau;
        private readonly SST<SYMBOL> tauInv;
        private readonly Abstraction<SYMBOL> abstr;
        private readonly bool verbose;
        private readonly bool printAutomata;
        private readonly string outputDir;
        private readonly PrintFormat format;
        private readonly string imgExt;
        private readonly long timeout;
        private readonly Stopwatch stopwatch;
        private int loops;

        /// <summary>
        /// Elapsed time.
        /// </summary>
        public TimeSpan ElapsedTime
        {
            get { return stopwatch.Elapsed; }
        }

        /// <summary>
        /// Number of refinements.
        /// </summary>
        public int Refinements
        {
            get { return loops; }
        }

        /// <summary>
        /// ARMC constructor.
        /// </summary>
        /// <param name="config">Configuration.</param>
        public ARMC(Config config)
            : this(SSA<SYMBOL>.Parse(config.InitFilePath),
                   SSA<SYMBOL>.Parse(config.BadFilePath),
                   config.TauFilePaths.Select(path => SST<SYMBOL>.Parse(path)).ToArray(),
                   config)
        {
        }

        /// <summary>
        /// ARMC constructor.
        /// </summary>
        /// <param name="configFileName">Configuration file path.</param>
        public ARMC(string configFileName)
            : this(new Config(configFileName))
        {
        }

        /// <summary>
        /// ARMC constructor.
        /// </summary>
        /// <param name="init">SSA representing initial states.</param>
        /// <param name="bad">SSA representing bad states.</param>
        /// <param name="tau">SST representing transition.</param>
        public ARMC(SSA<SYMBOL> init, SSA<SYMBOL> bad, SST<SYMBOL> tau)
            : this(init, bad, new SST<SYMBOL>[] { tau }, new Config())
        {
        }

        /// <summary>
        /// ARMC constructor.
        /// </summary>
        /// <param name="init">SSA representing initial states.</param>
        /// <param name="bad">SSA representing bad states.</param>
        /// <param name="tau">SST representing transition.</param>
        /// <param name="configFileName">Configuration file path.</param>
        public ARMC(SSA<SYMBOL> init, SSA<SYMBOL> bad, SST<SYMBOL> tau, string configFileName)
            : this(init, bad, new SST<SYMBOL>[] { tau }, new Config(configFileName))
        {
        }

        /// <summary>
        /// ARMC constructor.
        /// </summary>
        /// <param name="init">SSA representing initial states.</param>
        /// <param name="bad">SSA representing bad states.</param>
        /// <param name="tau">SST representing transition.</param>
        /// <param name="config">Configuration.</param>
        public ARMC(SSA<SYMBOL> init, SSA<SYMBOL> bad, SST<SYMBOL> tau, Config config)
            : this(init, bad, new SST<SYMBOL>[] { tau }, config)
        {
        }

        /// <summary>
        /// ARMC constructor.
        /// </summary>
        /// <param name="init">SSA representing initial states.</param>
        /// <param name="bad">SSA representing bad states.</param>
        /// <param name="taus">SSTs whose composition represents transition.</param>
        public ARMC(SSA<SYMBOL> init, SSA<SYMBOL> bad, SST<SYMBOL>[] taus)
            : this(init, bad, taus, new Config())
        {
        }

        /// <summary>
        /// ARMC constructor.
        /// </summary>
        /// <param name="init">SSA representing initial states.</param>
        /// <param name="bad">SSA representing bad states.</param>
        /// <param name="taus">SSTs whose composition represents transition.</param>
        /// <param name="configFileName">Configuration file path.</param>
        public ARMC(SSA<SYMBOL> init, SSA<SYMBOL> bad, SST<SYMBOL>[] taus, string configFileName)
            : this(init, bad, taus, new Config(configFileName))
        {
        }

        /// <summary>
        /// ARMC constructor.
        /// </summary>
        /// <param name="init">SSA representing initial states.</param>
        /// <param name="bad">SSA representing bad states.</param>
        /// <param name="taus">SSTs whose composition represents transition.</param>
        /// <param name="config">Configuration.</param>
        public ARMC(SSA<SYMBOL> init, SSA<SYMBOL> bad, SST<SYMBOL>[] taus, Config config)
        {

            /* merge alphabets */
            Set<SYMBOL> alphabet = init.Alphabet + bad.Alphabet + taus.Select(tau => tau.Alphabet).Aggregate(Set<SYMBOL>.Union);
            init.Alphabet = alphabet;
            bad.Alphabet = alphabet;
            for (int i = 0; i < taus.Length; i++)
                taus[i].Alphabet = alphabet;

            if (config.PredicateLanguages == config.FiniteLengthLanguages)  // sanity check
                throw ConfigException.AbstractionNotChosen();

            this.init = init;
            this.bad = bad;
            this.tau = SST<SYMBOL>.Union(taus);
            this.tauInv = tau.Invert();
            this.abstr = config.PredicateLanguages ?
                (Abstraction<SYMBOL>)new PredicateAbstraction<SYMBOL>(config, init, bad, taus) : 
                new FiniteLengthAbstraction<SYMBOL>(config, init, bad, taus);
            this.verbose = config.Verbose;
            this.printAutomata = config.PrintAutomata;
            this.outputDir = config.OutputDirectory;
            this.format = config.AutomataFormat;
            this.imgExt = (config.ImageFormat == null) ? "" : config.ImageFormat.ToString();
            this.timeout = config.Timeout.Ticks;
            this.stopwatch = new Stopwatch();
            this.loops = 0;

            if (outputDir != "") {
                /* clear output directory */
                DirectoryInfo dirInfo = Directory.CreateDirectory(outputDir);
                foreach (FileInfo fi in dirInfo.EnumerateFiles())
                    fi.Delete();
                foreach (DirectoryInfo di in dirInfo.EnumerateDirectories())
                    di.Delete(true);
            }

            if (printAutomata) {
                /* print input automata and configuration */
                string dir = Path.Combine(outputDir, "armc-input");
                Directory.CreateDirectory(dir);
                PrintAutomaton(init, dir, "init");
                PrintAutomaton(bad, dir, "bad");
                PrintAutomaton(tau, dir, "tau");
                if (taus.Length > 1)  // no point in only printing tau1
                    for (int i = 0; i < taus.Length; i++)
                        PrintAutomaton(taus[i], dir, "tau" + (i+1).ToString());
                PrintAutomaton(tauInv, dir, "tau-inv");
                config.Write(Path.Combine(dir, "armc.properties"));
            }

            if (config.ComputationDirection == Config.Direction.Backward) {
                /* reverse direction, i.e. check if tauInv*(bad) & init is empty */
                this.init = bad;
                this.bad = init;
                this.tauInv = tau;
                this.tau = tau.Invert();
            }

            if (!SSA<SYMBOL>.ProductIsEmpty(init, bad)) {  // no point in further verification
                if (printAutomata) {
                    string dir = Path.Combine(outputDir, "armc-counterexample");
                    Directory.CreateDirectory(dir);
                    PrintAutomaton(init & bad, dir, "initXbad");
                }
                throw ARMCException.InitialPropertyViolation();
            }
        }

        /// <summary>
        /// Performs the entire ARMC verification algorithm.
        /// </summary>
        /// <returns>Verification result, i.e. whether property holds.</returns>
        /// <param name="counterexample">Counterexample (if encountered, i.e. <code>false</code> returned).</param>
        /// <remarks>Termination not guaranteed, consider setting timeout in configuration.</remarks>
        public bool Verify(out Counterexample<SYMBOL> counterexample)
        {
            stopwatch.Reset();

            while (true) {
                bool? result = VerifyStep(out counterexample);
                if (result.HasValue)
                    return (bool)result;
                if (timeout > 0 && stopwatch.ElapsedTicks > timeout)
                    throw ARMCException.Timeout();
                loops++;
            }
        }

        /// <summary>
        /// Performs one step (inner loop) of ARMC.
        /// </summary>
        /// <returns>Verification result, or <code>null</code> if undecided.</returns>
        /// <param name="counterexample">Counterexample (if encountered, i.e. <code>false</code> returned).</param>
        public bool? VerifyStep(out Counterexample<SYMBOL> counterexample)
        {
            var ssas = new Stack<Tuple<SSA<SYMBOL>,SSA<SYMBOL>>>();
            SSA<SYMBOL> m;
            SSA<SYMBOL> ml;
            SSA<SYMBOL> mAlpha = null;
            SSA<SYMBOL> x;
            var xs = new Stack<SSA<SYMBOL>>();
            int i = 0;
            int l = 0;
            string ext = "." + format.ToString().ToLower();
            string dir = Path.Combine(outputDir, "armc-loop-" + loops.ToString());

            stopwatch.Start();

            if (printAutomata) {
                Directory.CreateDirectory(dir);
                abstr.Print(dir, this);
            }

            counterexample = null;

            m = init;
            if (printAutomata)
                PrintAutomaton(m, dir, "M0");

            while (true) {
                if (verbose)
                    Log("\r" + loops.ToString() + "." + i.ToString());
                
                if (i > 0 && !SSA<SYMBOL>.ProductIsEmpty(m, bad)) {
                    if (verbose)
                        LogLine(": counterexample encountered");
                    l = i;
                    x = m & bad;
                    x.Name = "<i>X</i><sub>" + l.ToString() + "</sub>";
                    if (printAutomata)
                        PrintAutomaton(x, dir, "X" + l.ToString());
                    ml = m;
                    xs.Push(x);
                    break;
                }

                mAlpha = abstr.Collapse(m).Determinize().Minimize();
                mAlpha.Name = "<i>M</i><sub>" + i.ToString() + "</sub><sup>&alpha;</sup>";
                if (printAutomata)
                    PrintAutomaton(mAlpha, dir, "M" + i.ToString() + "+");

                if (i > 0 && mAlpha == ssas.Peek().Item2) {
                    stopwatch.Stop();
                    if (verbose) {
                        LogLine(": fixpoint reached");
                        LogLine("time = " + stopwatch.Elapsed.ToString());
                    }
                    return true;
                }

                if (timeout > 0 && stopwatch.ElapsedTicks > timeout) {
                    stopwatch.Stop();
                    if (verbose)
                        LogLine(": timeout (" + stopwatch.Elapsed.ToString() + ")");
                    throw ARMCException.Timeout();
                }

                ssas.Push(Tuple.Create(m, mAlpha));
                i++;

                m = tau.Apply(mAlpha).Determinize().Minimize();
                m.Name = "<i>M</i><sub>" + i.ToString() + "</sub>";
                if (printAutomata)
                    PrintAutomaton(m, dir, "M" + i.ToString());
            }

            bool spurious = false;

            foreach (var pair in ssas) {
                m = pair.Item1;
                mAlpha = pair.Item2;

                i--;

                if (verbose)
                    Log("\r" + loops.ToString() + "." + l.ToString() + "-" + i.ToString());
                
                x = (tauInv.Apply(x) & mAlpha).Determinize().Minimize();
                x.Name = "<i>X</i><sub>" + i.ToString() + "</sub>";
                xs.Push(x);
                if (printAutomata)
                    PrintAutomaton(x, dir, "X" + i.ToString());

                if (SSA<SYMBOL>.ProductIsEmpty(x, m)) {
                    spurious = true;
                    break;
                }
            }

            stopwatch.Stop();

            if (spurious) {
                if (verbose)
                    LogLine(": counterexample is spurious");
                abstr.Refine(m, x);
                return null;
            } else {
                if (verbose) {
                    LogLine(": counterexample is real");
                    LogLine("time = " + stopwatch.Elapsed.ToString());
                }
                List<Tuple<SSA<SYMBOL>,SSA<SYMBOL>>> ms = ssas.ToList();
                ms.Reverse();
                ms.Add(new Tuple<SSA<SYMBOL>,SSA<SYMBOL>>(ml, null));
                counterexample = new Counterexample<SYMBOL> {
                    Ms = ms,
                    Xs = xs.ToList()
                };
                return false;
            }
        }

        /// <summary>
        /// Print the counterexample (SSAs).
        /// </summary>
        /// <param name="counterexample">Counterexample.</param>
        /// <param name="directory">Output directory path.</param>
        public void PrintCounterexample(Counterexample<SYMBOL> counterexample, string directory)
        {
            int i = 0;
            foreach (var pair in counterexample.Ms) {
                SSA<SYMBOL> m = pair.Item1;
                SSA<SYMBOL> mAlpha = pair.Item2;
                PrintAutomaton(m, directory, "M" + i.ToString());
                if (mAlpha != null)
                    PrintAutomaton(mAlpha, directory, "M" + i.ToString() + "+");
                i++;
            }
            i = 0;
            foreach (SSA<SYMBOL> x in counterexample.Xs) {
                PrintAutomaton(x, directory, "X" + i.ToString());
                i++;
            }
        }

        private void Log(string text)
        {
            stopwatch.Stop();
            Console.Write(text);
            stopwatch.Start();
        }

        private void LogLine(string line)
        {
            stopwatch.Stop();
            Console.WriteLine(line);
            stopwatch.Start();
        }

        /* prints automaton, including optional image (temporarily pauses timer) */
        internal void PrintAutomaton(ISSAutomaton<SYMBOL> aut, string dir, string fileName, bool normalize = true)
        {
            stopwatch.Stop();

            string ext;
            switch (format) {
            case PrintFormat.TIMBUK:
                ext = "tmb";
                break;
            case PrintFormat.FSA:
                ext = "pl";
                break;
            default:
                ext = format.ToString().ToLower();
                break;
            }

            string path = Path.Combine(dir, fileName);
            string textPath = path + "." + ext;

            if (normalize)
                aut = aut.GenericNormalize();

            Printer<SYMBOL>.PrintAutomaton(textPath, aut, format);

            if (imgExt != "") {  // print image by executing DOT process
                string imgPath = path + "." + imgExt;
                var process = new Process();
                process.StartInfo.FileName = "dot";
                if (format == PrintFormat.DOT) {  // already created DOT file, just read from there
                    process.StartInfo.Arguments = string.Format("-T{0} {1} -o {2}", imgExt, textPath, imgPath);
                    process.Start();
                } else {  // print automaton as DOT and pipe to process' standard input
                    process.StartInfo.Arguments = string.Format("-T{0} -o {1}", imgExt, imgPath);
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardInput = true;
                    process.Start();
                    StreamWriter stdin = process.StandardInput;
                    Printer<SYMBOL>.PrintAutomaton(stdin, aut, PrintFormat.DOT);
                    stdin.Close();
                }
            }

            stopwatch.Start();
        }
	}

    public class Counterexample<SYMBOL>
    {
        public List<Tuple<SSA<SYMBOL>,SSA<SYMBOL>>> Ms;
        public List<SSA<SYMBOL>> Xs;
    }
}
