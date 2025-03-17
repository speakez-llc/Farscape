namespace Farscape.Core

open System
open System.Text
open System.Runtime.InteropServices

/// Module to generate F# code for bindings
module CodeGenerator =
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
    
    /// Generate the entire F# code for bindings
    let generateCode (declarations: CppParser.Declaration list) (namespace': string) (libraryName: string) =
        let typesList = TypeMapper.mapTypes declarations
        let typeMap = typesList |> List.map (fun t -> t.OriginalName, t) |> Map.ofList
        
        let sb = StringBuilder()
        
        // Add header
        sb.AppendLine($"namespace {namespace'}") |> ignore
        sb.AppendLine() |> ignore
        sb.AppendLine("open System") |> ignore
        sb.AppendLine("open System.Runtime.InteropServices") |> ignore
        sb.AppendLine("open System.Text") |> ignore
        sb.AppendLine() |> ignore
        
        // Add module
        sb.AppendLine($"module {libraryName} =") |> ignore
        
        // Add declarations
        for decl in declarations do
            sb.AppendLine() |> ignore
            sb.AppendLine(generateDeclaration decl libraryName typeMap) |> ignore
            
        sb.ToString()