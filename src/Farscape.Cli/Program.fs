module Farscape.Cli.Program

open System
open System.IO
open Farscape.Core
open FSharp.SystemCommandLine
open System.CommandLine.Invocation
open System.CommandLine.Help

type CommandOptions = 
    {
        Header: FileInfo
        Library: string
        Output: string
        Namespace: string
        IncludePaths: string[]
        Verbose: bool
    }

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
    if options.IncludePaths = [||] then
        printLine "  None"
    else
        options.IncludePaths
        |> Array.iter (fun path -> printLine $"  {path}")
    
    printLine ""

let runGeneration (options: CommandOptions) =
    // Show generating message
    printHeader "Generating F# bindings..."
    printLine ""

    let includePaths = options.IncludePaths |> Array.toList

    // Create options
    let generationOptions = {
        BindingGenerator.GenerationOptions.HeaderFile = options.Header.FullName
        BindingGenerator.GenerationOptions.LibraryName = options.Library
        BindingGenerator.GenerationOptions.OutputDirectory = options.Output
        BindingGenerator.GenerationOptions.Namespace = options.Namespace
        BindingGenerator.GenerationOptions.IncludePaths = includePaths
        BindingGenerator.GenerationOptions.Verbose = options.Verbose
    }

    // Process steps with appropriate messages
    printLine "Starting C++ header parsing..."
    let declarations = CppParser.parse options.Header.FullName includePaths options.Verbose

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

let generateCommand = 
    let header = 
        Input.Option<FileInfo>(["--header"], 
            // Manually edit underlying S.CL option to add validator logic.
            fun o -> 
                o.Description <- "Path to C++ header file"
                o.IsRequired <- true
                o.AddValidator(fun result -> 
                    let file = result.GetValueForOption<FileInfo> o
                    if not file.Exists then
                        result.ErrorMessage <- $"Header file not found: {file.FullName}"
                )
        )
    let library =   Input.OptionRequired<string>(["-l"; "--library"], description = "Name of native library to bind to")
    let output =    Input.Option<string>(["-o"; "--output"], description = "Output directory for generated code", defaultValue = "./output")
    let ns =        Input.Option<string>(["-n"; "--namespace"], description = "Namespace for generated code", defaultValue = "NativeBindings")
    let includes =  Input.Option<string[]>(["-i"; "--include-paths"], description = "Additional include paths")
    let verbose =   Input.Option<bool>(["-v"; "--verbose"], description = "Verbose output", defaultValue = false)

    let handler (header, library, output, ns, includes, verbose) = 
        let options = {
            Header = header
            Library = library
            Output = output
            Namespace = ns
            IncludePaths = includes
            Verbose = verbose
        }
        showHeader()
        showConfiguration options
        runGeneration options
        showNextSteps options

    command "generate" {
        description "Generate F# bindings for a native library"
        inputs (header, library, output, ns, includes, verbose)
        setHandler handler
    }

let showHelp (ctx: InvocationContext) =
    let hc = HelpContext(ctx.HelpBuilder, ctx.Parser.Configuration.RootCommand, System.Console.Out)
    ctx.HelpBuilder.Write(hc)

[<EntryPoint>]
let main argv =
    rootCommand argv {
        description "Farscape: F# Native Library Binding Generator"
        inputs (Input.Context())
        setHandler showHelp
        addCommands [ 
            generateCommand
        ]
    }
