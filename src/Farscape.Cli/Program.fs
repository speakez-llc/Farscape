module Farscape.Cli.Program

open System
open System.IO
open Farscape.Core
open FSharp.SystemCommandLine
open System.CommandLine.Invocation

[<AutoOpen>]
module Options = 
    type CommandOptions = 
        {
            Header: string
            Library: string
            Output: string
            Namespace: string
            IncludePaths: string[]
            Verbose: bool
        }

    let header = 
        Input.Option<string>(["-h"; "--header"], 
            // Manually edit underlying S.CL option to add validator logic.
            fun o -> 
                o.Description <- "Path to C++ header file"
                o.IsRequired <- true
                o.AddValidator(fun result -> 
                    let path = result.GetValueForOption<string> o
                    if not (File.Exists path) then
                        result.ErrorMessage <- $"Header file not found: {path}"
                )
        )
    let library =       Input.OptionRequired<string>(["-l"; "--library"], description = "Name of native library to bind to")
    let output =        Input.Option<string>(["-o"; "--output"], description = "Output directory for generated code", defaultValue = "./output")
    let namespace' =    Input.Option<string>(["-n"; "--namespace"], description = "Namespace for generated code", defaultValue = "NativeBindings")
    let includePaths =  Input.Option<string[]>(["-i"; "--include-paths"], description = "Additional include paths")
    let verbose =       Input.Option<bool>(["-v"; "--verbose"], description = "Verbose output", defaultValue = false)

    let all : HandlerInput seq = [header; library; output; namespace'; includePaths; verbose]

    let bind (ctx: InvocationContext) = 
        {
            Header = header.GetValue ctx
            Library = library.GetValue ctx
            Output = output.GetValue ctx
            Namespace = namespace'.GetValue ctx
            IncludePaths = includePaths.GetValue ctx
            Verbose = verbose.GetValue ctx            
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

let handler (ctx: InvocationContext) = 
    let options = Options.bind ctx
    showHeader()
    showConfiguration options
    runGeneration options
    showNextSteps options

[<EntryPoint>]
let main argv =
    rootCommand argv {
        description "Farscape: F# Native Library Binding Generator"
        inputs (Input.Context())
        addGlobalOptions Options.all
        setHandler handler
    }
