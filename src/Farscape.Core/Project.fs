fsharp
/// Generate a solution file
let generateSolutionFile (options: ProjectOptions) (solutionPath: string) =
    let libraryProjectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant()
    let testsProjectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant()
    let solutionGuid = Guid.NewGuid().ToString("B").ToUpperInvariant()
    let srcFolderGuid = Guid.NewGuid().ToString("B").ToUpperInvariant()
    let testsFolderGuid = Guid.NewGuid().ToString("B").ToUpperInvariant()
    let projectTypeGuid = Guid.NewGuid().ToString("B").ToUpperInvariant()

    let sb = StringBuilder()

    sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00") |> ignore
    sb.AppendLine("# Visual Studio Version 17") |> ignore
    sb.AppendLine("VisualStudioVersion = 17.0.31903.59") |> ignore
    sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1") |> ignore

    // Add solution folders
    sb.AppendLine($"Project(\"{{2150E333-8FDC-42A3-9474-1A3956D46DE8}}\") = \"src\", \"src\", \"{{{srcFolderGuid}}}\"") |> ignore
    sb.AppendLine("EndProject") |> ignore
    sb.AppendLine($"Project(\"{{2150E333-8FDC-42A3-9474-1A3956D46DE8}}\") = \"tests\", \"tests\", \"{{{testsFolderGuid}}}\"") |> ignore
    sb.AppendLine("EndProject") |> ignore

    // Add projects
    sb.AppendLine($"Project(\"{{{projectTypeGuid}}}\") = \"{options.LibraryName}\", \"src\\{options.LibraryName}\\{options.LibraryName}.fsproj\", \"{libraryProjectGuid}\"") |> ignore
    sb.AppendLine("EndProject") |> ignore
    sb.AppendLine($"Project(\"{{{projectTypeGuid}}}\") = \"{options.LibraryName}.Tests\", \"tests\\{options.LibraryName}.Tests\\{options.LibraryName}.Tests.fsproj\", \"{testsProjectGuid}\"") |> ignore
    sb.AppendLine("EndProject") |> ignore

    // Add solution configurations
    sb.AppendLine("Global") |> ignore
    sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution") |> ignore
    sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU") |> ignore
    sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU") |> ignore
    sb.AppendLine("\tEndGlobalSection") |> ignore
    sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution") |> ignore
    sb.AppendLine($"\t\t{libraryProjectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU") |> ignore
    sb.AppendLine($"\t\t{libraryProjectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU") |> ignore
    sb.AppendLine($"\t\t{libraryProjectGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU") |> ignore
    sb.AppendLine($"\t\t{libraryProjectGuid}.Release|Any CPU.Build.0 = Release|Any CPU") |> ignore
    sb.AppendLine($"\t\t{testsProjectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU") |> ignore
    sb.AppendLine($"\t\t{testsProjectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU") |> ignore
    sb.AppendLine($"\t\t{testsProjectGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU") |> ignore
    sb.AppendLine($"\t\t{testsProjectGuid}.Release|Any CPU.Build.0 = Release|Any CPU") |> ignore
    sb.AppendLine("\tEndGlobalSection") |> ignore
    sb.AppendLine("\tGlobalSection(SolutionProperties) = preSolution") |> ignore
    sb.AppendLine("\t\tHideSolutionNode = FALSE") |> ignore
    sb.AppendLine("\tEndGlobalSection") |> ignore
    sb.AppendLine("\tGlobalSection(NestedProjects) = preSolution") |> ignore
    sb.AppendLine($"\t\t{libraryProjectGuid} = {{{srcFolderGuid}}}") |> ignore
    sb.AppendLine($"\t\t{testsProjectGuid} = {{{testsFolderGuid}}}") |> ignore
    sb.AppendLine("\tEndGlobalSection") |> ignore
    sb.AppendLine("EndGlobal") |> ignore

    File.WriteAllText(solutionPath, sb.ToString())