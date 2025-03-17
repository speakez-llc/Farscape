namespace Farscape.Core

open System
open System.Text
open System.Collections.Generic

/// Module to generate F# code for bindings
module CodeGenerator =
    type CodeSection = {
        FileName: string
        Content: string
        Order: int
    }

    type GeneratedCode = {
        Sections: CodeSection list
    }

    /// Generate XML documentation comment
    let private generateDocComment (comment: string option) =
        match comment with
        | None -> ""
        | Some text ->
            let lines = text.Split([|'\n'|], StringSplitOptions.RemoveEmptyEntries)
            let sb = StringBuilder()
            sb.AppendLine("    /// <summary>") |> ignore
            for line in lines do
                sb.AppendLine($"    /// {line.Trim()}") |> ignore
            sb.AppendLine("    /// </summary>") |> ignore
            sb.ToString()
    
    /// Generate a P/Invoke declaration for a function
    let generatePInvoke (func: CppParser.Declaration) (libraryName: string) (typeMap: Map<string, TypeMapper.TypeMapping>) =
        match func with
        | CppParser.Declaration.Function f ->
            let returnType = 
                if typeMap.ContainsKey(f.ReturnType) then
                    typeMap.[f.ReturnType].FSharpName
                else
                    TypeMapper.getFSharpType f.ReturnType
                    
            let parameters = 
                f.Parameters
                |> List.map (fun (name, paramType) ->
                    let fsharpType = 
                        if typeMap.ContainsKey(paramType) then
                            typeMap.[paramType].FSharpName
                        else
                            TypeMapper.getFSharpType paramType
                    $"{name}: {fsharpType}")
                |> String.concat ", "
                
            let docComment = generateDocComment f.Documentation
            
            $"{docComment}    [<DllImport(\"{libraryName}\", CallingConvention = CallingConvention.Cdecl)>]\n    extern {returnType} {f.Name}({parameters})"
        | _ -> failwith "Expected a function declaration"
    
    /// Generate a struct declaration
    let generateStruct (struct': CppParser.Declaration) (typeMap: Map<string, TypeMapper.TypeMapping>) =
        match struct' with
        | CppParser.Declaration.Struct s ->
            let fields = 
                s.Fields
                |> List.map (fun (name, fieldType) ->
                    let fsharpType = 
                        if typeMap.ContainsKey(fieldType) then
                            typeMap.[fieldType].FSharpName
                        else
                            TypeMapper.getFSharpType fieldType
                    
                    // Check if field needs marshaling
                    let marshalAttr = 
                        if typeMap.ContainsKey(fieldType) && typeMap.[fieldType].MarshalAs.IsSome then
                            let marshal = typeMap.[fieldType].MarshalAs.Value
                            $"    [<MarshalAs(UnmanagedType.{marshal.Value})>]\n"
                        else
                            ""
                    
                    $"{marshalAttr}    val mutable {name}: {fsharpType}")
                |> String.concat "\n"
                
            let docComment = generateDocComment s.Documentation
                
            $"{docComment}[<Struct; StructLayout(LayoutKind.Sequential)>]\ntype {s.Name} =\n{fields}"
        | _ -> failwith "Expected a struct declaration"
    
    /// Generate an enum declaration
    let generateEnum (enum': CppParser.Declaration) =
        match enum' with
        | CppParser.Declaration.Enum e ->
            let values = 
                e.Values
                |> List.map (fun (name, value) -> $"    | {name} = {value}L")
                |> String.concat "\n"
                
            let docComment = generateDocComment e.Documentation
                
            $"{docComment}type {e.Name} =\n{values}"
        | _ -> failwith "Expected an enum declaration"
    
    /// Generate a class declaration
    let generateClass (class': CppParser.Declaration) (libraryName: string) (typeMap: Map<string, TypeMapper.TypeMapping>) =
        match class' with
        | CppParser.Declaration.Class c ->
            let methods =
                c.Methods
                |> List.map (fun method -> 
                    match method with
                    | CppParser.Declaration.Function _ -> generatePInvoke method libraryName typeMap
                    | _ -> "")
                |> List.filter (fun s -> s <> "")
                |> String.concat "\n\n"
                
            let fields = 
                c.Fields
                |> List.map (fun (name, fieldType) ->
                    let fsharpType = 
                        if typeMap.ContainsKey(fieldType) then
                            typeMap.[fieldType].FSharpName
                        else
                            TypeMapper.getFSharpType fieldType
                    
                    // Check if field needs marshaling
                    let marshalAttr = 
                        if typeMap.ContainsKey(fieldType) && typeMap.[fieldType].MarshalAs.IsSome then
                            let marshal = typeMap.[fieldType].MarshalAs.Value
                            $"    [<MarshalAs(UnmanagedType.{marshal.Value})>]\n"
                        else
                            ""
                    
                    $"{marshalAttr}    val mutable {name}: {fsharpType}")
                |> String.concat "\n"
                
            let docComment = generateDocComment c.Documentation
            
            if c.IsAbstract then
                $"{docComment}[<AbstractClass>]\ntype {c.Name}() =\n{fields}\n\n{methods}"
            else
                $"{docComment}type {c.Name}() =\n{fields}\n\n{methods}"
        | _ -> failwith "Expected a class declaration"
    
    /// Generate a typedef declaration
    let generateTypedef (typedef: CppParser.Declaration) (typeMap: Map<string, TypeMapper.TypeMapping>) =
        match typedef with
        | CppParser.Declaration.Typedef t ->
            let underlyingType = 
                if typeMap.ContainsKey(t.UnderlyingType) then
                    typeMap.[t.UnderlyingType].FSharpName
                else
                    TypeMapper.getFSharpType t.UnderlyingType
                    
            let docComment = generateDocComment t.Documentation
                
            $"{docComment}type {t.Name} = {underlyingType}"
        | _ -> failwith "Expected a typedef declaration"
    
    /// Generate a namespace declaration
    let rec generateNamespace (namespace': CppParser.Declaration) (libraryName: string) (typeMap: Map<string, TypeMapper.TypeMapping>) =
        match namespace' with
        | CppParser.Declaration.Namespace ns ->
            let declarations = 
                ns.Declarations
                |> List.map (fun decl -> generateDeclaration decl libraryName typeMap)
                |> String.concat "\n\n"
                
            $"namespace {ns.Name}\n\n{declarations}"
        | _ -> failwith "Expected a namespace declaration"
    
    /// Generate code for a declaration
    and generateDeclaration (decl: CppParser.Declaration) (libraryName: string) (typeMap: Map<string, TypeMapper.TypeMapping>) =
        match decl with
        | CppParser.Declaration.Function _ -> generatePInvoke decl libraryName typeMap
        | CppParser.Declaration.Struct _ -> generateStruct decl typeMap
        | CppParser.Declaration.Enum _ -> generateEnum decl
        | CppParser.Declaration.Typedef _ -> generateTypedef decl typeMap
        | CppParser.Declaration.Namespace _ -> generateNamespace decl libraryName typeMap
        | CppParser.Declaration.Class _ -> generateClass decl libraryName typeMap

    /// Generate wrapper class for a struct or class
    let generateWrapperClass (decl: CppParser.Declaration) (libraryName: string) (typeMap: Map<string, TypeMapper.TypeMapping>) =
        match decl with
        | CppParser.Declaration.Struct s ->
            let className = s.Name
            let sb = StringBuilder()
            
            // Generate class doc comment
            match s.Documentation with
            | Some doc -> 
                sb.AppendLine($"/// <summary>") |> ignore
                sb.AppendLine($"/// F# wrapper for {className} struct") |> ignore
                sb.AppendLine($"/// {doc}") |> ignore
                sb.AppendLine($"/// </summary>") |> ignore
            | None ->
                sb.AppendLine($"/// <summary>") |> ignore
                sb.AppendLine($"/// F# wrapper for {className} struct") |> ignore
                sb.AppendLine($"/// </summary>") |> ignore
                
            // Generate class definition
            sb.AppendLine($"type {className}Wrapper(handle: nativeint) =") |> ignore
            sb.AppendLine($"    /// The raw handle to the native {className}") |> ignore
            sb.AppendLine($"    member this.Handle = handle") |> ignore
            sb.AppendLine() |> ignore
            
            // Generate IDisposable implementation if needed
            let disposableFunctionName = $"{className.ToLower()}{className.Substring(1)}Destroy"
            sb.AppendLine($"    interface IDisposable with") |> ignore
            sb.AppendLine($"        member this.Dispose() = ") |> ignore
            sb.AppendLine($"            {libraryName}.{disposableFunctionName}(handle)") |> ignore
            sb.AppendLine() |> ignore
            
            // Generate factory methods
            sb.AppendLine($"    /// Create a new {className} wrapper") |> ignore
            sb.AppendLine($"    static member Create() =") |> ignore
            sb.AppendLine($"        let handle = {libraryName}.{className.ToLower()}{className.Substring(1)}Create()") |> ignore
            sb.AppendLine($"        new {className}Wrapper(handle)") |> ignore
            
            sb.ToString()
                
        | CppParser.Declaration.Class c ->
            let className = c.Name
            let sb = StringBuilder()
            
            // Generate class doc comment
            match c.Documentation with
            | Some doc -> 
                sb.AppendLine($"/// <summary>") |> ignore
                sb.AppendLine($"/// F# wrapper for {className} class") |> ignore
                sb.AppendLine($"/// {doc}") |> ignore
                sb.AppendLine($"/// </summary>") |> ignore
            | None ->
                sb.AppendLine($"/// <summary>") |> ignore
                sb.AppendLine($"/// F# wrapper for {className} class") |> ignore
                sb.AppendLine($"/// </summary>") |> ignore
                
            // Generate class definition
            sb.AppendLine($"type {className}(handle: nativeint) =") |> ignore
            sb.AppendLine($"    /// The raw handle to the native {className}") |> ignore
            sb.AppendLine($"    member this.Handle = handle") |> ignore
            sb.AppendLine() |> ignore
            
            // Generate IDisposable implementation if needed
            let disposableFunctionName = $"{char.ToLower(className.[0])}{className.Substring(1)}Destroy"
            sb.AppendLine($"    interface IDisposable with") |> ignore
            sb.AppendLine($"        member this.Dispose() = ") |> ignore
            sb.AppendLine($"            {libraryName}.{disposableFunctionName}(handle)") |> ignore
            sb.AppendLine() |> ignore
            
            // Generate methods
            let methods = 
                c.Methods 
                |> List.filter (fun m -> 
                    match m with 
                    | CppParser.Declaration.Function f -> not f.IsStatic
                    | _ -> false)
                    
            for method in methods do
                match method with
                | CppParser.Declaration.Function f ->
                    // Only generate wrapper methods for non-constructor/destructor methods
                    if not (f.Name.Contains(className) || f.Name.Contains("destroy") || f.Name.Contains("create")) then
                        let returnType = 
                            if typeMap.ContainsKey(f.ReturnType) then
                                typeMap.[f.ReturnType].FSharpName
                            else
                                TypeMapper.getFSharpType f.ReturnType
                        
                        let parameterValues = 
                            f.Parameters
                            |> List.map (fun (name, _) -> name)
                            |> String.concat ", "
                        
                        let parameters = 
                            f.Parameters
                            |> List.map (fun (name, paramType) ->
                                let fsharpType = 
                                    if typeMap.ContainsKey(paramType) then
                                        typeMap.[paramType].FSharpName
                                    else
                                        TypeMapper.getFSharpType paramType
                                $"{name}: {fsharpType}")
                            |> String.concat ", "
                        
                        // Generate doc comment
                        match f.Documentation with
                        | Some doc ->
                            sb.AppendLine($"    /// <summary>") |> ignore
                            sb.AppendLine($"    /// {doc}") |> ignore
                            sb.AppendLine($"    /// </summary>") |> ignore
                        | None -> ()
                        
                        sb.AppendLine($"    member this.{f.Name}({parameters}) : {returnType} =") |> ignore
                        sb.AppendLine($"        {libraryName}.{f.Name}(this.Handle, {parameterValues})") |> ignore
                        sb.AppendLine() |> ignore
                | _ -> ()
                
            // Generate static factory methods
            sb.AppendLine($"    /// <summary>") |> ignore
            sb.AppendLine($"    /// Create a new {className} instance") |> ignore
            sb.AppendLine($"    /// </summary>") |> ignore
            sb.AppendLine($"    static member Create() =") |> ignore
            sb.AppendLine($"        let handle = {libraryName}.{char.ToLower(className.[0])}{className.Substring(1)}Create()") |> ignore
            sb.AppendLine($"        new {className}(handle)") |> ignore
            
            sb.ToString()
            
        | _ -> ""
        
    /// Generate a wrapper function for a native function
    let generateWrapperFunction (func: CppParser.Declaration) (libraryName: string) (typeMap: Map<string, TypeMapper.TypeMapping>) =
        match func with
        | CppParser.Declaration.Function f ->
            let returnType = 
                if typeMap.ContainsKey(f.ReturnType) then
                    typeMap.[f.ReturnType].FSharpName
                else
                    TypeMapper.getFSharpType f.ReturnType
                    
            let parameters = 
                f.Parameters
                |> List.map (fun (name, paramType) ->
                    let fsharpType = 
                        if typeMap.ContainsKey(paramType) then
                            typeMap.[paramType].FSharpName
                        else
                            TypeMapper.getFSharpType paramType
                    $"{name}: {fsharpType}")
                |> String.concat ", "
                
            let parameterValues = 
                f.Parameters
                |> List.map (fun (name, _) -> name)
                |> String.concat ", "
                
            let docComment = generateDocComment f.Documentation
            
            // Generate a wrapper function with error handling
            let sb = StringBuilder()
            sb.AppendLine(docComment) |> ignore
            sb.AppendLine($"let {f.Name} {parameters} : {returnType} =") |> ignore
            
            // Add error handling for integer return types that might be error codes
            if returnType = "int32" || returnType = "int" then
                sb.AppendLine($"    let result = {libraryName}.{f.Name}({parameterValues})") |> ignore
                sb.AppendLine($"    if result < 0 then") |> ignore
                sb.AppendLine($"        failwith $\"Error calling {f.Name}: %d{{result}}\"") |> ignore
                sb.AppendLine($"    result") |> ignore
            else
                sb.AppendLine($"    {libraryName}.{f.Name}({parameterValues})") |> ignore
                
            sb.ToString()
        | _ -> ""
        
    /// Generate the entire F# code for bindings
    let generateCode (declarations: CppParser.Declaration list) (namespace': string) (libraryName: string) : GeneratedCode =
        let typesList = TypeMapper.mapTypes declarations
        let typeMap = typesList |> List.map (fun t -> t.OriginalName, t) |> Map.ofList
        
        // Organize declarations by type
        let functions, structs, enums, typedefs, classes =
            declarations |> List.fold (fun (funcs, structs, enums, typedefs, classes) decl ->
                match decl with
                | CppParser.Declaration.Function _ -> (decl :: funcs, structs, enums, typedefs, classes)
                | CppParser.Declaration.Struct _ -> (funcs, decl :: structs, enums, typedefs, classes)
                | CppParser.Declaration.Enum _ -> (funcs, structs, decl :: enums, typedefs, classes)
                | CppParser.Declaration.Typedef _ -> (funcs, structs, enums, decl :: typedefs, classes)
                | CppParser.Declaration.Class _ -> (funcs, structs, enums, typedefs, decl :: classes)
                | CppParser.Declaration.Namespace ns ->
                    // Extract declarations from namespaces
                    let extractDecls (decls: CppParser.Declaration list) =
                        decls |> List.fold (fun (f, s, e, t, c) d ->
                            match d with
                            | CppParser.Declaration.Function _ -> (d :: f, s, e, t, c)
                            | CppParser.Declaration.Struct _ -> (f, d :: s, e, t, c)
                            | CppParser.Declaration.Enum _ -> (f, s, d :: e, t, c)
                            | CppParser.Declaration.Typedef _ -> (f, s, e, d :: t, c)
                            | CppParser.Declaration.Class _ -> (f, s, e, t, d :: c)
                            | CppParser.Declaration.Namespace ns2 ->
                                let (f2, s2, e2, t2, c2) = extractDecls ns2.Declarations
                                (f2 @ f, s2 @ s, e2 @ e, t2 @ t, c2 @ c)
                        ) (funcs, structs, enums, typedefs, classes)
                    
                    extractDecls ns.Declarations
            ) ([], [], [], [], [])
            
        // 1. Generate Types.fs - Contains all type definitions
        let typesFile = 
            let sb = StringBuilder()
            sb.AppendLine($"namespace {namespace'}") |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine("open System") |> ignore
            sb.AppendLine("open System.Runtime.InteropServices") |> ignore
            sb.AppendLine() |> ignore
            
            sb.AppendLine("/// Native type definitions") |> ignore
            sb.AppendLine("[<AutoOpen>]") |> ignore
            sb.AppendLine("module Types =") |> ignore
            
            // Add enums
            if not enums.IsEmpty then
                for enum in enums do
                    sb.AppendLine($"    {generateEnum enum}") |> ignore
                    sb.AppendLine() |> ignore
                    
            // Add structs as raw types
            if not structs.IsEmpty then
                for struct' in structs do
                    sb.AppendLine($"    {generateStruct struct' typeMap}") |> ignore
                    sb.AppendLine() |> ignore
                    
            // Add typedefs
            if not typedefs.IsEmpty then
                for typedef in typedefs do
                    sb.AppendLine($"    {generateTypedef typedef typeMap}") |> ignore
                    sb.AppendLine() |> ignore
                    
            // Add classes as raw types
            if not classes.IsEmpty then
                for class' in classes do
                    sb.AppendLine($"    {generateClass class' libraryName typeMap}") |> ignore
                    sb.AppendLine() |> ignore
            
            sb.ToString()
            
        // 2. Generate Bindings.fs - Contains P/Invoke declarations
        let bindingsFile =
            let sb = StringBuilder()
            sb.AppendLine($"namespace {namespace'}") |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine("open System") |> ignore
            sb.AppendLine("open System.Runtime.InteropServices") |> ignore
            sb.AppendLine("open System.Text") |> ignore
            sb.AppendLine($"open {namespace'}.Types") |> ignore
            sb.AppendLine() |> ignore
            
            sb.AppendLine("/// Native function bindings") |> ignore
            sb.AppendLine($"module {libraryName} =") |> ignore
            
            // Add function declarations
            if not functions.IsEmpty then
                for func in functions do
                    sb.AppendLine(generatePInvoke func libraryName typeMap) |> ignore
                    sb.AppendLine() |> ignore
                    
            sb.ToString()
            
        // 3. Generate Wrappers.fs - Contains idiomatic F# wrappers
        let wrappersFile = 
            let sb = StringBuilder()
            sb.AppendLine($"namespace {namespace'}") |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine("open System") |> ignore
            sb.AppendLine("open System.Runtime.InteropServices") |> ignore
            sb.AppendLine($"open {namespace'}.Types") |> ignore
            sb.AppendLine($"open {namespace'}") |> ignore
            sb.AppendLine() |> ignore
            
            sb.AppendLine("/// F# wrapper functions and classes") |> ignore
            sb.AppendLine("[<AutoOpen>]") |> ignore
            sb.AppendLine("module Wrappers =") |> ignore
            
            // Add function wrappers
            if not functions.IsEmpty then
                for func in functions do
                    sb.AppendLine($"    {generateWrapperFunction func libraryName typeMap}") |> ignore
                    sb.AppendLine() |> ignore
                    
            // Add class wrappers
            if not structs.IsEmpty then
                for struct' in structs do
                    sb.AppendLine($"    {generateWrapperClass struct' libraryName typeMap}") |> ignore
                    sb.AppendLine() |> ignore
                    
            if not classes.IsEmpty then
                for class' in classes do
                    sb.AppendLine($"    {generateWrapperClass class' libraryName typeMap}") |> ignore
                    sb.AppendLine() |> ignore
                    
            sb.ToString()
            
        // 4. Generate Delegates.fs - Contains delegate type definitions and conversions
        let delegatesFile =
            let sb = StringBuilder()
            let functionPointers = DelegatePointer.identifyFunctionPointers declarations
            
            sb.AppendLine($"namespace {namespace'}") |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine("open System") |> ignore
            sb.AppendLine("open System.Runtime.InteropServices") |> ignore
            sb.AppendLine($"open {namespace'}.Types") |> ignore
            sb.AppendLine() |> ignore
            
            sb.AppendLine("/// Delegate types for function pointers") |> ignore
            sb.AppendLine("module Delegates =") |> ignore
            
            // Add delegate type declarations
            let delegateTypes = DelegatePointer.generateDelegateTypes functionPointers
            sb.AppendLine(delegateTypes) |> ignore
            sb.AppendLine() |> ignore
            
            // Add delegate wrapper functions
            let delegateWrappers = DelegatePointer.generateDelegateWrappers functionPointers
            sb.AppendLine(delegateWrappers) |> ignore
            sb.AppendLine() |> ignore
            
            // Add delegate unwrapper functions
            let delegateUnwrappers = DelegatePointer.generateDelegateUnwrappers functionPointers
            sb.AppendLine(delegateUnwrappers) |> ignore
            
            sb.ToString()
            
        // 5. Generate Memory.fs - Contains memory management utilities
        let memoryFile =
            let sb = StringBuilder()
            let structTypes = 
                structs 
                |> List.choose (fun s -> 
                    match s with 
                    | CppParser.Declaration.Struct s -> Some s.Name 
                    | _ -> None)
                    
            sb.AppendLine($"namespace {namespace'}")  |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine("open System") |> ignore
            sb.AppendLine("open System.Runtime.InteropServices") |> ignore
            sb.AppendLine("open System.Runtime.CompilerServices") |> ignore
            sb.AppendLine($"open {namespace'}.Types") |> ignore
            sb.AppendLine() |> ignore
            
            sb.AppendLine("/// Memory management utilities") |> ignore
            sb.AppendLine("module Memory =") |> ignore
            
            // Add memory management functions
            let memoryManagement = MemoryManager.generateMemoryManagement structTypes
            sb.AppendLine(memoryManagement) |> ignore
            
            sb.ToString()
            
        // 6. Generate Extensions.fs - Contains extension methods
        let extensionsFile =
            let sb = StringBuilder()
            
            sb.AppendLine($"namespace {namespace'}")  |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine("open System") |> ignore
            sb.AppendLine($"open {namespace'}.Types") |> ignore
            sb.AppendLine($"open {namespace'}.Wrappers") |> ignore
            sb.AppendLine() |> ignore
            
            sb.AppendLine("/// Extension methods and additional utilities") |> ignore
            sb.AppendLine("module Extensions =") |> ignore
            
            // Add example extension method
            sb.AppendLine("    /// Example extension method - Replace with actual extensions") |> ignore
            sb.AppendLine("    type System.String with") |> ignore
            sb.AppendLine("        /// Converts a string to a native handle using UTF-8 encoding") |> ignore
            sb.AppendLine("        member this.ToNativeUtf8() =") |> ignore
            sb.AppendLine("            let bytes = System.Text.Encoding.UTF8.GetBytes(this + \"\\0\")")  |> ignore
            sb.AppendLine("            let handle = Marshal.AllocHGlobal(bytes.Length)")  |> ignore
            sb.AppendLine("            Marshal.Copy(bytes, 0, handle, bytes.Length)")  |> ignore
            sb.AppendLine("            handle")  |> ignore
            
            sb.ToString()
            
        // Return all generated code sections
        { Sections = [
            { FileName = "Types.fs"; Content = typesFile; Order = 1 }
            { FileName = "Bindings.fs"; Content = bindingsFile; Order = 2 }
            { FileName = "Memory.fs"; Content = memoryFile; Order = 3 }
            { FileName = "Delegates.fs"; Content = delegatesFile; Order = 4 }
            { FileName = "Wrappers.fs"; Content = wrappersFile; Order = 5 }
            { FileName = "Extensions.fs"; Content = extensionsFile; Order = 6 }
          ] }