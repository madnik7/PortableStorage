using System;

using Android.App;
using Android.Content;
using Android.Provider;

namespace PortableStorage.Droid
{
    public static class SafStorageHelper
    {
        public static void BrowserFolder(Activity activity, int requestCode)
        {
            using (var intent = new Intent(Intent.ActionOpenDocumentTree))
                activity.StartActivityForResult(intent, requestCode);
        }

        /// <summary>
        /// return null if the request does not belong to requestId
        /// </summary>
        public static Uri ResolveFromActivityResult(Activity activity, int requestCode, Result resultCode, Intent data, int originalRequestCode)
        {
            if (requestCode == originalRequestCode && resultCode == Result.Ok)
            {
                var androidUri = data.Data;
                var takeFlags = data.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
                activity.ContentResolver.TakePersistableUriPermission(androidUri, takeFlags);
                var storageUri = DocumentsContract.BuildDocumentUriUsingTree(androidUri, DocumentsContract.GetTreeDocumentId(androidUri));
                return new Uri(storageUri.ToString());
            }

            return null;
        }
    }
}