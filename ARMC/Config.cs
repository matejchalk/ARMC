/*
 * Copyright (c) 2018 Matej Chalk <xchalk00@stud.fit.vutbr.cz>. All rights reserved.
 * Licensed under the MIT License. See LICENSE.txt file in the project root for full license information.
 */

using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ARMC
{
    /// <summary>
    /// ARMC configuration class.
    /// </summary>
    public class Config
    {
        public enum PropertyGroup { General, PredicateAbstr, LengthAbstr }
        public enum Direction { Forward, Backward }
        public enum InitPred { Init, Bad, Both }
        public enum InitBound { One, Init, Bad }
        public enum BoundInc { One, X, M }
        public enum ImgFormat { gif, jpg, pdf, png, svg }
        public enum PredHeuristic { ImportantStates, KeyStates }

        private static Dictionary<PropertyGroup,string[]> groupDescription = new Dictionary<PropertyGroup,string[]>{
            [PropertyGroup.General] = new string[] {
                "###################################################",
                "## Configuration file for ARMC                   ##",
                "## Settings are in the format 'name=value'       ##",
                "## Lines beginning with '#' are comments         ##",
                "## Blank lines and spaces around '=' are ignored ##",
                "###################################################"
            },
            [PropertyGroup.PredicateAbstr] = new string[] {
                "#########################################################",
                "# Settings for abstraction based on predicate languages #",
                "# Ignored if PREDICATE_LANGUAGES set to NO              #",
                "#########################################################"
            },
            [PropertyGroup.LengthAbstr] = new string[] {
                "#############################################################",
                "# Settings for abstraction based on finite-length languages #",
                "# Ignored if FINITE_LENGTH_LANGUAGES set to NO              #",
                "#############################################################"
            }
        };

        [Group(PropertyGroup.General)]
        [Description("Path to SSA encoding initial configurations (Init)")]
        public string InitFilePath { get; set; }

        [Group(PropertyGroup.General)]
        [Description("Path to SSA encoding bad configurations (Bad)")]
        public string BadFilePath { get; set; }

        [Group(PropertyGroup.General)]
        [Description("Path(s) to SST(s) encoding transition(s) (Tau = Tau1 U ... U TauN)")]
        public string[] TauFilePaths { get; set; }

        [Group(PropertyGroup.General)]
        [Description("Direction of computation")]
        public Direction ComputationDirection { get; set; }

        [Group(PropertyGroup.General)]
        [Description("Direction of state (or trace) languages")]
        public Direction LanguageDirection { get; set; }

        [Group(PropertyGroup.General)]
        [Description("Time limit upon which ARMC will end with \"don't know\" (disabled if zero)",
                     "Use [d.]hh:mm:ss[.fffffff] format")]
        public TimeSpan Timeout { get; set; }

        [Group(PropertyGroup.General)]
        [Description("Print computation progress to standard output?")]
        public bool Verbose { get; set; }

        [Group(PropertyGroup.General)]
        [Description("Print created automata to files?")]
        public bool PrintAutomata { get; set; }

        [Group(PropertyGroup.General)]
        [Description("File format for printing automata (text)",
                     "Ignored if PRINT_AUTOMATA set to NO")]
        public PrintFormat AutomataFormat { get; set; }

        [Group(PropertyGroup.General)]
        [Description("Path to directory (current if blank) where automata files will be placed",
                     "Ignored if PRINT_AUTOMATA set to NO")]
        public string OutputDirectory { get; set; }

        [Group(PropertyGroup.General)]
        [Description("File format for printing automata as image",
                     "Requires that `dot` be in PATH, ignored if PRINT_AUTOMATA set to NO")]
        public ImgFormat? ImageFormat { get; set; }

        [Group(PropertyGroup.PredicateAbstr)]
        [Description("Enable abstraction based on predicate languages?")]
        public bool PredicateLanguages { get; set; }

        [Group(PropertyGroup.PredicateAbstr)]
        [Description("Automaton/a used to initialize set of predicate languages")]
        public InitPred InitialPredicate { get; set; }

        [Group(PropertyGroup.PredicateAbstr)]
        [Description("Include transducer domain in initial set of predicates?")]
        public bool IncludeGuard { get; set; }

        [Group(PropertyGroup.PredicateAbstr)]
        [Description("Include transducer range in initial set of predicates?")]
        public bool IncludeAction { get; set; }

        [Group(PropertyGroup.PredicateAbstr)]
        [Description("Heuristic to use for abstraction refinement")]
        public PredHeuristic? Heuristic { get; set; }

        [Group(PropertyGroup.LengthAbstr)]
        [Description("Enable abstraction based on finite-length languages?")]
        public bool FiniteLengthLanguages { get; set; }

        [Group(PropertyGroup.LengthAbstr)]
        [Description("Use trace languages?")]
        public bool TraceLanguages { get; set; }

        [Group(PropertyGroup.LengthAbstr)]
        [Description("Value of initial bound (either 1 or number of states in chosen automaton)")]
        public InitBound InitialBound { get; set; }

        [Group(PropertyGroup.LengthAbstr)]
        [Description("Set initial bound equal to half the number of automata states?",
                     "Ignored if INITIAL_BOUND set to One")]
        public bool HalveInitialBound { get; set; }

        [Group(PropertyGroup.LengthAbstr)]
        [Description("Value of bound increment (either 1 or number of states in chosen automaton)")]
        public BoundInc BoundIncrement { get; set; }

        [Group(PropertyGroup.LengthAbstr)]
        [Description("Increment bound by half the number of automata states?",
                     "Ignored if BOUND_INCREMENT set to One")]
        public bool HalveBoundIncrement { get; set; }

        /// <summary>
        /// Constructs default configuration (without automata/transducers).
        /// </summary>
        public Config()
        {
            InitFilePath = "";
            BadFilePath = "";
            TauFilePaths = new string[] {""};

            ComputationDirection = Direction.Forward;
            LanguageDirection = Direction.Forward;

            Timeout = new TimeSpan();

            Verbose = false;
            PrintAutomata = false;
            OutputDirectory = "armc-output";
            AutomataFormat = PrintFormat.DOT;
            ImageFormat = null;

            PredicateLanguages = true;
            InitialPredicate = InitPred.Bad;
            IncludeGuard = false;
            IncludeAction = false;

            FiniteLengthLanguages = false;
            TraceLanguages = false;
            InitialBound = InitBound.One;
            HalveInitialBound = false;
            BoundIncrement = BoundInc.M;
            HalveBoundIncrement = false;
        }

        /// <summary>
        /// Constructs configuration from file.
        /// </summary>
        /// <param name="fileName">File path.</param>
        public Config(string fileName)
        {
            PropertyInfo[] properties = this.GetType().GetProperties();
            var regex = new Regex(@"^(\w+)\s*=\s*([^\s]*)$");

            var propFound = new Dictionary<string,bool>(properties.Length);
            foreach (var prop in properties)
                propFound[PropertyToFile(prop.Name)] = false;

            foreach (string line in File.ReadLines(fileName)) {
                if (line == "" || line[0] == '#')
                    continue;

                Match match = regex.Match(line);
                if (!match.Success)
                    throw ConfigException.BadFileFormat();
                string name = match.Groups[1].Value;
                string value = match.Groups[2].Value;

                if (!propFound.ContainsKey(name))
                    throw ConfigException.UnknownProperty(name);
                if (propFound[name])
                    throw ConfigException.DuplicateProperty(name);
                propFound[name] = true;

                PropertyInfo property = Array.Find(properties, prop => prop.Name == PropertyFromFile(name));
                Type type = property.PropertyType;
                if (type == typeof(string)) {
                    property.SetValue(this, value);
                } else if (type == typeof(string[])) {
                    property.SetValue(this, value.Split(Path.PathSeparator));
                } else if (type == typeof(bool)) {
                    if (value == "YES") {
                        property.SetValue(this, true);
                    } else if (value == "NO") {
                        property.SetValue(this, false);
                    } else {
                        throw ConfigException.BadPropertyValue(name);
                    }
                } else if (type == typeof(TimeSpan)) {
                    try {
                        property.SetValue(this, TimeSpan.Parse(value));
                    } catch (FormatException) {
                        throw ConfigException.BadPropertyValue(name);
                    }
                } else {
                    Type utype = Nullable.GetUnderlyingType(type);
                    if (utype != null) {
                        if (value == "") {
                            property.SetValue(this, null);
                            continue;
                        }
                        type = utype;
                    }
                    if (!Enum.GetNames(type).Contains(value))
                        throw ConfigException.BadPropertyValue(name);
                    property.SetValue(this, Enum.Parse(type, value));
                }
            }

            foreach (KeyValuePair<string,bool> pair in propFound)
                if (!pair.Value)
                    throw ConfigException.MissingProperty(pair.Key);

            if (PredicateLanguages == FiniteLengthLanguages)
                throw ConfigException.AbstractionNotChosen();
        }

        /// <summary>
        /// Saves configuration to a file.
        /// </summary>
        /// <param name="fileName">File path.</param>
        public void Write(string fileName)
        {
            PropertyInfo[] properties = this.GetType().GetProperties();
            var groups = properties.GroupBy(prop => ((GroupAttribute)prop.GetCustomAttribute(typeof(GroupAttribute))).Group);

            using (StreamWriter file = new StreamWriter(fileName)) {
                foreach (IGrouping<PropertyGroup,PropertyInfo> group in groups) {
                    foreach (string line in groupDescription[group.Key])
                        file.WriteLine(line);
                    file.WriteLine();

                    foreach (PropertyInfo property in group) {
                        string name = property.Name;
                        Type type = property.PropertyType;
                        var desc = (DescriptionAttribute)property.GetCustomAttribute(typeof(DescriptionAttribute));

                        if (type == typeof(bool))
                            file.WriteLine("# {0} (YES or NO)", desc.Description);
                        else
                            file.WriteLine("# {0}", desc.Description);

                        if (desc.Note != null)
                            file.WriteLine("# {0}", desc.Note);

                        string value;
                        if (type == typeof(string)) {
                            value = (string)property.GetValue(this);
                        } else if (type == typeof(string[])) {
                            value = string.Join(":", (string[])property.GetValue(this));
                            file.WriteLine("# Multiple values are separated by '{0}'", Path.PathSeparator);
                        } else if (type == typeof(bool)) {
                            value = (bool)property.GetValue(this) ? "YES" : "NO";
                        } else if (type == typeof(TimeSpan)) {
                            value = ((TimeSpan)property.GetValue(this)).ToString();
                        } else {
                            Type utype = Nullable.GetUnderlyingType(type);
                            if (utype != null)
                                type = utype;
                            file.WriteLine(
                                "# Possible values: {0}{1}",
                                string.Join(", ", Enum.GetNames(type)),
                                (utype != null) ? " (or leave blank to disable)" : ""
                            );
                            object obj = property.GetValue(this);
                            value = (obj == null) ? "" : property.GetValue(this).ToString();
                        }

                        file.WriteLine("{0} = {1}", PropertyToFile(name), value);
                        file.WriteLine();
                    }
                }
            }
        }

        /* convert "PROPERTY_NAME" to "PropertyName" */
        private static string PropertyFromFile(string name)
        {
            var sb = new StringBuilder();
            bool firstLetter = true;
            foreach (char c in name) {
                if (c == '_') {
                    firstLetter = true;
                } else {
                    sb.Append(firstLetter ? c : char.ToLower(c));
                    firstLetter = false;
                }
            }
            return sb.ToString();
        }

        /* convert "PropertyName" to "PROPERTY_NAME" */
        private static string PropertyToFile(string name)
        {
            var sb = new StringBuilder();
            bool firstLetter = true;
            foreach (char c in name) {
                if (char.IsUpper(c)) {
                    if (firstLetter) {
                        firstLetter = false;
                    } else {
                        sb.Append('_');
                    }
                }
                sb.Append(char.ToUpper(c));
            }
            return sb.ToString();
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class DescriptionAttribute : Attribute
    {
        public string Description { get; }
        public string Note { get; }

        public DescriptionAttribute(string description, string note = null)
        {
            this.Description = description;
            this.Note = note;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class GroupAttribute : Attribute
    {
        public Config.PropertyGroup Group { get; }

        public GroupAttribute(Config.PropertyGroup group)
        {
            this.Group = group;
        }
    }
}

