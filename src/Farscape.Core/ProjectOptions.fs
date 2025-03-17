namespace Farscape.Core

module ProjectOptions =

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

