namespace Farscape.Core

open System.IO
open ProjectOptions
open CodeGenerator


module BindingGenerator =

    type GenerationOptions = {
        HeaderFile: FileInfo
        LibraryName: string
        OutputDirectory: string
        Namespace: string
        IncludePaths: string list
        Verbose: bool
    }

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

    let logVerbose (message: string) (verbose: bool) =
        if verbose then
            printfn "%s" message

    let generateBindings (options: GenerationOptions) =
        Directory.CreateDirectory(options.OutputDirectory) |> ignore

        logVerbose $"Starting binding generation for {options.HeaderFile}" options.Verbose
        logVerbose $"Target library: {options.LibraryName}" options.Verbose
        logVerbose $"Output directory: {options.OutputDirectory}" options.Verbose
        logVerbose $"Namespace: {options.Namespace}" options.Verbose

        logVerbose "Parsing header file..." options.Verbose
        let declarations = CppParser.parse options.HeaderFile.FullName options.IncludePaths options.Verbose

        logVerbose "Generating F# code..." options.Verbose
        let generatedCode = generateCode declarations options.Namespace options.LibraryName

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
            HeaderFile = options.HeaderFile.FullName
            LibraryName = options.LibraryName
            IncludePaths = options.IncludePaths
            Verbose = options.Verbose
        }

        let (solutionPath, libraryPath, testPath) = Project.generateProject projectOptions generatedCode

        logVerbose "Binding generation completed successfully." options.Verbose
        logVerbose $"Solution generated at: {solutionPath}" options.Verbose
        logVerbose $"Library project generated at: {libraryPath}" options.Verbose
        logVerbose $"Test project generated at: {testPath}" options.Verbose
        
        (solutionPath, libraryPath, testPath)