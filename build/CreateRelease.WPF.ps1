# When using System.IO.Compression.ZipFile.CreateFromDirectory in PowerShell, it still uses backslashes in the zip paths
# despite this https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/mitigation-ziparchiveentry-fullname-path-separator

# Based upon post by Seth Jackson https://sethjackson.github.io/2016/12/17/path-separators/

#
# PowerShell 5 (WMF5) & 6
# Using class Keyword https://msdn.microsoft.com/powershell/reference/5.1/Microsoft.PowerShell.Core/about/about_Classes
#
# https://gist.github.com/lantrix/738ebfa616d5222a8b1db947793bc3fc
#

####################################
###        Fix Zip slash         ###
####################################
Add-Type -AssemblyName System.Text.Encoding
Add-Type -AssemblyName System.IO.Compression.FileSystem

class FixedEncoder : System.Text.UTF8Encoding {
    FixedEncoder() : base($true) { }

    [byte[]] GetBytes([string] $s)
    {
        $s = $s.Replace("\", "/");
        return ([System.Text.UTF8Encoding]$this).GetBytes($s);
    }
}


function wixCleanUpElement([System.Xml.XmlElement]$element, [string]$rootPath)
{
    $files = Get-ChildItem -Path $rootPath -File -Force -ErrorAction SilentlyContinue
    $folders = Get-ChildItem -Path $rootPath -Directory -Force -ErrorAction SilentlyContinue
    
    # Removing not existant files
    foreach($e in $element.Component)
    {
        $found = $false
        foreach($file in $files)
        {
            if($e.File.Source.EndsWith("\$($file.Name)")){
                $found = $true
                break
            }
        }

        if ($found -eq $false){
            # File not found on this directory, remove element
            Write-Host("WIX: File '$($e.File.Source)' does not exist anymore, removing...")
            $e.ParentNode.RemoveChild($e) | Out-Null
        }
    }

    # Adding not existant files
    foreach($file in $files)
    {
        $found = $false
        foreach($e in $element.Component)
        {
            if($null -ne $e.File.Source -and $e.File.Source.EndsWith("\$($file.Name)")){
                $found = $true
                break
            }
        }

        if ($found -eq $false){
            # File not found on manifest, add element
            $guid = [guid]::NewGuid().ToString().ToUpper()
            $guidNoSeparator = $guid.Replace('-', '')
            $filePath = $file.FullName.Substring($msiSourceFiles.Length)
            while ($filePath.StartsWith('\'))
            {
                $filePath = $filePath.Remove(0, 1)
            }
            Write-Host("WIX: File '$filePath' does not exist on manifest, adding with GUID: $guid")
            $xmlComponent = $element.OwnerDocument.CreateElement("Component", $element.OwnerDocument.DocumentElement.NamespaceURI)
            $xmlComponent.SetAttribute("Id", "owc$guidNoSeparator")
            $xmlComponent.SetAttribute("Guid", $guid)

            $xmlFile = $element.OwnerDocument.CreateElement("File", $element.OwnerDocument.DocumentElement.NamespaceURI)
            $xmlFile.SetAttribute("Id", "owf$guidNoSeparator")
            $xmlFile.SetAttribute("Source", "`$(var.SourceDir)\$filePath")
            $xmlFile.SetAttribute("KeyPath", 'yes')

            $xmlComponent.AppendChild($xmlFile) | Out-Null
            $element.AppendChild($xmlComponent) | Out-Null
        }
    }
    
    # Removing not existant folters
    foreach($e in $element.Directory)
    {
        $found = $false
        foreach($folder in $folders)
        {
            if($e.Name -eq $folder.Name){
                $found = $true
                break
            }
        }

        if ($found -eq $false){
            # Directory not found on this directory, remove element
            Write-Host("WIX: Directory '$($e.Name)' does not exist anymore, removing...")
            $e.ParentNode.RemoveChild($e) | Out-Null
        }
    }
    
    # Adding not existant directories
    foreach($folder in $folders)
    {
        $foundDirectoryElement = $null
        foreach($e in $element.Directory)
        {
            #Write-Host("$($e.Name) == $($folder.Name)")
            if($null -ne $e.Name -and $e.Name -eq $folder.Name){
                $foundDirectoryElement = $e
                break
            }
        }

        if ($null -eq $foundDirectoryElement){
            # Folder not found on manifest, add element
            $guid = [guid]::NewGuid().ToString().ToUpper()
            $guidNoSeparator = $guid.Replace('-', '')

            $directoryRelativePath = $folder.FullName.Substring($msiSourceFiles.Length)
            while ($directoryRelativePath.StartsWith('\'))
            {
                $directoryRelativePath = $directoryRelativePath.Remove(0, 1)
            }

            Write-Host("WIX: Directory '$directoryRelativePath' does not exist on manifest, adding with GUID: $guid")
            $xmlDirectory = $element.OwnerDocument.CreateElement("Directory", $element.OwnerDocument.DocumentElement.NamespaceURI)
            $xmlDirectory.SetAttribute("Id", "owd$guidNoSeparator")
            $xmlDirectory.SetAttribute("Name", $folder.Name)
            $element.AppendChild($xmlDirectory) | Out-Null
            
            wixCleanUpElement $xmlDirectory $folder.FullName
        }else{
            # Folder found on manifest, recursive from this folder
            wixCleanUpElement $foundDirectoryElement $folder.FullName
        }
    }
}


<#
$msiComponentsXml = [Xml] (Get-Content $msiComponentsFile)
foreach($element in $msiComponentsXml.Wix.Module.Directory.Directory)
{
    if($element.Id -eq 'MergeRedirectFolder')
    {
        wixCleanUpElement $element $msiSourceFiles
        #WriteXmlToScreen($msiComponentsXml);
        #$msiComponentsXml.Save("$rootFolder\$publishFolder\test.xml")
        return
        break
    }
}
#>

function WriteXmlToScreen([xml]$xml)
{
    $StringWriter = New-Object System.IO.StringWriter;
    $XmlWriter = New-Object System.Xml.XmlTextWriter $StringWriter;
    $XmlWriter.Formatting = "indented";
    $xml.WriteTo($XmlWriter);
    $XmlWriter.Flush();
    $StringWriter.Flush();
    Write-Output $StringWriter.ToString();
}

# Script working directory
Set-Location $PSScriptRoot\..

####################################
###         Configuration        ###
####################################
$enableMSI = $true
#$buildOnly = 'win-x64'
#$buildOnly = 'linux-x64'
#$buildOnly = 'osx-x64'
#$buildOnly = 'osx-arm64'
$zipPackages = $true
#$enableNugetPublish = $true

# Profilling
$stopWatch = New-Object -TypeName System.Diagnostics.Stopwatch 
$deployStopWatch = New-Object -TypeName System.Diagnostics.Stopwatch
$stopWatch.Start()


# Variables
$software = "UVtools"
$project = "UVtools.WPF"
$buildWith = "Release"
$netFolder = "net6.0"
$rootFolder = $(Get-Location)
$releaseFolder = "$project\bin\$buildWith\$netFolder"
$objFolder = "$project\obj\$buildWith\$netFolder"
$publishFolder = "publish"
$platformsFolder = "UVtools.Platforms"

# Not supported yet! No fuse on WSL
$appImageFile = 'appimagetool-x86_64.AppImage'
$appImageFilePath = "$platformsFolder/AppImage/$appImageFile"
$appImageUrl = "https://github.com/AppImage/AppImageKit/releases/download/continuous/$appImageFile"

$macIcns = "UVtools.CAD/UVtools.icns"

#$version = (Get-Command "$releaseFolder\UVtools.dll").FileVersionInfo.ProductVersion
$projectXml = [Xml] (Get-Content "$project\$project.csproj")
$version = "$($projectXml.Project.PropertyGroup.Version)".Trim();
if([string]::IsNullOrWhiteSpace($version)){
    Write-Error "Can not detect the UVtools version, does $project\$project.csproj exists?"
    exit
}

# MSI Variables
$installers = @("UVtools.InstallerMM", "UVtools.Installer")
$msiOutputFile = "$rootFolder\UVtools.Installer\bin\x64\Release\UVtools.msi"
$msiComponentsFile = "$rootFolder\UVtools.InstallerMM\UVtools.InstallerMM.wxs"
$msiSourceFiles = "$rootFolder\$publishFolder\win-x64"
$msbuild = "`"${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe`" /t:Build /p:Configuration=$buildWith /p:MSIProductVersion=$version"

Write-Output "
####################################
###  UVtools builder & deployer  ###
####################################
Version: $version [$buildWith]
"

####################################
###   Clean up previous publish  ###
####################################
# Clean up previous publish
Remove-Item $publishFolder -Recurse -ErrorAction Ignore # Clean

####################################
###    Self-contained runtimes   ###
####################################
$runtimes = 
@{
    "win-x64" = @{
        "extraCmd" = ""
        "exclude" = @("UVtools.sh")
        "include" = @()
    }
    "linux-x64" = @{
        "extraCmd" = ""
        "exclude" = @()
        "include" = @("libcvextern.so")
    }
    "arch-x64" = @{
        "extraCmd" = ""
        "exclude" = @()
        "include" = @("libcvextern.so")
    }
    "rhel-x64" = @{
        "extraCmd" = ""
        "exclude" = @()
        "include" = @("libcvextern.so")
    }
    #"linux-arm64" = @{
    #    "extraCmd" = ""
    #    "exclude" = @()
    #    "include" = @("libcvextern.so")
    #}
    #"unix-x64" = @{
    #    "extraCmd" = ""
    #    "exclude" = @("x86", "x64", "libcvextern.dylib")
    #}
    "osx-x64" = @{
        "extraCmd" = ""
        "exclude" = @()
        "include" = @("libcvextern.dylib")
    }
    "osx-arm64" = @{
        "extraCmd" = ""
        "exclude" = @()
        "include" = @("libcvextern.dylib")
    }
}


# https://github.com/AppImage/AppImageKit/wiki/Bundling-.NET-Core-apps
#Invoke-WebRequest -Uri $appImageUrl -OutFile $appImageFilePath
#wsl chmod a+x $appImageFilePath

if($null -ne $enableNugetPublish -and $enableNugetPublish)
{
    $nugetApiKeyFile = 'build/nuget_api.key'
    if (Test-Path -Path $nugetApiKeyFile -PathType Leaf)
    {
        Write-Output "Creating nuget package"
        dotnet pack UVtools.Core --configuration 'Release'

        $nupkg = "UVtools.Core/bin/Release/UVtools.Core.$version.nupkg"

        if (Test-Path -Path $nupkg -PathType Leaf){
            $nugetApiKeyFile = (Get-Content $nugetApiKeyFile)
            dotnet nuget push $nupkg --api-key $nugetApiKeyFile --source https://api.nuget.org/v3/index.json
            #Remove-Item $nupkg
        }else {
            Write-Error "Nuget package publish failed!"
            Write-Error "File '$nupkg' was not found"
            return
        }
    }
}

foreach ($obj in $runtimes.GetEnumerator()) {
    if(![string]::IsNullOrWhiteSpace($buildOnly) -and !$buildOnly.Equals($obj.Name)) {continue}
    # Configuration
    $deployStopWatch.Restart()
    $runtime = $obj.Name;       # runtime name
    $extraCmd = $obj.extraCmd;  # extra cmd to run with dotnet
    $targetZip = "$publishFolder/${software}_${runtime}_v$version.zip"  # Target zip filename
    
    # Deploy
    Write-Output "################################
Building: $runtime"
    dotnet publish $project -o "$publishFolder/$runtime" -c $buildWith -r $runtime -p:PublishReadyToRun=true --self-contained $extraCmd

    New-Item "$publishFolder/$runtime/runtime_package.dat" -ItemType File -Value $runtime
    if(!$runtime.Equals('win-x64'))
    {
        # Fix permissions
        wsl chmod +x "$publishFolder/$runtime/UVtools" `|`| :
        wsl chmod +x "$publishFolder/$runtime/UVtools.sh" `|`| :
    }
    
    # Cleanup
    Remove-Item "$releaseFolder\$runtime" -Recurse -ErrorAction Ignore
    Remove-Item "$objFolder\$runtime" -Recurse -ErrorAction Ignore
    Write-Output "$releaseFolder\$runtime"
    
    foreach ($excludeObj in $obj.Value.exclude) {
        Write-Output "Excluding: $excludeObj"
        Remove-Item "$publishFolder\$runtime\$excludeObj" -Recurse -ErrorAction Ignore
    }

    foreach ($includeObj in $obj.Value.include) {
        Write-Output "Including: $includeObj"
        Copy-Item "$platformsFolder\$runtime\$includeObj" -Destination "$publishFolder\$runtime"  -Recurse -ErrorAction Ignore
    }

    #if($runtime.Equals('linux-x64')){
    #    $appDirDest = "$publishFolder/AppImage.$runtime/AppDir"
    #    Copy-Item "$platformsFolder/AppImage/AppDir" $appDirDest -Force -Recurse
    #    wsl chmod 755 "$appDirDest/AppRun"
    #    wsl cp -a "$publishFolder/$runtime/." "$appDirDest/usr/bin"
    #    wsl $appImageFilePath $appDirDest
    #}
    if($runtime.StartsWith('osx-')){
        $macAppFolder = "${software}.app"
        $macPublishFolder = "$publishFolder/${macAppFolder}"
        $macInfoplist = "$platformsFolder/$runtime/Info.plist"
        $macOutputInfoplist = "$macPublishFolder/Contents"
        $macTargetZipLegacy = "$publishFolder/${software}_${runtime}-legacy_v$version.zip"

        New-Item -ItemType directory -Path "$macPublishFolder"
        New-Item -ItemType directory -Path "$macPublishFolder/Contents"
        New-Item -ItemType directory -Path "$macPublishFolder/Contents/MacOS"
        New-Item -ItemType directory -Path "$macPublishFolder/Contents/Resources"

        
        Copy-Item "$macIcns" -Destination "$macPublishFolder/Contents/Resources"
        ((Get-Content -Path "$macInfoplist") -replace '#VERSION',"$version") | Set-Content -Path "$macOutputInfoplist/Info.plist"
        wsl cp -a "$publishFolder/$runtime/." "$macPublishFolder/Contents/MacOS"
        wsl chmod +x "$macPublishFolder/Contents/MacOS/UVtools"

        if($null -ne $zipPackages -and $zipPackages)
        {
            wsl cd "$publishFolder/" `&`& pwd `&`& zip -r "../$targetZip" "$macAppFolder/*"
            #wsl cd "$publishFolder/$runtime" `&`& pwd `&`& zip -r "../../$macTargetZipLegacy" .
        }

        Rename-Item -Path $macPublishFolder -NewName "${macAppFolder}_${runtime}"
        
    }
    else {
        if($null -ne $zipPackages -and $zipPackages)
        {
            Write-Output "Compressing $runtime to: $targetZip"
            Write-Output $targetZip "$publishFolder/$runtime"
            wsl cd "$publishFolder/$runtime" `&`& pwd `&`& zip -r "../../$targetZip" .
        }
    }

    # Zip
    #Write-Output "Compressing $runtime to: $targetZip"
    #Write-Output $targetZip "$publishFolder/$runtime"
    #[System.IO.Compression.ZipFile]::CreateFromDirectory("$publishFolder\$runtime", $targetZip, [System.IO.Compression.CompressionLevel]::Optimal, $false, [FixedEncoder]::new())
	#wsl cd "$publishFolder/$runtime" `&`& pwd `&`& chmod +x -f "./$software" `|`| : `&`& zip -r "../../$targetZip" "."
    $deployStopWatch.Stop()
    Write-Output "Took: $($deployStopWatch.Elapsed)
################################
"
}

# Universal package
<#
$deployStopWatch.Restart()
$runtime = "universal-x86-x64"
$targetZip = "$publishFolder\${software}_${runtime}_v$version.zip"

Write-Output "################################
Building: $runtime"
dotnet build $project -c $buildWith

Write-Output "Compressing $runtime to: $targetZip"
[System.IO.Compression.ZipFile]::CreateFromDirectory($releaseFolder, $targetZip, [System.IO.Compression.CompressionLevel]::Optimal, $false, [FixedEncoder]::new())
Write-Output "Took: $($deployStopWatch.Elapsed)
################################
"
$stopWatch.Stop()
#>

# MSI Installer for Windows
if($null -ne $enableMSI -and $enableMSI)
{
    $deployStopWatch.Restart()
    $runtime = 'win-x64'
    if (Test-Path -Path $msiSourceFiles) {
        $msiTargetFile = "$publishFolder\${software}_${runtime}_v$version.msi"
        Write-Output "################################"
        Write-Output "Clean and build MSI components manifest file"
        $msiComponentsXml = [Xml] (Get-Content $msiComponentsFile)
        foreach($element in $msiComponentsXml.Wix.Module.Directory.Directory)
        {
            if($element.Id -eq 'MergeRedirectFolder')
            {
                wixCleanUpElement $element $msiSourceFiles
                $msiComponentsXml.Save($msiComponentsFile)
                break
            }
        }

        Write-Output "Building: $runtime MSI Installer"

        foreach($installer in $installers)
        {
            # Clean and build MSI
            Remove-Item "$installer\obj" -Recurse -ErrorAction Ignore
            Remove-Item "$installer\bin" -Recurse -ErrorAction Ignore
            Invoke-Expression "& $msbuild $installer\$installer.wixproj"
        }

        Write-Output "Copying $runtime MSI to: $msiTargetFile"
        Copy-Item $msiOutputFile $msiTargetFile

        Write-Output "Took: $($deployStopWatch.Elapsed)
        ################################
        "
    }
    else {
        Write-Error "MSI build is enabled but the runtime '$runtime' is not found."
    }
    
}


Write-Output "
####################################
###           Completed          ###
####################################
In: $($stopWatch.Elapsed)"


