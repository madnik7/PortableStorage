using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using PortableStorage.Droid;
using System;

namespace AndroidSample
{
    [Activity(Label = "Portable Storage Sample for SAF", MainLauncher = true, Theme = "@android:style/Theme.Holo.Light")]
    public class MainActivity : Activity
    {
        private const int browseRequestCode = 100;

        private Button selectFolderButton;
        private Button readWriteButton;
        private TextView infoView;
        private TextView uriView;
        private void InitUI()
        {
            var root = new TableLayout(this);
            root.SetPadding(0, 20, 0, 0);

            //add buttons
            var buttonLayout = new LinearLayout(this);
            root.AddView(buttonLayout);

            selectFolderButton = new Button(this)
            {
                Text = "Select Folder",
            };
            selectFolderButton.Click += BrowseOnClick;
            buttonLayout.AddView(selectFolderButton);

            readWriteButton = new Button(this)
            {
                Text = "Read & Write",
            };
            readWriteButton.Click += ReadWriteClick;
            buttonLayout.AddView(readWriteButton);

            //add uri
            uriView = new TextView(this)
            {
                Text = "Uri: ",
            };
            root.AddView(uriView);

            //add text
            infoView = new TextView(this)
            {
                TextSize = 20,
                Text = StorageUri == null ? "Info: First \"Select Folder\" then press \"Read & Write\" to check access." : "",
            };
            root.AddView(infoView);
            SetContentView(root);
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            InitUI();

            if (StorageUri != null)
                uriView.Text = "Uri: " + StorageUri;
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        private Uri StorageUri
        {
            get
            {
                var value = Xamarin.Essentials.Preferences.Get("LastUri", null);
                return value != null ? new Uri(value) : null;
            }
            set
            {
                Xamarin.Essentials.Preferences.Set("LastUri", value.ToString());
                uriView.Text = "Uri: " + value;
            }
        }

        private void BrowseOnClick(object sender, EventArgs eventArgs)
        {
            SafStorageHelper.BrowserFolder(this, browseRequestCode);
        }

        private void ReadWriteClick(object sender, EventArgs eventArgs)
        {
            try
            {
                if (StorageUri == null)
                    throw new Exception("No folder has been selected!");


                var filename = "test.txt";
                var sampleText = "Sample Text";
                var storage = SafStorgeProvider.CreateStorage(this, StorageUri);
                storage.CreateStorage("_PortableStorage.Test");
                storage.WriteAllText(filename, sampleText);
                var res = storage.ReadAllText(filename);
                if (res == sampleText)
                {
                    infoView.Text = "Info: The content has been written and readed successfully :)\n\r";
                    infoView.Text += "Now you have a access to the storage even after reloading the App.";
                }
            }
            catch (Exception ex)
            {
                infoView.Text = "Error: " + ex.Message;
            }
        }

        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            try
            {
                var uri = SafStorageHelper.ResolveFromActivityResult(this, requestCode, resultCode, data, browseRequestCode);
                if (uri != null)
                    StorageUri = uri;
            }
            catch (Exception ex)
            {
                infoView.Text = "Error: " + ex.Message;
            }

        }
    }
}

