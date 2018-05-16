# ARMC

This tool implements the *abstract regular model checking* formal verification technique.
It may be used as a command-line tool, as well as a C# library.
System configurations and transitions between them are represented using *simple symbolic automata* and *simple symbolic transducers*, respectively.

## Build

The solution file *ARMC.sln* at the project root may be built and run using the [Microsoft Visual Studio](https://www.visualstudio.com/) (Windows-only) or [MonoDevelop](https://www.monodevelop.com/) (multi-platform) IDEs.
Alternatively, build from the command-line using [MSBuild](https://github.com/Microsoft/msbuild), and execute *ARMC.Console.exe* either directly (Windows-only) or using [Mono](https://www.mono-project.com/) (multi-platform).

## Usage

To run ARMC from the command-line, first generate a configuration file (called *armc.properties* by default) by specifying the `-g`/`--generate-config` parameter.
The file uses a simple *key=value* format and contains helpful comments.
Choose your settings and enter the paths to the automata and transducers which represent your verification task (or specify these using the command-line).
[Timbuk](http://people.irisa.fr/Thomas.Genet/timbuk/), [FSA](http://www.let.rug.nl/~vannoord/Fsa/) and [FSM](http://web.eecs.umich.edu/~radev/NLP-fall2015/resources/fsm_archive/fsm.5.html) file formats are supported, as well as [DOT](https://www.graphviz.org/) for printing.

You may then run ARMC using your configuration.
The result of the verification is printed, along with computation progress and/or generated automata if so selected.
Termination is not guaranteed, so you have the option of specifying a time limit in the configuration file.
