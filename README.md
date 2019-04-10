# PortableStorage
A Portable Storage for .NET

## Features
* Build-in Cashe
* Easy to add provider
* .NET Standard 2.0 File Provider
* Android Storage Access Framework (SAF) File Provider

## nuget
* https://www.nuget.org/packages/PortableStorage/
* https://www.nuget.org/packages/PortableStorage.Android/

## Usage

nuget Install-Package PortableStorage

### For File System 
A provider for .NET Standard File.
```c#
  var storage = FileStorgeProvider.CreateStorage("c:/storage", true);
  storage.WriteAllText("fileName.txt", "1234");
```

### For Android Storage Access Framework (SAF)
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
    var uri = SafStorageHelper.ResolveFromActivityResult(this, requestCode, resultCode, data, browseRequestCode);
    if (uri != null)
    {
        var storage = SafStorgeProvider.CreateStorage(this, uri);
        storage.CreateStorage("_PortableStorage.Test");
        storage.WriteAllText("test.txt", "123");
    }
}
```
