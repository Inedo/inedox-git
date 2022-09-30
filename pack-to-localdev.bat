@echo off

dotnet new tool-manifest --force
dotnet tool install inedo.extensionpackager

cd Git\GitHub.InedoExtension
dotnet inedoxpack pack . C:\LocalDev\BuildMaster\Extensions\GitHub.upack --build=Debug -o
cd ..\..

cd Git\AzureDevOps.InedoExtension
dotnet inedoxpack pack . C:\LocalDev\BuildMaster\Extensions\AzureDevOps.upack --build=Debug -o
cd ..\..

cd Git\Git.InedoExtension
dotnet inedoxpack pack . C:\LocalDev\BuildMaster\Extensions\Git.upack --build=Debug -o
cd ..\..

cd Git\GitLab.InedoExtension
dotnet inedoxpack pack . C:\LocalDev\BuildMaster\Extensions\GitLab.upack --build=Debug -o
cd ..\..