namespace Farscape.Core

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

/// Module for handling memory management and pinning
module MemoryManager =
    /// Generate code for pinned allocation of struct array
    let generatePinnedStructArray (structName: string) (elementSize: int) =
        $"""
/// Allocates a pinned array of {structName} on the pinned object heap
let allocatePinned{structName}Array (count: int) : {structName}[] =
    let array = GC.AllocateArray<{structName}>(count, true)
    array
        """
        
    /// Generate code for getting a pointer to a pinned array
    let generateGetPinnedArrayPointer (structName: string) =
        $"""
/// Gets a pointer to the first element of a pinned {structName} array
let get{structName}ArrayPointer (array: {structName}[]) : nativeint =
    GCHandle.Alloc(array, GCHandleType.Pinned).AddrOfPinnedObject()
        """
        
    /// Generate code for memory marshaling
    let generateMemoryCast (sourceType: string) (targetType: string) =
        $"""
/// Cast a Span<{sourceType}> to a Span<{targetType}>
let cast{sourceType}To{targetType} (source: Span<{sourceType}>) : Span<{targetType}> =
    MemoryMarshal.Cast<{sourceType}, {targetType}>(source)
        """
        
    /// Generate code for a fixed statement equivalent in F#
    let generateFixedStatement (structName: string) =
        $"""
/// Performs an operation with a fixed pointer to a {structName}
let fixed{structName} (value: {structName}) (action: nativeint -> 'T) : 'T =
    let valueArr = [|value|]
    let handle = GCHandle.Alloc(valueArr, GCHandleType.Pinned)
    try
        let ptr = handle.AddrOfPinnedObject()
        action ptr
    finally
        handle.Free()
        """
        
    /// Generate code for creating a Span from a pointer
    let generateSpanFromPointer (structName: string) =
        $"""
/// Creates a Span<{structName}> from a pointer and length
let spanFrom{structName}Pointer (ptr: nativeint) (length: int) : Span<{structName}> =
    MemoryMarshal.CreateSpan(ref Unsafe.AsRef<{structName}>(ptr.ToPointer()), length)
        """
        
    /// Generate code for freeing native memory
    let generateFreeNativeMemory =
        """
/// Frees memory allocated with Marshal.AllocHGlobal
let freeNativeMemory (ptr: nativeint) : unit =
    if ptr <> IntPtr.Zero then
        Marshal.FreeHGlobal(ptr)
        """
        
    /// Generate code for allocating native memory
    let generateAllocateNativeMemory =
        """
/// Allocates memory using Marshal.AllocHGlobal
let allocateNativeMemory (size: int) : nativeint =
    Marshal.AllocHGlobal(size)
        """
        
    /// Generate code for copying to native memory
    let generateCopyToNativeMemory (structName: string) =
        $"""
/// Copies a {structName} to native memory
let copyToNativeMemory (value: {structName}) : nativeint =
    let size = Marshal.SizeOf<{structName}>()
    let ptr = Marshal.AllocHGlobal(size)
    Marshal.StructureToPtr(value, ptr, false)
    ptr
        """
        
    /// Generate code for copying from native memory
    let generateCopyFromNativeMemory (structName: string) =
        $"""
/// Copies a {structName} from native memory
let copyFromNativeMemory (ptr: nativeint) : {structName} =
    Marshal.PtrToStructure<{structName}>(ptr)
        """
        
    /// Generate code for using Marshal with an array of structs
    let generateMarshalStructArray (structName: string) =
        $"""
/// Copies an array of {structName} to native memory
let marshalStructArray (array: {structName}[]) : nativeint =
    let elementSize = Marshal.SizeOf<{structName}>()
    let totalSize = elementSize * array.Length
    let ptr = Marshal.AllocHGlobal(totalSize)
    
    for i = 0 to array.Length - 1 do
        let elementPtr = IntPtr.op_Addition(ptr, i * elementSize)
        Marshal.StructureToPtr(array.[i], elementPtr, false)
        
    ptr
        """
        
    /// Generate the entire memory management module for a set of struct types
    let generateMemoryManagement (structTypes: string list) =
        let sb = System.Text.StringBuilder()
        
        // Add standard memory management functions
        sb.AppendLine(generateAllocateNativeMemory) |> ignore
        sb.AppendLine() |> ignore
        sb.AppendLine(generateFreeNativeMemory) |> ignore
        sb.AppendLine() |> ignore
        
        // Add type-specific memory management functions
        for structType in structTypes do
            sb.AppendLine(generatePinnedStructArray structType 0) |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine(generateGetPinnedArrayPointer structType) |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine(generateFixedStatement structType) |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine(generateCopyToNativeMemory structType) |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine(generateCopyFromNativeMemory structType) |> ignore
            sb.AppendLine() |> ignore
            sb.AppendLine(generateMarshalStructArray structType) |> ignore
            sb.AppendLine() |> ignore
        
        sb.ToString()