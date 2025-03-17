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
        inherit CppSharp.AST.AstVisitor()
    
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
                    Documentation = Option.ofObj func.Comment.BriefText
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
                    Documentation = Option.ofObj classDecl.Comment.BriefText
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
                    Documentation = Option.ofObj enumDecl.Comment.BriefText
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

    /// Parser options
    type ParserOptions = {
        HeaderFile: string
        IncludePaths: string list
        Verbose: bool
    }

    /// Parse a C++ header file using CppSharp/LibClang
    /// Parser options
    type HeaderParserOptions = {  // Renamed from ParserOptions
        HeaderFile: string
        IncludePaths: string list
        Verbose: bool
    }
    
    /// Parse a C++ header file using CppSharp/LibClang
    let parseHeader (options: HeaderParserOptions) =
        let parserOptions = new ParserOptions()  // Fully qualified name not needed now
        options.IncludePaths |> List.iter parserOptions.AddIncludeDirs  // Note: AddIncludeDir not AddIncludeDirs
        parserOptions.SetupMSVC()

        use parser = new ClangParser(parserOptions)
        let parserResult = parser.ParseHeader(options.HeaderFile)

        if not parserResult.IsSuccess then
            failwithf "Failed to parse header: %A" parserResult.Diagnostics

        let visitor = DeclarationVisitor()
        parserResult.ASTContext.TranslationUnit.Visit(visitor) |> ignore  // Changed to Visit
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