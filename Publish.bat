SET curdir=%~dp0

start /b cmd /k call "%curdir%PortableStorage.Android\.nuget\publish.bat"
start /b cmd /k call "%curdir%PortableStorage\.nuget\publish.bat"

pause

