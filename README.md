# PortableStorage
A Portable Storage for .NET

## Features
* Build-in Cashe
* Easy to add provider
* .NET Standard 2.0 File Provider
* Android Storage Access Framework (SAF) File Provider
* ZipStorage provider

## nuget
* https://www.nuget.org/packages/PortableStorage/
* https://www.nuget.org/packages/PortableStorage.Android/

## Usage

nuget Install-Package PortableStorage

### File System Provider
A provider for .NET Standard File.
```c#
  var storage = FileStorgeProvider.CreateStorage("c:/storage", true);
  storage.WriteAllText("fileName.txt", "1234");
```

### Android Storage Access Framework (SAF) Provider
A provider for Android SAF, easy access to Android external memory, USB OTG and sdcard.
Check Android Sample in the repository!

1) First get access to storage Uri by calling SafStorageHelper.BrowserFolder.
2) Obtain storage object by using the given uri. the uri can also be saved for later usage.

```c#

private const int browseRequestCode = 100; //Just a unique number


// Select a folder by Intent 
private void BrowseOnClick(object sender, EventArgs eventArgs)
{
     SafStorageHelper.BrowserFolder(this, browseRequestCode);
}

// Access the folder via SafStorgeProvider
protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
{
    base.OnActivityResult(requestCode, resultCode, data);
    if (requestCode == BROWSE_REQUEST_CODE && resultCode == Result.Ok)
    {
        var uri = SafStorageHelper.ResolveFromActivityResult(this, data);
        var storage = SafStorgeProvider.CreateStorage(this, uri);
        storage.CreateStorage("_PortableStorage.Test");
        storage.WriteAllText("test.txt", "123");
    }
}
```

### Zip Storage Provider (Read-Only)
Provider access to ZipFile same as a storage seamlessly.

```c#
  var storage = ZipStorgeProvider.CreateStorage("c:/temp/foo.zip");
  var testInZip = storage.ReadAllText("fileName.txt");
```
