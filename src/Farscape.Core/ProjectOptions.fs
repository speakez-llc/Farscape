namespace Farscape.Core

module ProjectOptions =

    /// Project options for generating the F# project file
    type ProjectOptions = {
        ProjectName: string
        Namespace: string
        OutputDirectory: string
        References: string list
        NuGetPackages: (string * string) list
        HeaderFile: string
        LibraryName: string
        IncludePaths: string list
        Verbose: bool
    } 

