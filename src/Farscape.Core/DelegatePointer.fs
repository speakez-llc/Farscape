namespace Farscape.Core

open System
open System.Text
open System.Runtime.InteropServices

/// Module for handling delegate pointers and callbacks
module DelegatePointer =
    /// Represents a function pointer signature
    type FunctionPointerSignature = {
        ReturnType: string
        ParameterTypes: string list
        CallingConvention: CallingConvention
    }
    
    /// Represents a delegate type definition
    type DelegateTypeDefinition = {
        Name: string
        Signature: FunctionPointerSignature
        Documentation: string option
    }
    
    /// Extract function pointer signature from a C++ type
    let extractFunctionPointerSignature (cppType: string) : FunctionPointerSignature option =
        // Function pointer pattern: return_type (*)(param1, param2, ...)
        let pattern = @"([\w\s\*]+)\s*\(\*\)\s*\(([\w\s\*,]*)\)"
        let regex = System.Text.RegularExpressions.Regex(pattern)
        let match' = regex.Match(cppType)
        
        if match'.Success then
            let returnType = match'.Groups.[1].Value.Trim()
            let paramStr = match'.Groups.[2].Value.Trim()
            
            let paramTypes = 
                if String.IsNullOrWhiteSpace(paramStr) then
                    []
                else
                    paramStr.Split(',')
                    |> Array.map (fun s -> s.Trim())
                    |> Array.toList
                    
            Some {
                ReturnType = returnType
                ParameterTypes = paramTypes
                CallingConvention = CallingConvention.Cdecl
            }
        else
            None
    
    /// Generate a delegate type declaration for a function pointer
    let generateDelegateType (signature: FunctionPointerSignature) (name: string) (documentation: string option) =
        let parameters = 
            signature.ParameterTypes
            |> List.mapi (fun i paramType -> 
                let fsharpType = TypeMapper.getFSharpType paramType
                $"arg{i}: {fsharpType}")
            |> String.concat ", "
            
        let returnType = TypeMapper.getFSharpType signature.ReturnType
        
        let docComment = 
            match documentation with
            | None -> ""
            | Some text ->
                let lines = text.Split([|'\n'|], StringSplitOptions.RemoveEmptyEntries)
                let sb = StringBuilder()
                sb.AppendLine("/// <summary>") |> ignore
                for line in lines do
                    sb.AppendLine($"/// {line.Trim()}") |> ignore
                sb.AppendLine("/// </summary>") |> ignore
                sb.ToString()
        
        let callingConvention = signature.CallingConvention.ToString()
        
        $"{docComment}[<UnmanagedFunctionPointer(CallingConvention.{callingConvention})>]\ntype {name} = delegate of {parameters} -> {returnType}"
    
    /// Generate a function to wrap a managed delegate as a function pointer
    let generateDelegateWrapper (delegateType: string) =
        $"let wrap{delegateType} (func: {delegateType}) : nativeint =\n    Marshal.GetFunctionPointerForDelegate(func) |> nativeint"
    
    /// Generate a function to unwrap a function pointer to a managed delegate
    let generateDelegateUnwrapper (delegateType: string) =
        $"let unwrap{delegateType} (ptr: nativeint) : {delegateType} =\n    Marshal.GetDelegateForFunctionPointer(ptr, typeof<{delegateType}>) :?> {delegateType}"
    
    /// Identify function pointer types in declarations
    let identifyFunctionPointers (declarations: CppParser.Declaration list) : DelegateTypeDefinition list =
        let rec processDeclarations (decls: CppParser.Declaration list) =
            let mutable delegates = []
    
            // Helper to process parameters
            let processParameters (parameters: (string * string) list) =
                for (name, paramType) in parameters do
                    match extractFunctionPointerSignature paramType with
                    | Some signature ->
                        let delegateName = $"{name}Delegate"
                        delegates <- { Name = delegateName; Signature = signature; Documentation = None } :: delegates
                    | None -> ()
    
            for decl in decls do
                match decl with
                | CppParser.Declaration.Function f ->
                    processParameters f.Parameters
    
                    // Check if return type is a function pointer
                    match extractFunctionPointerSignature f.ReturnType with
                    | Some signature ->
                        let delegateName = $"{f.Name}ReturnDelegate"
                        delegates <- { Name = delegateName; Signature = signature; Documentation = None } :: delegates
                    | None -> ()
    
                | CppParser.Declaration.Struct s ->
                    for (fieldName, fieldType) in s.Fields do
                        match extractFunctionPointerSignature fieldType with
                        | Some signature ->
                            let delegateName = $"{s.Name}_{fieldName}Delegate"
                            delegates <- { Name = delegateName; Signature = signature; Documentation = None } :: delegates
                        | None -> ()
    
                | CppParser.Declaration.Class c ->
                    processDeclarations c.Methods |> ignore
    
                    for (fieldName, fieldType) in c.Fields do
                        match extractFunctionPointerSignature fieldType with
                        | Some signature ->
                            let delegateName = $"{c.Name}_{fieldName}Delegate"
                            delegates <- { Name = delegateName; Signature = signature; Documentation = None } :: delegates
                        | None -> ()
    
                | CppParser.Declaration.Namespace ns ->
                    delegates <- processDeclarations ns.Declarations @ delegates
    
                | _ -> ()
    
            List.distinctBy (fun d -> d.Name) delegates
    
        processDeclarations declarations
        
    /// Generate delegate type declarations for function pointers
    let generateDelegateTypes (functionPointers: DelegateTypeDefinition list) =
        functionPointers
        |> List.map (fun fp -> 
            generateDelegateType fp.Signature fp.Name fp.Documentation)
        |> String.concat "\n\n"
    
    /// Generate delegate wrapper functions
    let generateDelegateWrappers (functionPointers: DelegateTypeDefinition list) =
        functionPointers
        |> List.map (fun fp -> generateDelegateWrapper fp.Name)
        |> String.concat "\n\n"
    
    /// Generate delegate unwrapper functions
    let generateDelegateUnwrappers (functionPointers: DelegateTypeDefinition list) =
        functionPointers
        |> List.map (fun fp -> generateDelegateUnwrapper fp.Name)
        |> String.concat "\n\n"