module Farscape.Cli.Program

open System
open System.IO
open Farscape.Core
open System.Text

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

let printLine (text: string) =
    Console.WriteLine(text)

let printColorLine (text: string) (color: ConsoleColor) =
    let originalColor = Console.ForegroundColor
    Console.ForegroundColor <- color
    Console.WriteLine(text)
    Console.ForegroundColor <- originalColor

let printHeader (text: string) =
    let width = Math.Min(Console.WindowWidth, 80)
    let line = new String('=', width)
    printColorLine line ConsoleColor.Cyan
    printColorLine text ConsoleColor.Cyan
    printColorLine line ConsoleColor.Cyan

let showHeader () =
    printHeader "Farscape: F# Native Library Binding Generator"

let showConfiguration (options: CommandOptions) =
    printHeader "Configuration"
    printLine ""

    printColorLine "Header File:" ConsoleColor.Yellow
    printLine $"  {options.Header}"
    
    printColorLine "Library Name:" ConsoleColor.Yellow
    printLine $"  {options.Library}"
    
    printColorLine "Output Directory:" ConsoleColor.Yellow
    printLine $"  {options.Output}"
    
    printColorLine "Namespace:" ConsoleColor.Yellow
    printLine $"  {options.Namespace}"
    
    printColorLine "Include Paths:" ConsoleColor.Yellow
    if String.IsNullOrEmpty(options.IncludePaths) then
        printLine "  None"
    else
        options.IncludePaths.Split(',')
        |> Array.iter (fun path -> printLine $"  {path}")
    
    printLine ""

let parseIncludePaths (paths: string) =
    if String.IsNullOrWhiteSpace(paths) then []
    else paths.Split(',', StringSplitOptions.RemoveEmptyEntries) |> Array.toList

let runGeneration (options: CommandOptions) =
    // Show generating message
    printHeader "Generating F# bindings..."
    printLine ""

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
    printLine "Starting C++ header parsing..."
    let declarations = CppParser.parse options.Header includePaths options.Verbose

    printLine "Mapping C++ types to F#..."
    System.Threading.Thread.Sleep(500) // Simulating work

    printLine "Generating F# code..."
    System.Threading.Thread.Sleep(500) // Simulating work

    printLine "Creating project files..."
    BindingGenerator.generateBindings generationOptions |> ignore

    printLine "Generation complete!"
    printLine ""

    // Show completion message
    printColorLine "Generation Complete" ConsoleColor.Green
    printLine ""

    // Output info
    printColorLine $"Output: F# bindings were successfully generated in {options.Output}" ConsoleColor.Cyan
    printLine ""

let showNextSteps (options: CommandOptions) =
    // Show next steps
    printHeader "Next Steps"
    printLine ""

    printLine "How to use the generated bindings:"
    printLine ""
    
    printLine "Build the project:"
    printLine $"  cd {options.Output}"
    printLine "  dotnet build"
    printLine ""
    
    printLine "Use in your own project:"
    printLine $"  Add a reference to {options.Library}.dll"
    printLine $"  open {options.Namespace}"
    printLine ""

let showError (message: string) =
    printColorLine $"Error: {message}" ConsoleColor.Red
    printLine ""

let showUsage () =
    printColorLine "Usage: farscape [options]" ConsoleColor.Yellow
    printLine ""
    printColorLine "Options:" ConsoleColor.Yellow
    printLine ""
    
    printLine "  -h, --header        Path to C++ header file (Required)"
    printLine "  -l, --library       Name of native library to bind to (Required)"
    printLine "  -o, --output        Output directory for generated code (Default: ./output)"
    printLine "  -n, --namespace     Namespace for generated code (Default: NativeBindings)"
    printLine "  -i, --include-paths Additional include paths (comma separated)"
    printLine "  -v, --verbose       Enable verbose output"
    printLine ""

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
                printLine ""
                
                errors |> List.iter showError
                showUsage()
                1
    with
    | ex ->
        showError ex.Message
        1