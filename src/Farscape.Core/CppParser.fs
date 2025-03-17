namespace Farscape.Core

open System
open System.IO
open System.Collections.Generic
open CppSharp
open CppSharp.AST
open CppSharp.Parser

/// Module to handle C++ parsing with CppSharp/LibClang
module CppParser =
    /// Represents a parsed C++ declaration
    type Declaration =
        | Function of {| Name: string; ReturnType: string; Parameters: (string * string) list; Documentation: string option; IsVirtual: bool; IsStatic: bool |}
        | Struct of {| Name: string; Fields: (string * string) list; Documentation: string option |}
        | Enum of {| Name: string; Values: (string * int64) list; Documentation: string option |}
        | Typedef of {| Name: string; UnderlyingType: string; Documentation: string option |}
        | Namespace of {| Name: string; Declarations: Declaration list |}
        | Class of {| Name: string; Methods: Declaration list; Fields: (string * string) list; Documentation: string option; IsAbstract: bool |}
    
    /// Options for parsing
    type ParserOptions = {
        HeaderFile: string
        IncludePaths: string list
        Verbose: bool
    }
    
    /// Custom AST visitor to collect declarations
    type DeclarationVisitor() =
        inherit ASTVisitor()
        
        let mutable declarations = []
        
        member this.Declarations = declarations
        
        override this.VisitFunctionDecl(func) =
            let parameters = 
                [for param in func.Parameters do
                    yield (param.Name, param.Type.ToString())]
            
            let funcDecl = Function {| 
                Name = func.Name
                ReturnType = func.ReturnType.ToString()
                Parameters = parameters
                Documentation = if String.IsNullOrEmpty(func.Comment.BriefText) then None else Some(func.Comment.BriefText)
                IsVirtual = func.IsVirtual
                IsStatic = func.IsStatic
            |}
            
            declarations <- funcDecl :: declarations
            true
            
        override this.VisitClassDecl(classDecl) =
            let visitor = DeclarationVisitor()
            for method in classDecl.Methods do
                visitor.VisitFunctionDecl(method) |> ignore
                
            let methods = visitor.Declarations
            
            let fields = 
                [for field in classDecl.Fields do
                    yield (field.Name, field.Type.ToString())]
                    
            let classDeclaration = Class {|
                Name = classDecl.Name
                Methods = methods
                Fields = fields
                Documentation = if String.IsNullOrEmpty(classDecl.Comment.BriefText) then None else Some(classDecl.Comment.BriefText)
                IsAbstract = classDecl.IsAbstract
            |}
            
            declarations <- classDeclaration :: declarations
            true
            
        override this.VisitEnumDecl(enumDecl) =
            let values = 
                [for item in enumDecl.Items do
                    yield (item.Name, item.Value)]
                    
            let enumDeclaration = Enum {|
                Name = enumDecl.Name
                Values = values
                Documentation = if String.IsNullOrEmpty(enumDecl.Comment.BriefText) then None else Some(enumDecl.Comment.BriefText)
            |}
            
            declarations <- enumDeclaration :: declarations
            true
            
        override this.VisitTypedefDecl(typedefDecl) =
            let typedefDeclaration = Typedef {|
                Name = typedefDecl.Name
                UnderlyingType = typedefDecl.Type.ToString()
                Documentation = if String.IsNullOrEmpty(typedefDecl.Comment.BriefText) then None else Some(typedefDecl.Comment.BriefText)
            |}
            
            declarations <- typedefDeclaration :: declarations
            true
            
        override this.VisitNamespaceDecl(namespaceDecl) =
            let visitor = DeclarationVisitor()
            namespaceDecl.Declarations |> Seq.iter (fun decl -> visitor.Visit(decl) |> ignore)
            
            let nsDeclaration = Namespace {|
                Name = namespaceDecl.Name
                Declarations = visitor.Declarations
            |}
            
            declarations <- nsDeclaration :: declarations
            true
    
    /// Parse a header file using CppSharp/LibClang
    let parseHeader (options: ParserOptions) : Declaration list =
        let parserOptions = ParserOptions()
        
        // Set include paths
        for path in options.IncludePaths do
            parserOptions.AddIncludeDirs(path)
        
        // Create parser
        let parser = ClangParser()
        
        // Set up parser
        parserOptions.SetupMSVC()
        parser.Options <- parserOptions
        
        // Parse the header
        let parseResult = parser.ParseHeader(options.HeaderFile)
        
        if not parseResult.Success then
            failwithf "Failed to parse header: %s" parseResult.Diagnostics
            
        // Visit the AST
        let visitor = DeclarationVisitor()
        parseResult.Library.Visit(visitor) |> ignore
        
        // Return collected declarations
        visitor.Declarations
        
    /// Parse a C++ header
    let parse (headerFile: string) (includePaths: string list) (verbose: bool) : Declaration list =
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