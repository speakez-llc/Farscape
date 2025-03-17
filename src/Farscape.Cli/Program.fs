module Farscape.Cli.Program
    
open System
open System.IO
open Farscape.Core
open Spectre.Console
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

    let configTable =
        Table()

    configTable.AddColumn("Setting") |> ignore
    configTable.AddColumn("Value") |> ignore

    configTable.AddRow("[yellow]Header File[/]", options.Header) |> ignore
    configTable.AddRow("[yellow]Library Name[/]", options.Library) |> ignore
    configTable.AddRow("[yellow]Output Directory[/]", options.Output) |> ignore
    configTable.AddRow("[yellow]Namespace[/]", options.Namespace) |> ignore
    configTable.AddRow(
        "[yellow]Include Paths[/]",
        if String.IsNullOrEmpty(options.IncludePaths) then
            "[gray]None[/]"
        else
            options.IncludePaths.Replace(",", "\n")) |> ignore

    configTable.RoundedBorder() |> toOutputPayload |> toConsole

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

    // Use status for process steps
    AnsiConsole.Status()
        .Start("Processing...", fun ctx ->
            // Execute parsing step
            ctx.Status("[blue]Parsing C++ header...[/]") |> ignore
            let declarations = CppParser.parse options.Header includePaths options.Verbose

            // Execute mapping step
            ctx.Status("[blue]Mapping C++ types to F#...[/]") |> ignore
            System.Threading.Thread.Sleep(1000) // Simulating work

            // Execute code generation step
            ctx.Status("[blue]Generating F# code...[/]") |> ignore
            System.Threading.Thread.Sleep(1000) // Simulating work

            // Execute project generation
            ctx.Status("[blue]Creating project files...[/]") |> ignore
            BindingGenerator.generateBindings generationOptions

            // Completion
            ctx.Status("[green]Complete![/]")
        ) |> ignore

    // Show completion message
    rule "[green]Generation Complete[/]" |> toConsole

    BlankLine |> toConsole

    // Output info
    rule $"[bold]Output:[/] F# bindings were successfully generated in [cyan]{options.Output}[/]" |> toConsole
    
    BlankLine |> toConsole

open SpectreCoff

let showNextSteps (options: CommandOptions) =
    // Show next steps
    rule "\n[bold]Next Steps:[/]" |> toConsole
    
    BlankLine |> toConsole

    let tree =
        Tree("[yellow]How to use the generated bindings:[/]")

    let buildProject = tree.AddNode("[cyan]Build the project[/]")
    buildProject.AddNode($"cd {options.Output}") |> ignore
    buildProject.AddNode("dotnet build") |> ignore

    let useInProject = tree.AddNode("[cyan]Use in your own project[/]")
    useInProject.AddNode($"Add a reference to {options.Library}.dll") |> ignore
    useInProject.AddNode($"open {options.Namespace}") |> ignore

    tree |> toOutputPayload |> toConsole

let showError (message: string) =
    rule $"[red]Error:[/] {message}" |> toConsole
    
    BlankLine |> toConsole

let showUsage () =
    rule "[yellow]Usage:[/] farscape [options]" |> toConsole

    BlankLine |> toConsole

    rule "[bold]Options:[/]" |> toConsole

    BlankLine |> toConsole

    let options = {
        Header = ""
        Library = ""
        Output = "./output"
        Namespace = "NativeBindings"
        IncludePaths = ""
        Verbose = false
    }

    let tableColumns = [
        "Setting"
        "Value"
    ]

    let tableRows = [
        ["[yellow]Header File[/]"; options.Header]
        ["[yellow]Library Name[/]"; options.Library]
        ["[yellow]Output Directory[/]"; options.Output]
        ["[yellow]Namespace[/]"; options.Namespace]
        [
            "[yellow]Include Paths[/]"
            if String.IsNullOrEmpty(options.IncludePaths) then
                "[gray]None[/]"
            else
                options.IncludePaths.Replace(",", "\n")
        ]
    ]

    let configTable =
        Table()

    configTable.AddColumn("Setting") |> ignore
    configTable.AddColumn("Value") |> ignore

    tableRows
    |> List.iter (fun row ->
        match row with
        | setting :: value :: _ -> configTable.AddRow(setting, value) |> ignore
        | _ -> ()
    )

    configTable.RoundedBorder() |> toOutputPayload |> toConsole

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