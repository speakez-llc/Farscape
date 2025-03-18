namespace Farscape.Core

open CppSharp.Parser
open CppSharp.AST

module CppParser =
    /// Represents a C++ function declaration
    type FunctionDecl = {
        Name: string
        ReturnType: string
        Parameters: (string * string) list
        Documentation: string option
        IsVirtual: bool
        IsStatic: bool 
    }

    type StructDecl = {
        Name: string
        Fields: (string * string) list
        Documentation: string option 
    }

    type EnumDecl = {
        Name: string
        Values: (string * uint64) list
        Documentation: string option 
    }

    type TypedefDecl = {
        Name: string
        UnderlyingType: string
        Documentation: string option 
    }

    type Declaration = 
        | Function of FunctionDecl
        | Struct of StructDecl
        | Enum of EnumDecl
        | Typedef of TypedefDecl
        | Namespace of NamespaceDecl
        | Class of ClassDecl

    and NamespaceDecl = {
        Name: string
        Declarations: Declaration list 
    }

    and ClassDecl = {
        Name: string
        Methods: Declaration list
        Fields: (string * string) list
        Documentation: string option
        IsAbstract: bool 
    }

    type DeclarationVisitor() =
        inherit AstVisitor()
    
        let declarations = ResizeArray<Declaration>()
    
        member _.GetDeclarations() = declarations |> List.ofSeq
    
        override _.VisitDeclaration(decl: CppSharp.AST.Declaration) = true
    
        override _.VisitFunctionDecl(func: CppSharp.AST.Function) =
            let parameters =
                func.Parameters
                |> Seq.map (fun p -> p.Name, p.Type.ToString())
                |> List.ofSeq
        
            declarations.Add(
                Function {
                    Name = func.Name
                    ReturnType = func.ReturnType.ToString()
                    Parameters = parameters
                    Documentation = 
                        if isNull func.Comment then None
                        else Option.ofObj func.Comment.BriefText
                    IsVirtual = 
                        match func with 
                        | :? CppSharp.AST.Method as m -> m.IsVirtual 
                        | _ -> false
                    IsStatic = 
                        match func with
                        | :? CppSharp.AST.Method as m -> m.IsStatic
                        | _ -> false
                })
            true
    
        override _.VisitClassDecl(classDecl: CppSharp.AST.Class) =
            let methodVisitor = DeclarationVisitor()
            classDecl.Methods |> Seq.iter (fun m -> methodVisitor.VisitDeclaration(m) |> ignore)
        
            let fields =
                classDecl.Fields
                |> Seq.map (fun f -> f.Name, f.Type.ToString())
                |> List.ofSeq
        
            declarations.Add(
                Class {
                    Name = classDecl.Name
                    Methods = methodVisitor.GetDeclarations()
                    Fields = fields
                    Documentation = 
                        if isNull classDecl.Comment then None
                        else Option.ofObj classDecl.Comment.BriefText
                    IsAbstract = classDecl.IsAbstract
                })
            true
    
               
        override _.VisitEnumDecl(enumDecl: CppSharp.AST.Enumeration) =
            declarations.Add(
                Enum {
                    Name = enumDecl.Name
                    Values = enumDecl.Items
                            |> Seq.map (fun i -> i.Name, i.Value)
                            |> List.ofSeq
                    Documentation = 
                        if isNull enumDecl.Comment then None
                        else Option.ofObj enumDecl.Comment.BriefText
                })
            true
    
        override _.VisitNamespace(namespaceDecl: CppSharp.AST.Namespace) =
            let visitor = DeclarationVisitor()
            namespaceDecl.Declarations |> Seq.iter (fun d -> visitor.VisitDeclaration(d) |> ignore)
        
            declarations.Add(
                Namespace {
                    Name = namespaceDecl.Name
                    Declarations = visitor.GetDeclarations()
                })
            true

    type HeaderParserOptions = {
        HeaderFile: string
        IncludePaths: string list
        Verbose: bool
    }

    let parseHeader (options: HeaderParserOptions) =
        let parserOptions = new ParserOptions()
    
        // Add include directories
        for includePath in options.IncludePaths do
            parserOptions.AddIncludeDirs(includePath)
    
        // Add standard include directories
        parserOptions.SetupMSVC()
        parserOptions.AddSystemIncludeDirs("D:\\msys64\\mingw64\\include")
        parserOptions.AddSystemIncludeDirs("D:\\msys64\\mingw64\\lib\\clang\\20\\include")
    
        // Setup for MinGW
        parserOptions.TargetTriple <- "x86_64-w64-mingw32"
    
        // Set the header file to parse
        parserOptions.Verbose <- options.Verbose
        parserOptions.AddArguments("-x")
        parserOptions.AddArguments("c++")
        parserOptions.AddArguments(options.HeaderFile)
    
        // Create a new parser and parse the header
        let parser = new ClangParser()
        let parseResult = ClangParser.ParseHeader(parserOptions)
    
        // Check for parsing errors
        if parseResult.DiagnosticsCount > 0u then
            let diagnostics = parseResult.DiagnosticsCount
            printfn "Diagnostics: %i" diagnostics
            failwith $"Failed to parse header: {options.HeaderFile}"
    
        // Get the AST context from the parsing result
        let astContext = ASTContext()
    
        // Visit the AST
        let visitor = DeclarationVisitor()
    
        // Process translation units from the context
        if astContext.TranslationUnits.Count = 0 then
            printfn "Warning: No translation units found"
        else
            try
                for unit in astContext.TranslationUnits do
                    unit.Visit(visitor :> AstVisitor) |> ignore
            with ex ->
                printfn $"Error during translation unit visit: {ex.Message}"
                reraise()
    
            if options.Verbose then
                printfn "Processed %d translation units" astContext.TranslationUnits.Count
    
        visitor.GetDeclarations()

    let parse headerFile includePaths verbose =
        let options = {
            HeaderFile = headerFile
            IncludePaths = includePaths
            Verbose = verbose
        }
        
        if verbose then
            printfn "Parsing header: %s" headerFile
            printfn "Include paths: %A" includePaths
        
        try
            let declarations = parseHeader options
            
            if verbose then
                printfn "Found %d declarations" (List.length declarations)
            
            declarations
        with ex ->
            printfn "Error parsing header: %s" ex.Message
            // Return empty list as fallback
            []