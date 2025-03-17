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

    /// Project options for generating the F# project file
    type ProjectOptions = {
        ProjectName: string
        Namespace: string
        OutputDirectory: string
        References: string list
        NuGetPackages: (string * string) list
        HeaderFile: string
        LibraryName: string
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

    /// Generate wrapper code
    let generateWrapperCode (declarations: CppParser.Declaration list) (namespace': string) (libraryName: string) =
        let code = CodeGenerator.generateCode declarations namespace' libraryName
        let structTypes = extractStructTypes declarations
        let functionPointers = DelegatePointers.identifyFunctionPointers declarations

        let delegateTypes = DelegatePointers.generateDelegateTypes functionPointers
        let delegateWrappers = DelegatePointers.generateDelegateWrappers functionPointers
        let delegateUnwrappers = DelegatePointers.generateDelegateUnwrappers functionPointers
        let memoryManagement = MemoryManager.generateMemoryManagement structTypes

        let sb = StringBuilder()
        sb.AppendLine(code) |> ignore

        let appendSection (condition: string -> bool) (header: string) (content: string) =
            if condition content then
                sb.AppendLine() |> ignore
                sb.AppendLine($"// {header}") |> ignore
                sb.AppendLine(content) |> ignore

        appendSection (not << String.IsNullOrWhiteSpace) "Delegate types for function pointers" delegateTypes
        appendSection (not << String.IsNullOrWhiteSpace) "Delegate wrapper functions" delegateWrappers
        appendSection (not << String.IsNullOrWhiteSpace) "Delegate unwrapper functions" delegateUnwrappers
        appendSection (not << String.IsNullOrWhiteSpace) "Memory management functions" memoryManagement

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
        let (projectFile, sourceFile, readmeFile) = Project.generateProject projectOptions code

        // Log completion
        logVerbose "Binding generation completed successfully." options.Verbose
        logVerbose $"Bindings generated at: {projectFile}" options.Verbose