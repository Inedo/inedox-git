@echo off

dotnet new tool-manifest --force
dotnet tool install inedo.extensionpackager

cd Git\InedoExtension
dotnet inedoxpack pack . C:\LocalDev\BuildMaster\Extensions\Git.upack --build=Debug -o
cd ..\..