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
Check Android Sample in the repository!

First get access to storage Uri
```c#
// Select a folder by Intent 
private void BrowseOnClick(object sender, EventArgs eventArgs)
{
     DroidStorageHelper.BrowserFolder(this, browseRequestCode);
}

// Access the folder via DroidStorgeSAF
protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
{
     base.OnActivityResult(requestCode, resultCode, data);
     var uri = DroidStorageHelper.ResolveFromActivityResult(this, requestCode, resultCode, data, browseRequestCode);
     if (uri != null)
     {
        var storage = DroidStorgeSAF.CreateStorage(this, uri);
        storage.CreateStorage("_PortableStorage.Test");
        storage.WriteAllText(filename, sampleText);
        var res = storage.ReadAllText(filename);
     }
}
```
