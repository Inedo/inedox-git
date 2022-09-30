$pkgName = "Inedo.SDK.DevOnly"
$pkgProjFile= "c:\Projects\Inedo.sdk\Inedo.SDK\Inedo.SDK.csproj"

$slnFile = "C:\Projects\inedox-git\Git\Git.sln"
$projFilesToMunge = @( `
  "C:\Projects\inedox-git\Git\AzureDevOps.InedoExtension\AzureDevOps.InedoExtension.csproj", `
  "C:\Projects\inedox-git\Git\Git.InedoExtension\Git.InedoExtension.csproj", `
  "C:\Projects\inedox-git\Git\GitHub.InedoExtension\GitHub.InedoExtension.csproj", `
  "C:\Projects\inedox-git\Git\GitLab.InedoExtension\GitLab.InedoExtension.csproj" `
)

dotnet sln "$slnFile" add "$pkgProjFile"
foreach ($projFile in $projFilesToMunge) {
  dotnet remove "$projFile" package "$pkgName"
  dotnet add $projFile reference $pkgProjFile
}