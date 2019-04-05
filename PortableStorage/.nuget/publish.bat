SET curdir=%~dp0
SET nuget=%curdir%nuget.exe
SET outdir=%curdir%..\bin\nuget
cd %curdir%..\

:: Pack
"%nuget%" pack -Prop Configuration=Release -OutputDirectory "%outdir%" -build -IncludeReferencedProjects


:: Find the latest package
for /f "tokens=*" %%a in ('dir "%outdir%\*.nupkg" /b /on') do set newPackage=%%a
set packagePath=%outdir%\%newPackage%

:: Publish
"%nuget%" push "%packagePath%" -ApiKey oy2f47qkwytaeddd7mcab637nrusk6ku3kckf2ry3nnj24 -Source https://api.nuget.org/v3/index.json

pause