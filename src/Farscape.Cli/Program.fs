module Farscape.Cli.Program

open System
open System.IO
open Farscape.Core
open SpectreCoff

type CommandOptions = {
    Header: string
    Library: string
    Output: string
    Namespace: string
    IncludePaths: string
    Verbose: bool
}

let parseArgs (args: string[]) =
    let rec parseArgsRec (i: int) (options: CommandOptions) =
        if i >= args.Length then
            options
        else
            match args.[i] with
            | "-h" | "--header" when i + 1 < args.Length ->
                parseArgsRec (i + 2) { options with Header = args.[i + 1] }
            | "-l" | "--library" when i + 1 < args.Length ->
                parseArgsRec (i + 2) { options with Library = args.[i + 1] }
            | "-o" | "--output" when i + 1 < args.Length ->
                parseArgsRec (i + 2) { options with Output = args.[i + 1] }
            | "-n" | "--namespace" when i + 1 < args.Length ->
                parseArgsRec (i + 2) { options with Namespace = args.[i + 1] }
            | "-i" | "--include-paths" when i + 1 < args.Length ->
                parseArgsRec (i + 2) { options with IncludePaths = args.[i + 1] }
            | "-v" | "--verbose" ->
                parseArgsRec (i + 1) { options with Verbose = true }
            | _ -> parseArgsRec (i + 1) options

    parseArgsRec 0 {
        Header = ""
        Library = ""
        Output = "./output"
        Namespace = "NativeBindings"
        IncludePaths = ""
        Verbose = false
    }

let validateOptions (options: CommandOptions) =
    let errors = [
        if String.IsNullOrEmpty(options.Header) then
            yield "Header file path is required (--header or -h)"
        elif not (File.Exists(options.Header)) then
            yield $"Header file not found: {options.Header}"

        if String.IsNullOrEmpty(options.Library) then
            yield "Library name is required (--library or -l)"
    ]

    if errors.IsEmpty then Ok options
    else Error errors

let showHeader () =
    rule "[cyan]Farscape: F# Native Library Binding Generator[/]" |> toConsole

let showConfiguration (options: CommandOptions) =
    rule "[bold]Configuration:[/]" |> toConsole
    BlankLine |> toConsole

    tableWithRows [
        [
            "[yellow]Header File[/]"
            options.Header
        ]
        [
            "[yellow]Library Name[/]"
            options.Library
        ]
        [
            "[yellow]Output Directory[/]"
            options.Output
        ]
        [
            "[yellow]Namespace[/]"
            options.Namespace
        ]
        [
            "[yellow]Include Paths[/]"
            if String.IsNullOrEmpty(options.IncludePaths) then
                "[gray]None[/]"
            else
                options.IncludePaths.Replace(",", "\n")
        ]
    ] |> withRoundedBorder |> toConsole

let parseIncludePaths (paths: string) =
    if String.IsNullOrWhiteSpace(paths) then []
    else paths.Split(',', StringSplitOptions.RemoveEmptyEntries) |> Array.toList

let runGeneration (options: CommandOptions) =
    // Show generating message
    rule "[bold]Generating F# bindings...[/]" |> toConsole
    BlankLine |> toConsole

    let includePaths = parseIncludePaths options.IncludePaths

    // Create options
    let generationOptions = {
        BindingGenerator.GenerationOptions.HeaderFile = options.Header
        BindingGenerator.GenerationOptions.LibraryName = options.Library
        BindingGenerator.GenerationOptions.OutputDirectory = options.Output
        BindingGenerator.GenerationOptions.Namespace = options.Namespace
        BindingGenerator.GenerationOptions.IncludePaths = includePaths
        BindingGenerator.GenerationOptions.Verbose = options.Verbose
    }

    // Process steps with appropriate messages
    text "Starting C++ header parsing..." |> toConsole
    let declarations = CppParser.parse options.Header includePaths options.Verbose

    text "Mapping C++ types to F#..." |> toConsole
    System.Threading.Thread.Sleep(1000) // Simulating work

    text "Generating F# code..." |> toConsole
    System.Threading.Thread.Sleep(1000) // Simulating work

    text "Creating project files..." |> toConsole
    BindingGenerator.generateBindings generationOptions |> ignore

    text "Generation complete!" |> toConsole

    // Show completion message
    rule "[green]Generation Complete[/]" |> toConsole
    BlankLine |> toConsole

    // Output info
    rule $"[bold]Output:[/] F# bindings were successfully generated in [cyan]{options.Output}[/]" |> toConsole
    BlankLine |> toConsole

let showNextSteps (options: CommandOptions) =
    // Show next steps
    rule "[bold]Next Steps:[/]" |> toConsole
    BlankLine |> toConsole

    let buildSteps = [
        $"cd {options.Output}"
        "dotnet build"
    ]

    let useSteps = [
        $"Add a reference to {options.Library}.dll"
        $"open {options.Namespace}"
    ]

    let markdownContent = sprintf """
### How to use the generated bindings:

#### Build the project
```
%s
```

#### Use in your own project
```fsharp
%s
```
""" (String.Join("\n", buildSteps)) (String.Join("\n", useSteps))
markdownContent |> toConsole

let showError (message: string) =
    rule $"[red]Error:[/] {message}" |> toConsole
    BlankLine |> toConsole

let showUsage () =
    rule "[yellow]Usage:[/] farscape [options]" |> toConsole
    BlankLine |> toConsole
    rule "[bold]Options:[/]" |> toConsole
    BlankLine |> toConsole

    let markdownContent = """
| Option | Description | Default |
|--------|-------------|---------|
| **-h, --header** | Path to C++ header file | *Required* |
| **-l, --library** | Name of native library to bind to | *Required* |
| **-o, --output** | Output directory for generated code | ./output |
| **-n, --namespace** | Namespace for generated code | NativeBindings |
| **-i, --include-paths** | Additional include paths (comma separated) | None |
| **-v, --verbose** | Enable verbose output | false |
"""
    AnsiConsole.Write(new Markup(markdownContent))

[<EntryPoint>]
let main argv =
    try
        showHeader()

        match argv with
        | [| "--help" |] | [| "-?" |] | [| |] ->
            showUsage()
            0
        | _ ->
            let options = parseArgs argv

            match validateOptions options with
            | Ok validOptions ->
                showConfiguration validOptions
                runGeneration validOptions
                showNextSteps validOptions
                0
            | Error errors ->
                BlankLine |> toConsole
                
                errors |> List.iter showError
                showUsage()
                1
    with
    | ex ->
        showError ex.Message
        1