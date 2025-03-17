namespace Farscape.Core

open System
open System.IO
open System.Text

/// Main module to orchestrate the binding generation process
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
            let mutable structs = []
            
            for decl in decls do
                match decl with
                | CppParser.Declaration.Struct s ->
                    structs <- s.Name :: structs
                | CppParser.Declaration.Namespace ns ->
                    structs <- collect ns.Declarations @ structs
                | CppParser.Declaration.Class c when c.Methods.IsEmpty ->
                    // Simple classes without methods can be treated as structs for pinning
                    structs <- c.Name :: structs
                | _ -> ()
                
            structs
            
        collect declarations
        |> List.distinct
    
    /// Generate wrapper code
    let generateWrapperCode (declarations: CppParser.Declaration list) (namespace': string) (libraryName: string) =
        let code = CodeGenerator.generateCode declarations namespace' libraryName
        
        // Get unique struct types for memory management
        let structTypes = extractStructTypes declarations
        
        // Generate delegate types for function pointers
        let functionPointers = DelegatePointers.identifyFunctionPointers declarations
        let delegateTypes = DelegatePointers.generateDelegateTypes functionPointers
        let delegateWrappers = DelegatePointers.generateDelegateWrappers functionPointers
        let delegateUnwrappers = DelegatePointers.generateDelegateUnwrappers functionPointers
        
        // Generate memory management code for structs
        let memoryManagement = MemoryManager.generateMemoryManagement structTypes
        
        // Combine all the code
        let sb = StringBuilder()
        
        sb.AppendLine(code) |> ignore
        
        if not (String.IsNullOrWhiteSpace(delegateTypes)) then
            sb.AppendLine() |> ignore
            sb.AppendLine("// Delegate types for function pointers") |> ignore
            sb.AppendLine(delegateTypes) |> ignore
        
        if not (String.IsNullOrWhiteSpace(delegateWrappers)) then
            sb.AppendLine() |> ignore
            sb.AppendLine("// Delegate wrapper functions") |> ignore
            sb.AppendLine(delegateWrappers) |> ignore
        
        if not (String.IsNullOrWhiteSpace(delegateUnwrappers)) then
            sb.AppendLine() |> ignore
            sb.AppendLine("// Delegate unwrapper functions") |> ignore
            sb.AppendLine(delegateUnwrappers) |> ignore
        
        if not (String.IsNullOrWhiteSpace(memoryManagement)) then
            sb.AppendLine() |> ignore
            sb.AppendLine("// Memory management functions") |> ignore
            sb.AppendLine(memoryManagement) |> ignore
            
        sb.ToString()

    /// Log a message if verbose mode is enabled
    let logVerbose (message: string) (verbose: bool) =
        if verbose then
            printfn "%s" message

    /// Generate a complete binding project
    let generateBindings (options: GenerationOptions) =
        // Create output directory
        Directory.CreateDirectory(options.OutputDirectory)
        
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
        let code = generateWrapperCode declarations options.Namespace options.LibraryName
        
        // Set up project generation
        logVerbose "Creating project files..." options.Verbose
        let projectOptions = {
            ProjectGenerator.ProjectOptions.ProjectName = options.LibraryName
            ProjectGenerator.ProjectOptions.Namespace = options.Namespace
            ProjectGenerator.ProjectOptions.OutputDirectory = options.OutputDirectory
            ProjectGenerator.ProjectOptions.References = []
            ProjectGenerator.ProjectOptions.NuGetPackages = [
                "System.Memory", "4.5.5"
                "System.Runtime.CompilerServices.Unsafe", "6.0.0"
            ]
        }
        
        // Generate project files
        ProjectGenerator.generateProject projectOptions code
        
        // Log completion
        logVerbose "Binding generation completed successfully." options.Verbose
        logVerbose $"""Bindings generated at: {Path.Combine(options.OutputDirectory, options.LibraryName + ".fsproj")
            }""" options.Verbose