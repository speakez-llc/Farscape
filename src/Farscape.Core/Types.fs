namespace Farscape.Core

/// Common types used across the library
module Types =
    /// Status code for operations
    type OperationStatus =
        | Success = 0
        | Error = 1
        | InvalidArgument = 2
        | NotImplemented = 3
        | NotSupported = 4
        | MemoryError = 5
        | TimeoutError = 6