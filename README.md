# DSDStudio

DSDStudio is a Windows desktop IDE and simulation workbench for DNA strand displacement (DSD) systems. It provides a `.dsd` editor, DSD source parsing, reaction-network generation, mass-action ODE construction, deterministic simulation, CPU/GPU benchmark utilities, and result visualization/export.

- [Tutorials and syntax guide](TUTORIALS.md)
- [Example models](examples/)
- [Benchmark project](Benchmark/)

## Publication
Our paper “DSDStudio: A GPU-Accelerated General-Purpose DSD Simulation Software” has been accepted for publication in Frontiers of Computer Science (FCS) special column “Code & Data”.

Citation: Tongmao Ma, Beining Qi, Alfonso Rodríguez-Patón, Yijun Xiao, Tao Song. DSDStudio: A GPU-Accelerated General-Purpose DSD Simulation Software. Front. Comput. Sci., 2026. DOI: 10.1007/s11704-026-60800-w

## Reviewer Quick Verification

For reviewers who only need to verify the submitted software, a pre-built Windows package is provided together with the source code. The package can be used without opening Visual Studio.

### Option A: Run the pre-built package

1. Download and unzip the release package.
2. Run `DSDStudio.exe`.
3. Open `examples/AND gate.dsd`.
4. Click `Compile`.
5. Check that the generated reaction network and ODEs are displayed.
6. Click `Run Simulation`.
7. Check that concentration trajectories and result tables are displayed.

The release package is framework-dependent. Install the .NET 9 Desktop Runtime before running `DSDStudio.exe`.

### Option B: Build and run from source

```powershell
dotnet build DSD.sln
dotnet run --project IDE/IDE.csproj
```

You can also open `DSD.sln` in Visual Studio 2022 or newer and run the `IDE` project.

### Benchmark smoke test

The benchmark executable can be used to verify that the CPU/GPU solver paths and CSV output are working.

```powershell
.\Benchmark.exe --min 4 --max 512 --times 1 --output benchmark-results/smoke.csv
```

Expected behavior: the program prints hardware/device information, runs the available solver paths, and writes a CSV file under `benchmark-results/`.

### Full benchmark command used for the manuscript

```powershell
.\Benchmark.exe --min 4 --max 16384 --times 3 --seed 288825296 --output benchmark-results/platform_name.csv
```

The benchmark writes per-run timing rows and a summary CSV file that can be used to reproduce runtime and speedup plots.

## Abstract

DNA strand displacement provides a programmable mechanism for molecular computing, but experimental validation of DSD circuits can be costly and time-consuming. DSDStudio is a simulation workbench for designing and verifying DSD systems. It integrates a Visual DSD-style syntax subset, source parsing, chemical reaction network generation, mass-action ODE construction, deterministic simulation, result visualization, and a GPU-accelerated Dormand--Prince solver path for large-scale ODE systems. The repository also includes reproducible CPU/GPU benchmarks for evaluating solver performance.

## Features

- Edit `.dsd` models in a WPF-based IDE with line numbers and DSD syntax highlighting.
- Compile DSD source files and report syntax or semantic errors with line and column information.
- Generate DNA strand displacement reaction networks from initial species and domain definitions.
- Convert generated reactions into mass-action ODE systems and display them as LaTeX.
- Run deterministic simulations and view concentration curves over time.
- Export reaction diagrams as SVG, ODEs as `.tex`, simulation tables as Excel files, and charts as SVG.
- Provide example models for DNA hybridization, three-way branch migration, and an AND gate.
- Provide a standalone benchmark executable for CPU/GPU ODE solver timing experiments.

## Project Structure

```text
DSDStudio-main/
├── DSD.sln                 # Visual Studio solution
├── IDE/                    # WPF desktop application
├── Lexer/                  # DSDCore: lexer, parser, AST, interpreter, CRN generation
├── ODE/                    # ODE system model and LaTeX generation
├── Solver/                 # Numerical solvers, including ILGPU-based paths
├── PaintUtils/             # SVG/chart rendering helpers
├── Benchmark/              # Standalone CPU/GPU ODE benchmark executable
├── examples/               # Sample .dsd models
├── TUTORIALS.md            # Syntax guide and step-by-step tutorials
└── LICENSE                 # MIT license
```

### Main Projects

| Project | Target | Purpose |
| --- | --- | --- |
| `IDE` | `net9.0-windows` | WPF application and user interface. |
| `DSDCore` | `net9.0` | DSD lexer, parser, interpreter, reaction generation, and simulation entry points. |
| `ODE` | `net9.0` | ODE representation and LaTeX output. |
| `Solver` | `net9.0` | RK-based numerical solving and GPU-related solver support through ILGPU. |
| `PaintUtils` | `net9.0` | SVG and chart helper utilities. |
| `Benchmark` | `net9.0` | Standalone executable for large-scale CPU/GPU ODE timing tests. |

## Requirements

For the desktop IDE:

- Windows
- .NET 9 SDK, or .NET 9 Desktop Runtime for framework-dependent release packages
- Visual Studio 2022 or newer is recommended for WPF development

For the benchmark executable:

- Windows command line or PowerShell
- CPU execution works without a discrete GPU
- GPU execution uses ILGPU and requires a compatible GPU backend

The IDE is a WPF application targeting `net9.0-windows`, so it is intended to run on Windows.

## Getting Started

For release packages, unzip the archive and run the desktop application:

```powershell
.\DSDStudio.exe
```

Keep the packaged files in the same directory.

For source development, build the solution with the .NET SDK:

```powershell
dotnet build DSD.sln
```

To run the IDE from source:

```powershell
dotnet run --project IDE/IDE.csproj
```

## Example Models

The `examples/` directory contains small models intended for verification and tutorial use.

| File | Purpose | Expected behavior |
| --- | --- | --- |
| `DNA hybridization.dsd` | Basic complementary strand binding | Free input strands are consumed and a bound complex accumulates. |
| `Three-way branch migration.dsd` | Toehold binding followed by branch migration | Intermediate and product species appear as the reaction network expands. |
| `AND gate.dsd` | Two-input gate example | The output species is tracked from initial concentration `0.0` and increases when both inputs are present. |

See [TUTORIALS.md](TUTORIALS.md) for step-by-step instructions and syntax details.

## CPU/GPU Benchmark

The benchmark is a standalone console executable for testing large-scale ODE solving time on CPU and GPU paths. By default it prints hardware information, per-run timing rows, a best/average summary, and output file paths. Additional ODE checks and RK45 step diagnostics are printed only with `--debug`.

The CPU and GPU benchmark paths use the same Dormand--Prince RK45 integration scheme and identical absolute/relative tolerance settings. The CPU baseline is executed on a single CPU core without parallel rate-evaluation acceleration, whereas the GPU path parallelizes rate evaluation through ILGPU while retaining host-side adaptive step control.

The benchmark is intended to stress-test the ODE solver backend. It uses a built-in local-group ODE generator and should not be interpreted as claiming that every generated benchmark system corresponds to a hand-written DSD circuit.

Show available options:

```powershell
.\Benchmark.exe --help
```

Run the default benchmark. It tests systems from 4 to 16384 substances over three passes and uses the built-in local-group ODE generator:

```powershell
.\Benchmark.exe
```

Run a smaller benchmark and write results to CSV:

```powershell
.\Benchmark.exe --min 4 --max 512 --times 1 --output benchmark-results/smoke.csv
```

Run CPU-only with RK45 debug output:

```powershell
.\Benchmark.exe --min 4 --max 64 --times 1 --skip-gpu --debug --debug-every 1
```

The default base seed is fixed for reproducibility. Use `--seed` only when a different seed is needed:

```powershell
.\Benchmark.exe --seed 288825296
```

A manuscript-scale benchmark can be run as follows:

```powershell
.\Benchmark.exe --min 4 --max 16384 --times 3 --seed 288825296 --output benchmark-results/platform_name.csv
```

Each benchmark row prints and writes the exact seed used for reproducibility. After all passes, the console prints best-of-N and average timing columns, and a separate `*-summary.csv` file is written for charting. If a run needs diagnosis, enable `--debug` and inspect `dt`, `errNorm`, min-step warnings, and ODE checks. ODE checks report empty equations, missing terms, invalid factors, non-finite coefficients or derivatives, and large initial derivative magnitudes. Use `--log <path>` to choose a log path or `--no-log` to disable log file writing.

## Tutorials and Syntax

The modeling syntax is a subset of Visual DSD-style notation. Syntax details, examples, and implementation notes are maintained in [TUTORIALS.md](TUTORIALS.md). Example models are available in `examples/`.

## Output Views

After compilation or simulation, DSDStudio can show:

- Reaction diagrams generated as SVG.
- ODE systems rendered as LaTeX text.
- Simulation tables that can be exported to Excel.
- Concentration charts rendered with OxyPlot and exportable as SVG.

## Supported Scope and Current Limitations

DSDStudio currently focuses on deterministic ODE simulation for the implemented DSD syntax subset.

- The solver is deterministic and does not yet provide stochastic simulation for low-copy-number regimes.
- GPU acceleration is most beneficial for sufficiently large ODE systems. Small systems may be faster on the CPU path because GPU kernel launch, device synchronization, and data movement overhead can dominate runtime.
- The parser and interpreter implement a Visual DSD-style subset rather than full compatibility with every construct in other DSD tools.
- The benchmark executable is a solver stress test based on generated ODE systems; it is separate from the small hand-written DSD examples in `examples/`.

## Dependencies

Notable NuGet dependencies include:

- `AvalonEdit` for the editor.
- `CommunityToolkit.Mvvm` for MVVM infrastructure.
- `OxyPlot` and `OxyPlot.SkiaSharp` for plotting and SVG chart export.
- `SharpVectors` and `SkiaSharp.Views.WPF` for rendering support.
- `ILGPU` and `ILGPU.Algorithms` for GPU-oriented solver support.
- `unvell.ReoGridWPF.dll` and `FreeSpire.XLS` for spreadsheet-related functionality.

## Development Notes

- `IDE/Services/DSDCoreService.cs` is the bridge between the WPF interface and `DSDCore`.
- `Lexer/DSDCore.cs` exposes the high-level API for compiling, solving, retrieving reaction SVGs, and retrieving the ODE system.
- `Lexer/Lexer.cs` performs lexical analysis and comment handling.
- `Lexer/Parser.cs` contains the DSD grammar parser.
- `Lexer/Interpreter.cs` initializes species, generates the reaction network, creates the ODE system, and calls the solver.
- `ODE/Ode.cs` constructs the mass-action ODE representation and LaTeX output.
- `Solver/Solver.cs`, `Solver/GPURK4Solver.cs`, and `Solver/GPURK45.cs` contain the numerical solver implementations.
- `Benchmark/` contains the standalone benchmark program used for CPU/GPU timing experiments.

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
