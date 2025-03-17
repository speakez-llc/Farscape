namespace Farscape.Core

open System
open System.Runtime.InteropServices
open System.Runtime.CompilerServices

module MemoryManager =

    let generatePinnedStructArray (structName: string) (elementSize: int) =
        $"""
/// Allocates a pinned array of {structName} on the pinned object heap
let allocatePinned{structName}Array (count: int) : {structName}[] =
    let array = GC.AllocateArray<{structName}>(count, true)
    array
        """
        
    let generateGetPinnedArrayPointer (structName: string) =
        $"""
/// Gets a pointer to the first element of a pinned {structName} array
let get{structName}ArrayPointer (array: {structName}[]) : nativeint =
    GCHandle.Alloc(array, GCHandleType.Pinned).AddrOfPinnedObject()
        """

    let generateMemoryCast (sourceType: string) (targetType: string) =
        $"""
/// Cast a Span<{sourceType}> to a Span<{targetType}>
let cast{sourceType}To{targetType} (source: Span<{sourceType}>) : Span<{targetType}> =
    MemoryMarshal.Cast<{sourceType}, {targetType}>(source)
        """

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

    let generateSpanFromPointer (structName: string) =
        $"""
/// Creates a Span<{structName}> from a pointer and length
let spanFrom{structName}Pointer (ptr: nativeint) (length: int) : Span<{structName}> =
    MemoryMarshal.CreateSpan(ref Unsafe.AsRef<{structName}>(ptr.ToPointer()), length)
        """

    let generateFreeNativeMemory =
        """
/// Frees memory allocated with Marshal.AllocHGlobal
let freeNativeMemory (ptr: nativeint) : unit =
    if ptr <> IntPtr.Zero then
        Marshal.FreeHGlobal(ptr)
        """

    let generateAllocateNativeMemory =
        """
/// Allocates memory using Marshal.AllocHGlobal
let allocateNativeMemory (size: int) : nativeint =
    Marshal.AllocHGlobal(size)
        """

    let generateCopyToNativeMemory (structName: string) =
        $"""
/// Copies a {structName} to native memory
let copyToNativeMemory (value: {structName}) : nativeint =
    let size = Marshal.SizeOf<{structName}>()
    let ptr = Marshal.AllocHGlobal(size)
    Marshal.StructureToPtr(value, ptr, false)
    ptr
        """

    let generateCopyFromNativeMemory (structName: string) =
        $"""
/// Copies a {structName} from native memory
let copyFromNativeMemory (ptr: nativeint) : {structName} =
    Marshal.PtrToStructure<{structName}>(ptr)
        """

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