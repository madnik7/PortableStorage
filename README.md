# PortableStorage
A Portable Storage for .NET

## Features
* Build-in Cashe
* Easy to add provider
* .NET Standard 2.0 File Provider
* Android Storage Access Framework (SAF) File Provider

## Usage

### For File System 
```c#
  var storage = FileStorgeProvider.CreateStorage("c:/storage");
  storage.WriteAllText("fileName.txt", "1234");
```


### For Android Storage Access Framework (SAF)
Comming Soon
