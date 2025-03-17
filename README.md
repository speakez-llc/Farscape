# Farscape

# Farscape: F# Native Library Binding Generator

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Farscape is a command-line tool that automatically generates idiomatic F# bindings for C++ libraries. It leverages LibClang through CppSHarp to parse C++ headers and produces F# code that can be directly used in F# applications.

## Features

- **C++ Header Parsing**: Uses CppSharp/LibClang to accurately parse C++ header files
- **Idiomatic F# Code Generation**: Generates clean, idiomatic F# code
- **P/Invoke Support**: Automatically creates proper P/Invoke declarations for native functions
- **Type Mapping**: Smart mapping between C++ and F# types
- **Project Generation**: Creates complete F# projects ready for building
- **Documentation**: Preserves C++ documentation as F# XML docs

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- [LLVM/Clang](https://releases.llvm.org/download.html) with development components (for LibClang)

## Installation

### From Source

```bash
# Clone the repository
git clone https://github.com/yourusername/farscape.git
cd farscape

# Build the project
./build.ps1

# Install as a global tool
dotnet tool install --global --add-source ./src/Farscape.Cli/nupkg farscape
```

## Usage

```bash
# Basic usage
farscape --header path/to/header.h --library libname

# With additional options
farscape --header path/to/header.h \
         --library libname \
         --output ./output \
         --namespace MyCompany.Bindings \
         --include-paths /usr/include,/usr/local/include \
         --verbose
```

### Command Line Options

| Option | Description |
|--------|-------------|
| `--header`, `-h` | Path to the C++ header file (required) |
| `--library`, `-l` | Name of the native library to bind to (required) |
| `--output`, `-o` | Output directory for the generated F# project (default: ./output) |
| `--namespace`, `-n` | Namespace prefix for the generated F# code (default: NativeBindings) |
| `--include-paths`, `-i` | Additional include paths for C++ header parsing (comma separated) |
| `--verbose`, `-v` | Enable verbose output |

## Examples

### Basic Example

Assume you have a simple C library with a header like this:

```c
// math_lib.h
#pragma once

#ifdef __cplusplus
extern "C" {
#endif

// Adds two integers
int add(int a, int b);

// Multiplies two doubles
double multiply(double a, double b);

#ifdef __cplusplus
}
#endif
```

Generate F# bindings with:

```bash
farscape --header math_lib.h --library mathlib
```

The generated F# code will look like:

```fsharp
namespace NativeBindings

open System
open System.Runtime.InteropServices

module NativeBindings =
    /// <summary>
    /// Adds two integers
    /// </summary>
    [<DllImport("mathlib", CallingConvention = CallingConvention.Cdecl)>]
    extern int add(int a, int b)
    
    /// <summary>
    /// Multiplies two doubles
    /// </summary>
    [<DllImport("mathlib", CallingConvention = CallingConvention.Cdecl)>]
    extern double multiply(double a, double b)
```

### Complex Example

Farscape can handle more complex scenarios including:

- Classes and structs
- Enums
- Templates (with limitations)
- Namespaces
- Function pointers
- Various calling conventions

## Advanced Topics

### Self-Hosting

Farscape can be used to generate F# bindings for LibClang itself, creating a self-hosting cycle.

### MLIR/LLVM Integration

The architecture is designed to potentially support MLIR/LLVM lowering in the future, enabling compilation to native code without the .NET runtime.

### Delegate Pointer Handling

Special handling for C++ function pointers and delegates is included, with support for marshaling between F# functions and C++ callbacks.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- [CppSharp](https://github.com/mono/CppSharp) for its deep coverage of LibClang
- [LLVM/Clang](https://llvm.org/) project for LibClang
- [XParsec](https://github.com/roboz0r/XParsec) for rich parsing of CppSharp and LibClang outputs
- F# community for inspiration and support
- .NET runtime for P/Invoke support
