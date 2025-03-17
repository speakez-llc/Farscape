namespace Farscape.Core

open System
open System.IO
open System.Text
open Farscape.Core.ProjectOptions

/// Module for generating F# bindings for C++ libraries
module BindingGenerator =

    /// Options for binding generation
    type GenerationOptions = {
        HeaderFile: string
        LibraryName: string
        OutputDirectory: string
        Namespace: string
        IncludePaths: string list
        Verbose: bool
    }

    /// Extract struct types from declarations
    let extractStructTypes (declarations: CppParser.Declaration list) : string list =
        let rec collect (decls: CppParser.Declaration list) =
            [
                for decl in decls do
                    match decl with
                    | CppParser.Declaration.Struct s -> yield s.Name
                    | CppParser.Declaration.Namespace ns -> yield! collect ns.Declarations
                    | CppParser.Declaration.Class c when c.Methods.IsEmpty -> yield c.Name
                    | _ -> ()
            ]
        collect declarations
        |> List.distinct

    /// Log a message if verbose mode is enabled
    let logVerbose (message: string) (verbose: bool) =
        if verbose then
            printfn "%s" message

    /// Generate a complete binding project
    let generateBindings (options: GenerationOptions) =
        // Create output directory
        Directory.CreateDirectory(options.OutputDirectory) |> ignore

        // Log start information
        logVerbose $"Starting binding generation for {options.HeaderFile}" options.Verbose
        logVerbose $"Target library: {options.LibraryName}" options.Verbose
        logVerbose $"Output directory: {options.OutputDirectory}" options.Verbose
        logVerbose $"Namespace: {options.Namespace}" options.Verbose

        // Parse the header file
        logVerbose "Parsing header file..." options.Verbose
        let declarations = CppParser.parse options.HeaderFile options.IncludePaths options.Verbose

        // Generate F# code
        logVerbose "Generating F# code..." options.Verbose
        let generatedCode = CodeGenerator.generateCode declarations options.Namespace options.LibraryName

        // Set up project generation
        logVerbose "Creating project files..." options.Verbose
        let projectOptions : ProjectOptions = {
            ProjectName = options.LibraryName
            Namespace = options.Namespace
            OutputDirectory = options.OutputDirectory
            References = []
            NuGetPackages = [
                ("System.Memory", "4.5.5")
                ("System.Runtime.CompilerServices.Unsafe", "6.0.0")
            ]
            HeaderFile = options.HeaderFile
            LibraryName = options.LibraryName
            IncludePaths = options.IncludePaths
            Verbose = options.Verbose
        }

        // Generate project files
        let (solutionPath, libraryPath, testPath) = Project.generateProject projectOptions generatedCode

        // Log completion
        logVerbose "Binding generation completed successfully." options.Verbose
        logVerbose $"Solution generated at: {solutionPath}" options.Verbose
        logVerbose $"Library project generated at: {libraryPath}" options.Verbose
        logVerbose $"Test project generated at: {testPath}" options.Verbose
        
        // Return paths to important generated files
        (solutionPath, libraryPath, testPath)