#I @"packages\FAKE.1.58.3\tools"
#r "FakeLib.dll"
open Fake
 
// Properties
let buildDir = @".\build\"
let nugetDir = @"..\nugetpackage" 

let appReferences  = !! @"PicoMvc\PicoMvc.fsproj"
 
Target "Clean" (fun _ ->
    CleanDirs [buildDir]
)

Target "BuildApp" (fun _ ->                     
    MSBuildDebug buildDir "Build" appReferences
        |> Log "AppBuild-Output: "
)


Target "CreateNuGet" (fun _ -> 
    let nugetLibsDir = nugetDir @@ @"lib\net40"
    XCopy (buildDir @@ @"Strangelights.PicoMvc.dll") nugetLibsDir
    XCopy (buildDir @@ @"Strangelights.PicoMvc.pdb") nugetLibsDir

    NuGet (fun p -> 
        {p with               
            Authors = ["Robert Pickering"]
            Project = "PicoMvc"
            Description = "A thin veneer of F#ness arround several different frameworks to make a light weight Mvc framework."
            Version = getBuildParam "version"
            OutputPath = nugetDir
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey" }) @"..\nugetpackage\PicoMvc.nuspec"
)

"Clean"
    ==> "BuildApp"
    ==> "CreateNuGet"


RunParameterTargetOrDefault "target" "BuildApp"