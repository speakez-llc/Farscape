namespace Farscape.Core

open CppSharp
open CppSharp.Parser
open CppSharp.AST

/// Module to handle C++ parsing with CppSharp/LibClang
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

    /// Represents a C++ struct declaration
    type StructDecl = {
        Name: string
        Fields: (string * string) list
        Documentation: string option 
    }

    /// Represents a C++ enum declaration
    type EnumDecl = {
        Name: string
        Values: (string * uint64) list
        Documentation: string option 
    }

    /// Represents a C++ typedef declaration
    type TypedefDecl = {
        Name: string
        UnderlyingType: string
        Documentation: string option 
    }

    /// Forward declaration for circular references
    type Declaration = 
        | Function of FunctionDecl
        | Struct of StructDecl
        | Enum of EnumDecl
        | Typedef of TypedefDecl
        | Namespace of NamespaceDecl
        | Class of ClassDecl

    /// Represents a C++ namespace declaration
    and NamespaceDecl = {
        Name: string
        Declarations: Declaration list 
    }

    /// Represents a C++ class declaration
    and ClassDecl = {
        Name: string
        Methods: Declaration list
        Fields: (string * string) list
        Documentation: string option
        IsAbstract: bool 
    }

    /// Visits declarations and collects them as our custom Declaration type
    type DeclarationVisitor() =
        inherit AstVisitor()
    
        let declarations = ResizeArray<Declaration>()
    
        member _.GetDeclarations() = declarations |> List.ofSeq
    
        override _.VisitDeclaration(decl: CppSharp.AST.Declaration) = true
    
        override _.VisitFunctionDecl(func: Function) =
            let parameters =
                func.Parameters
                |> Seq.map (fun p -> p.Name, p.Type.ToString())
                |> List.ofSeq
        
            declarations.Add(
                Function {
                    Name = func.Name
                    ReturnType = func.ReturnType.ToString()
                    Parameters = parameters
                    Documentation = Option.ofObj func.Comment.BriefText
                    IsVirtual = 
                        match func with 
                        | :? Method as m -> m.IsVirtual 
                        | _ -> false
                    IsStatic = 
                        match func with
                        | :? Method as m -> m.IsStatic
                        | _ -> false
                })
            true
    
        override _.VisitClassDecl(classDecl: Class) =
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
                    Documentation = Option.ofObj classDecl.Comment.BriefText
                    IsAbstract = classDecl.IsAbstract
                })
            true
    
               
        override _.VisitEnumDecl(enumDecl: Enumeration) =
            declarations.Add(
                Enum {
                    Name = enumDecl.Name
                    Values = enumDecl.Items
                            |> Seq.map (fun i -> i.Name, i.Value)
                            |> List.ofSeq
                    Documentation = Option.ofObj enumDecl.Comment.BriefText
                })
            true
    
        override _.VisitNamespace(namespaceDecl: Namespace) =
            let visitor = DeclarationVisitor()
            namespaceDecl.Declarations |> Seq.iter (fun d -> visitor.VisitDeclaration(d) |> ignore)
        
            declarations.Add(
                Namespace {
                    Name = namespaceDecl.Name
                    Declarations = visitor.GetDeclarations()
                })
            true

    /// Parser options
    type HeaderParserOptions = {
        HeaderFile: string
        IncludePaths: string list
        Verbose: bool
    }

    /// Parse a C++ header file using CppSharp/LibClang
    let parseHeader (options: HeaderParserOptions) =
        let parserOptions = new ParserOptions()
        
        // Add include directories
        for includePath in options.IncludePaths do
            parserOptions.AddIncludeDirs(includePath)
            
        parserOptions.SetupMSVC()
        
        // Create the AST context directly
        let astContext = new ASTContext()
        
        // Parse the header using LibClang
        let success = ClangParser.ParseHeader(parserOptions)
        
        if success.DiagnosticsCount > 0u then
            failwith "Failed to parse header"
        
        // Visit the translation units
        let visitor = DeclarationVisitor()
        
        for unit in astContext.TranslationUnits do
            unit.Visit(visitor) |> ignore
            
        visitor.GetDeclarations()
    
    /// Parse a C++ header
    let parse headerFile includePaths verbose =
        let options = {
            HeaderFile = headerFile
            IncludePaths = includePaths
            Verbose = verbose
        }
    
        if verbose then
            printfn "Parsing header: %s" headerFile
            printfn "Include paths: %A" includePaths
    
        let declarations = parseHeader options
    
        if verbose then
            printfn "Found %d declarations" (List.length declarations)
    
        declarations