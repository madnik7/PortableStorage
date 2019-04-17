SET curdir=%~dp0

start /b cmd /k call "%curdir%PortableStorage\.nuget\publish.bat"
pause 

start /b cmd /k call "%curdir%PortableStorage.Android\.nuget\publish.bat"

