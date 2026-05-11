# DSDStudio Tutorials

This document introduces the `.dsd` model syntax supported by DSDStudio and provides step-by-step verification examples. The syntax is a practical Visual DSD-style subset implemented by the current parser and interpreter. The goal is to help users compile DSD source code, inspect generated reaction networks and ODEs, run deterministic simulations, and reproduce the basic examples included with the repository.

## 1. Workflow Overview

A DSDStudio model follows this pipeline:

1. Source text is tokenized by `Lexer/Lexer.cs`.
2. Tokens are parsed into AST nodes by `Lexer/Parser.cs` and the node classes in `Lexer/AST.cs`.
3. `Lexer/Interpreter.cs` expands definitions, initializes species, and generates DNA strand displacement reactions.
4. The generated reaction network is converted into a mass-action ODE system by `ODE/Ode.cs`.
5. Deterministic simulation is performed through the solver module.
6. The IDE service layer in `IDE/Services/DSDCoreService.cs` exposes compilation, simulation, reaction SVG generation, ODE display, table output, and export functions to the WPF interface.

The common user workflow is:

```text
write or open .dsd model -> compile -> inspect reactions/ODEs -> simulate -> export results
```

## 2. Five-Minute GUI Tutorial

Use this section to verify that the pre-built package or source build is working.

1. Start DSDStudio.
2. Open `examples/AND gate.dsd`.
3. Click `Compile`.
4. Confirm that the reaction view and ODE view are populated.
5. Click `Run Simulation`.
6. Confirm that the concentration table and trajectory plot are populated.
7. Export one result, such as the reaction SVG or trajectory plot, to confirm the export path.

If this workflow succeeds, the core parser, interpreter, CRN generator, ODE builder, deterministic solver, visualization, and export paths are functioning.

## 3. Minimal Model

A small hybridization model looks like this:

```dsd
directive simulation {initial=0; final=50000;}
directive simulator deterministic

def N1 = 1.0
def N2 = 1.0

def Input1() = <a^ b^ c>
def Input2() = {d a^* b^*}

( N1 * Input1()
| N2 * Input2())
```

This example is available as `examples/DNA hybridization.dsd`.

The model contains four parts:

- A simulation directive defining the start and final times.
- A simulator directive indicating deterministic simulation.
- Numeric and species definitions.
- An initial process expression that gives initial concentrations.

## 4. Comments

The lexer supports single-line comments:

```dsd
// this is a comment
```

It also supports block comments using `(* ... *)`. The implementation keeps track of nested block comments in `Lexer/Lexer.cs`.

```dsd
(*
  block comment
*)
```

## 5. Directives

Directives start with `directive`. The current deterministic solving path uses the simulation time settings documented below.

```dsd
directive simulation {initial=0; final=50000;}
directive simulator deterministic
```

Runtime-effective simulation fields:

| Field | Example | Effect |
| --- | --- | --- |
| `initial` | `initial=0` | Start time passed to the ODE solver. |
| `final` | `final=50000` | End time passed to the ODE solver. |
| `points` | `points=1000` | Number of output intervals used to compute the solver step size, when specified. |

Example:

```dsd
directive simulation {initial=0; final=50000; points=1000;}
directive simulator deterministic
```

## 6. Numeric Definitions

Use `def` to define numeric constants:

```dsd
def N1 = 1.0
def GateAmount = 10.0
```

These are parsed by `Parser.ParseDef()` and stored as `DefNode` values. Numeric definitions can be used in initial process expressions:

```dsd
( N1 * Input1()
| GateAmount * Gate())
```

## 7. Species Definitions

Use `def` with a zero-argument function form to name a species template:

```dsd
def Input1() = <a^ b>
def Output() = <b c>
def Gate() = {a^*}[b c]{d^*}
```

The parser represents these definitions as function-style AST nodes. During interpretation, function calls such as `Input1()` are expanded before reaction generation.

## 8. Domains and Domain Modifiers

A domain is written as an identifier inside a strand. The parser supports the following modifiers:

| Syntax | Meaning | AST representation |
| --- | --- | --- |
| `a` | Normal domain | `DomType.Normal` |
| `a^` | Toehold domain | `DomType.ToeHold` |
| `a*` | Complement/reverse domain | `DomType.Rev` |
| `a^*` | Complement/reverse toehold | `DomType.ToeHoldRev` |
| `_` | Wildcard domain | `DomType.WildCard` |

Examples:

```dsd
<a^ b c*>
{a^* b*}
```

The parser handles this in `Parser.ParseSeq()`. The interpreter uses toehold and complement information when detecting binding, unbinding, and branch migration reactions.

## 9. Strand Syntax

DSDStudio supports three strand delimiters:

| Syntax | Meaning | Parser node |
| --- | --- | --- |
| `<...>` | Upper strand | `StrandType.upper` |
| `{...}` | Lower strand | `StrandType.lower` |
| `[...]` | Duplex segment | `StrandType.duplex` |

Examples:

```dsd
<a^ b c>
{a^* b^* c*}
[b c]
```

The implementation is in `Parser.ParseStrand()`.

## 10. Complexes and Linkers

A species can contain multiple strand or duplex segments:

```dsd
def Gate() = {a^*}[b c]{d^*}
```

The parser stores this as a complex structure composed of strand and linker nodes.

Two explicit linker operators are also recognized:

| Syntax | Meaning in parser |
| --- | --- |
| `:` | Lower linker |
| `::` | Upper linker |

The linker parsing logic is in `Parser.ParseSpecies()` and the AST representation is `LinkerNode`.

## 11. Initial Process Expressions

The initial state is written as a parenthesized process expression. Parallel species are separated by `|`:

```dsd
( N1 * Input1()
| N2 * Input2()
| 10 * Gate()
| 0.0 * Output())
```

Each entry gives an initial concentration multiplied by a species expression. This is parsed by `Parser.ParseProcess()`.

A common modeling pattern is to include an output species with initial concentration `0.0`. This makes the output appear in generated result tables and trajectory plots even before it is produced by reactions.

## 12. Supported Syntax Summary

| Construct | Supported | Notes |
| --- | --- | --- |
| `directive simulation {initial=...; final=...;}` | Yes | Main simulation time settings. |
| `directive simulator deterministic` | Yes | Deterministic ODE simulation path. |
| Numeric `def` values | Yes | Used in process expressions. |
| Zero-argument species templates | Yes | Example: `def Gate() = ...`. |
| Toeholds `a^` | Yes | Parsed as toehold domains. |
| Complements `a*`, `a^*` | Yes | Used for complement checks. |
| Upper/lower/duplex strands | Yes | `<...>`, `{...}`, `[...]`. |
| Parallel process operator `\|` | Yes | Separates initial species entries. |
| Nested block comments | Yes | `(* ... *)`. |
| Deterministic ODE simulation | Yes | Current primary simulation mode. |
| Stochastic simulation | No | Not implemented in the current release. |
| Full compatibility with all Visual DSD constructs | No | DSDStudio implements the subset documented here. |

## 13. Tutorial: DNA Hybridization

A simplified hybridization model:

```dsd
directive simulation {initial=0; final=50000;}
directive simulator deterministic

def N1 = 1.0
def N2 = 1.0

def Input1() = <a^ b^ c>
def Input2() = {d a^* b^*}

( N1 * Input1()
| N2 * Input2())
```

What the model says:

- `Input1` is an upper strand containing two toeholds, `a^` and `b^`.
- `Input2` is a lower strand containing complementary toeholds, `a^*` and `b^*`.
- The interpreter can generate binding reactions when complementary domains are exposed.
- The generated reaction network is normalized and converted to mass-action ODEs.

Step-by-step:

1. Open `examples/DNA hybridization.dsd`.
2. Click `Compile`.
3. Inspect the generated reaction network and ODEs.
4. Click `Run Simulation`.
5. Inspect the trajectory plot and concentration table.

Expected result:

- The reaction view should contain binding or unbinding reactions between complementary exposed domains.
- The ODE view should contain mass-action terms derived from those reactions.
- The simulation should show time-dependent depletion of free input strands and accumulation of generated complexes, depending on the generated reaction network.

Implementation path:

```text
Parser.ParseDef()
-> Parser.ParseSpecies()
-> Interpreter.GenReactions()
-> ODEsys.CreateFromR4()
-> Solver path
```

## 14. Tutorial: Three-Way Branch Migration

A simplified three-way branch migration model:

```dsd
directive simulation {initial=0; final=50000;}
directive simulator deterministic

def N1 = 1.0
def N2 = 1.0

def Input1() = <a^> [b^ c]
def Input2() = {a^* b^* c*}

( N1 * Input1()
| N2 * Input2())
```

What the model says:

- `Input1` contains an exposed toehold `<a^>` followed by a duplex `[b^ c]`.
- `Input2` contains complementary lower-strand domains.
- Toehold binding can create an intermediate complex.
- Branch migration can then move the paired region through adjacent matching domains.

Step-by-step:

1. Open `examples/Three-way branch migration.dsd`.
2. Click `Compile`.
3. Inspect whether toehold binding and branch-migration reactions are generated.
4. Click `Run Simulation`.
5. Inspect the intermediate and product trajectories.

Expected result:

- The generated CRN should include reactions corresponding to toehold association and branch migration when the domains match.
- The trajectory plot should show the time evolution of the invading strand, intermediate complex, and product species.

Implementation path:

- Domain and strand structure is parsed by `Parser.ParseSeq()` and `Parser.ParseStrand()`.
- Toehold matching and migration checks are performed in `Lexer/Interpreter.cs`.
- Generated reactions are converted to ODE terms in `ODE/Ode.cs`.

## 15. Tutorial: AND Gate

A simplified AND gate model:

```dsd
directive simulation {initial=0; final=200000;}
directive simulator deterministic

def N1 = 1.0
def N2 = 1.0
def N = 10.0

def Input1() = <a^ b>
def Input2() = <c d^>
def Output() = <b c>
def AND() = {a^*}[b c]{d^*}

( N1 * Input1()
| N2 * Input2()
| 10 * AND()
| 0.0 * Output())
```

What the model says:

- `AND` is a gate species with two exposed lower toeholds and a duplex region.
- `Input1` and `Input2` interact with the gate through complementary domains.
- `Output` is initially absent but is included with concentration `0.0` so its concentration can be tracked.
- The deterministic simulation shows how output concentration evolves over time.

Step-by-step:

1. Open `examples/AND gate.dsd`.
2. Click `Compile`.
3. Inspect the reaction network, especially reactions involving `Input1`, `Input2`, `AND`, and `Output`.
4. Open the ODE view and confirm that the generated equations are non-empty.
5. Click `Run Simulation`.
6. Inspect the output species trajectory.

Expected result:

- The compiled network should contain reactions generated from interactions between the two inputs and the gate species.
- The ODE view should contain mass-action terms for the generated CRN.
- The `Output` species should appear in the result table and trajectory plot because it is included as `0.0 * Output()`.
- The output concentration should evolve from zero as the gate is consumed or transformed by reactions involving the two inputs.

This example demonstrates how named species templates make larger systems easier to read.

## 16. From Model to ODE

After reaction generation, DSDStudio builds an ODE system from reactions. A reaction contains:

- reactants
- products
- forward and reverse rates

The `ODEsys.CreateFromR4()` method applies mass-action terms:

```text
reactants consume concentration
products gain concentration
reaction rates become ODE coefficients
```

Conceptually, each species concentration follows a mass-action equation of the form:

```text
d[C_i]/dt = sum_j stoichiometric_change(i, j) * rate_j * product_m [C_m]^{reactant_order(j, m)}
```

The ODE system can be rendered as LaTeX through `ODEsys.ToLatex()`, which is used by the IDE result view and export path.

## 17. Benchmark Tutorial

The benchmark executable is separate from the small `.dsd` examples. It is intended to stress-test the ODE solver backend using generated ODE systems.

Show help:

```powershell
.\Benchmark.exe --help
```

Run a small smoke test:

```powershell
.\Benchmark.exe --min 4 --max 512 --times 1 --output benchmark-results/smoke.csv
```

Run the manuscript-scale benchmark setting:

```powershell
.\Benchmark.exe --min 4 --max 16384 --times 3 --seed 288825296 --output benchmark-results/platform_name.csv
```

Expected result:

- The console should print device information and timing rows.
- A CSV file should be written to the selected output path.
- A summary CSV file should be generated for plotting runtime and speedup curves.

Solver comparison note:

- The CPU and GPU benchmark paths use the same Dormand--Prince RK45 method and identical tolerance settings.
- The CPU baseline is single-core unless otherwise specified.
- The GPU path uses ILGPU to parallelize rate evaluation while retaining host-side adaptive step control.
- Small systems may be faster on CPU because GPU kernel launch and synchronization overhead can dominate.

## 18. Programmatic Use

The IDE uses `DSDCoreService`, but the core API can also be used directly from C#:

```csharp
using DSDCore;

string source = File.ReadAllText("examples/DNA hybridization.dsd");
var core = new DSDCore.DSDCore(source);

var reactionSvgs = core.GetSvgs();
var odeSystem = core.GetODEsys();
var solution = core.SolveWithTime();
```

Useful entry points:

| Task | Code |
| --- | --- |
| Compile source | `new DSDCore.DSDCore(source)` |
| Get ODE system | `DSDCore.GetODEsys()` |
| Solve with time points | `DSDCore.SolveWithTime()` |
| Get reaction diagrams | `DSDCore.GetSvgs()` |

## 19. Implementation Map

| Feature | Main implementation file |
| --- | --- |
| Tokenization and comments | `Lexer/Lexer.cs` |
| Directives, definitions, processes, species syntax | `Lexer/Parser.cs` |
| AST node types | `Lexer/AST.cs` |
| Reaction generation | `Lexer/Interpreter.cs` |
| ODE construction and LaTeX output | `ODE/Ode.cs` |
| CPU/GPU deterministic solving | `Solver/Solver.cs`, `Solver/GPURK45.cs` |
| IDE compile/simulate bridge | `IDE/Services/DSDCoreService.cs` |
| Standalone CPU/GPU benchmark | `Benchmark/` |

## 20. Practical Modeling Tips

- Start from one of the files in `examples/`.
- Use `def` names for every important strand, gate, and output species.
- Include expected output species with `0.0 * Output()` if you want them visible in result tables and plots.
- Use `directive simulation {initial=...; final=...; points=...;}` when you need explicit simulation timing.
- Include `directive simulator deterministic` for deterministic ODE simulation examples.
- After compilation, inspect the generated reaction diagrams and ODEs before relying on timing or concentration curves.
- If a model produces no expected products, first check domain complementarity, toehold notation, and whether the output species was included in the initial process expression.

## 21. Troubleshooting

| Symptom | Possible cause | Suggested check |
| --- | --- | --- |
| The model does not compile | Syntax error or unsupported construct | Check line/column error messages and compare with the examples above. |
| Output species is missing from plots | Output species was not part of the initial process | Add `0.0 * Output()` to the process expression. |
| No reactions are generated | Domains may not be complementary or exposed | Check `a^` versus `a^*`, strand orientation, and duplex placement. |
| Simulation is slow for tiny systems on GPU | GPU overhead dominates small workloads | Use CPU path or test a larger system. |
| Benchmark output differs across hardware | Different CPUs/GPUs and drivers affect runtime | Compare trends and summary CSVs rather than expecting identical wall-clock times. |
