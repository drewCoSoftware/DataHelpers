function Item-Exists($path) {

    $x = Get-Item -Path $path -ErrorAction SilentlyContinue
    return $x.Length -gt 0
}


# Get a clean copy of the build dir.
if (Item-Exists("nuget-build")) {
    Remove-Item "nuget-build" -Force -Recurse
}
New-Item "nuget-build" -ItemType "Directory"
New-Item "nuget-build\lib" -ItemType "Directory"
New-Item "nuget-build\lib\net8.0" -ItemType "Directory"



# Build our assemblies:
dotnet build DataHelpers.sln -c Release

# Copy the appropriate files to the nuget dir....
$fromDir = "DataHelpers\bin\Release\net8.0\"
$toDir = "nuget-build\lib\net8.0\"
Copy-Item ($fromDir + "DataHelpers.dll") ($toDir + "DataHelpers.dll")
Copy-Item ($fromDir + "DataHelpers.pdb") ($toDir + "DataHelpers.pdb")
Copy-Item ($fromDir + "DataHelpers.xml") ($toDir + "DataHelpers.xml")

# exit


# Copy-Item 

# Copy everything to the build dir....
# Copy-Item -Path ".\lib" ".\nuget-build\lib" -Recurse
Copy-Item ".\DataHelpers.nuspec" ".\nuget-build\DataHelpers.nuspec"

# Pack it all up...
nuget pack ".\nuget-build\DataHelpers.nuspec"