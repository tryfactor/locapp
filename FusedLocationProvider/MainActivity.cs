using System;
using System.Threading.Tasks;

using Android;
using Android.App;
using Android.Content.PM;
using Android.Gms.Common;
using Android.Gms.Location;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using System.Net.Http;
using System.Net;
using System.IO;
using Android.Locations;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using ILocationListener = Android.Locations.ILocationListener;
using Android.Runtime;
using AlertDialog = Android.App.AlertDialog;
using Android.Content;

namespace com.xamarin.samples.location.fusedlocationprovider
{
    [Activity(Label = "@string/app_name", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, ILocationListener
    {
        const long ONE_MINUTE = 60 * 1000;
        const long FIVE_MINUTES = 5 * ONE_MINUTE;
        const long TWO_MINUTES = 2 * ONE_MINUTE;

        static readonly int RC_LAST_LOCATION_PERMISSION_CHECK = 1000;
        static readonly int RC_LOCATION_UPDATES_PERMISSION_CHECK = 1100;

        private static WebClient client = new WebClient();
        static readonly string KEY_REQUESTING_LOCATION_UPDATES = "requesting_location_updates";

        FusedLocationProviderClient fusedLocationProviderClient;
        Button getLastLocationButton;
        bool isGooglePlayServicesInstalled;
        bool isRequestingLocationUpdates;
        TextView latitude;
        internal TextView latitude2;
        LocationCallback locationCallback;
        LocationRequest locationRequest;
        TextView longitude;
        internal TextView longitude2;
        TextView provider;
        internal TextView provider2;
        
        ProgressDialog progressDialog;
        LocationManager locationManager;
        Timer gpsTimer;
        int gpsTimeout = 30000;
        Location coordinates;

        internal Button requestLocationUpdatesButton;
        Button send_last_location_button;

        string globalLocationLat, globalLocationLong;

        View rootLayout;
        

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            if (bundle != null)
            {
                isRequestingLocationUpdates = bundle.KeySet().Contains(KEY_REQUESTING_LOCATION_UPDATES) &&
                                              bundle.GetBoolean(KEY_REQUESTING_LOCATION_UPDATES);
            }
            else
            {
                isRequestingLocationUpdates = false;
            }

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.Main);
            isGooglePlayServicesInstalled = IsGooglePlayServicesInstalled();
            rootLayout = FindViewById(Resource.Id.root_layout);

            // UI to display last location
            getLastLocationButton = FindViewById<Button>(Resource.Id.get_last_location_button);
            latitude = FindViewById<TextView>(Resource.Id.latitude);
            longitude = FindViewById<TextView>(Resource.Id.longitude);
            provider = FindViewById<TextView>(Resource.Id.provider);

            // UI to display location updates
            requestLocationUpdatesButton = FindViewById<Button>(Resource.Id.request_location_updates_button);
            latitude2 = FindViewById<TextView>(Resource.Id.latitude2);
            longitude2 = FindViewById<TextView>(Resource.Id.longitude2);
            provider2 = FindViewById<TextView>(Resource.Id.provider2);

            send_last_location_button = FindViewById<Button>(Resource.Id.send_last_location_button);

            if (isGooglePlayServicesInstalled)
            {
                locationRequest = new LocationRequest()
                                  .SetPriority(LocationRequest.PriorityHighAccuracy)
                                  .SetInterval(FIVE_MINUTES)
                                  .SetFastestInterval(TWO_MINUTES);
                locationCallback = new FusedLocationProviderCallback(this);

                fusedLocationProviderClient = LocationServices.GetFusedLocationProviderClient(this);
                getLastLocationButton.Click += GetLastLocationButtonOnClick;
                requestLocationUpdatesButton.Click += getLocationDetailsButtonOnClick;// RequestLocationUpdatesButtonOnClick;
                send_last_location_button.Click += SendLastLocationButtonOnClick;
                //send_last_location_button.Click += getLocationDetailsButtonOnClick;

            }
            else
            {
                // If there is no Google Play Services installed, then this sample won't run.
                Snackbar.Make(rootLayout, Resource.String.missing_googleplayservices_terminating, Snackbar.LengthIndefinite)
                        .SetAction(Resource.String.ok, delegate { FinishAndRemoveTask(); })
                        .Show();
            }
        }

        private void getLocationDetailsButtonOnClick(object sender, EventArgs e)
        {
            if (ActivityCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) != (int)Permission.Granted)
            {
                //Permission has not been granted
                RequestLocationPermission(RC_LAST_LOCATION_PERMISSION_CHECK); 
            }
            else
            {
                // Permission is already available.
                Log.Info("LOCATION_PERMISSION_CHECK", "GPS permission has already been granted.");

                StartGPS();
            }
        }

        #region Panic location
        private void SendLastLocationButtonOnClick(object sender, EventArgs e)
        {

            sendRequest("test");

        }

        int a = 1;
        private async Task<string> sendRequest(string url)
        {
            // Create an HTTP web request using the URL:
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(new Uri("https://jtdwp4z7b3.execute-api.us-east-1.amazonaws.com/decemberstage/PostPanic?a=http://maps.google.com/?q="+globalLocationLat+","+globalLocationLong));

            request.ContentType = "application/json";
            request.Method = "POST";

            // Send the request to the server and wait for the response:
            using (WebResponse response = await request.GetResponseAsync())
            {
                // Get a stream representation of the HTTP web response:
                using (Stream stream = response.GetResponseStream())
                {
                    // Use this stream to build a JSON document object:
                    string jsonDoc = await Task.Run(() => stream.ToString());
                    Console.Out.WriteLine("Response: {0}", jsonDoc.ToString());

                    // Return the JSON document:
                    return jsonDoc;
                }
            }
        }
        #endregion

        #region Get current location
        public void StartGPS()
        {
            try
            {
                long checkInterval = 0;
                long minDistance = 0;

                locationManager = (LocationManager)this.GetSystemService(Activity.LocationService);
                string locationProvider = null;

                Criteria criteriaForLocationService = new Criteria { Accuracy = Accuracy.Coarse };
                IList<string> acceptableLocationProviders = locationManager.GetProviders(criteriaForLocationService, true);

                if (acceptableLocationProviders.Any())
                {
                    locationProvider = acceptableLocationProviders.First();
                    locationManager.RequestLocationUpdates(locationProvider, checkInterval, minDistance, this);
                    gpsTimer = new Timer(gpsTimeout);
                    gpsTimer.Elapsed += OnGPSTimerElapsed;
                    gpsTimer.Enabled = true;

                    progressDialog = new ProgressDialog(this);
                    progressDialog.SetTitle("GPS Location");
                    progressDialog.SetMessage("Getting GPS location...");
                    progressDialog.SetProgressStyle(ProgressDialogStyle.Spinner);
                    progressDialog.SetCancelable(true);
                    progressDialog.CancelEvent += OnGPSCancel;
                    progressDialog.Show();

                    int titleDividerId = Resources.GetIdentifier("titleDivider", "id", "android");
                    View titleDivider = progressDialog.FindViewById(titleDividerId);
                    if (titleDivider != null)
                        titleDivider.SetBackgroundColor(Resources.GetColor(Resource.Color.material_blue_grey_800));

                    Log.Debug("ProviderSearch", "gps requesting updates");
                }
                else
                {
                    AlertDialog alert = new AlertDialog.Builder(this)
                        .SetPositiveButton("Yes", (sender, EventArgs) =>
                        {
                            Intent gpsOptionsIntent = new Intent(Android.Provider.Settings.ActionLocationSourceSettings);
                            StartActivity(gpsOptionsIntent);
                        })
                        .SetNegativeButton("No", (sender, EventArgs) =>
                        {
                        })
                        .SetMessage("This requires your GPS to be switched on. Switch on GPS and retry.")
                        .SetTitle("Turn on GPS")
                        .Show();

                    int titleDividerId = Resources.GetIdentifier("titleDivider", "id", "android");
                    View titleDivider = alert.FindViewById(titleDividerId);
                    if (titleDivider != null)
                        titleDivider.SetBackgroundColor(Resources.GetColor(Resource.Color.material_blue_grey_800));
                }
            }
            catch (System.Exception ex)
            {
            }
        }

        void OnGPSCancel(object sender, EventArgs e)
        {
            if (gpsTimer != null && gpsTimer.Enabled)
            {
                gpsTimer.Stop();
                gpsTimer.Close();
            }
            locationManager.RemoveUpdates(this);
            this.RunOnUiThread(() => progressDialog.Dismiss());
        }

        void OnGPSTimerElapsed(object sender, ElapsedEventArgs e)
        {
            locationManager.RemoveUpdates(this);

            this.RunOnUiThread(() => progressDialog.Dismiss());

            if (gpsTimer != null && gpsTimer.Enabled)
            {
                gpsTimer.Stop();
                gpsTimer.Close();
            }

            this.RunOnUiThread(() => {
                AlertDialog alert = new AlertDialog.Builder(this)
                        .SetPositiveButton("OK", (sender1, EventArgs) => {
                        })
                        .SetMessage("Unable to get GPS location.")
                        .SetTitle("GPS Location")
                    .Show();

                int titleDividerId = Resources.GetIdentifier("titleDivider", "id", "android");
                View titleDivider = alert.FindViewById(titleDividerId);
                if (titleDivider != null)
                    titleDivider.SetBackgroundColor(Resources.GetColor(Resource.Color.material_blue_grey_800));
            });
        }

        public void OnLocationChanged(Location location)
        {
            Log.Debug("ProviderSearch", "OnLocationChanged");
            coordinates = location;
            if (location != null)
            {
                Log.Debug("ProviderSearch", "location is not null");
                if (gpsTimer != null && gpsTimer.Enabled)
                {
                    gpsTimer.Stop();
                    gpsTimer.Close();
                }
                this.RunOnUiThread(() => progressDialog.Dismiss());
                Log.Debug("ProviderSearchFragment", "longitude:" + location.Longitude + " latitude:" + location.Latitude);

                latitude2.Text = location.Latitude.ToString();
                longitude2.Text = location.Longitude.ToString();

                locationManager.RemoveUpdates(this);
            }
            else
            {
                Log.Debug("ProviderSearch", "location is null");
            }
        }

        #endregion



        #region Get location updates

        async void RequestLocationUpdatesButtonOnClick(object sender, EventArgs eventArgs)
        {
            // No need to request location updates if we're already doing so.
            if (isRequestingLocationUpdates)
            {
                StopRequestLocationUpdates();
                isRequestingLocationUpdates = false;
            }
            else
            {
                if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) == Permission.Granted)
                {
                    await StartRequestingLocationUpdates();
                    isRequestingLocationUpdates = true;
                }
                else
                {
                    RequestLocationPermission(RC_LAST_LOCATION_PERMISSION_CHECK);
                }
            }
        }

        async void StopRequestLocationUpdates()
        {
            latitude2.Text = string.Empty;
            longitude2.Text = string.Empty;
            provider2.Text = string.Empty;

            requestLocationUpdatesButton.SetText(Resource.String.request_location_button_text);

            if (isRequestingLocationUpdates)
            {
                await fusedLocationProviderClient.RemoveLocationUpdatesAsync(locationCallback);
            }
        }

        async Task StartRequestingLocationUpdates()
        {
            requestLocationUpdatesButton.SetText(Resource.String.request_location_in_progress_button_text);
            await fusedLocationProviderClient.RequestLocationUpdatesAsync(locationRequest, locationCallback);
        }
        #endregion

        #region Get Last Location

        async void GetLastLocationButtonOnClick(object sender, EventArgs eventArgs)
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) == Permission.Granted)
            {
                await GetLastLocationFromDevice();
            }
            else
            {
                RequestLocationPermission(RC_LAST_LOCATION_PERMISSION_CHECK);
            }
        }

        async Task GetLastLocationFromDevice()
        {
            getLastLocationButton.SetText(Resource.String.getting_last_location);
            var location = await fusedLocationProviderClient.GetLastLocationAsync();

            if (location == null)
            {
                latitude.SetText(Resource.String.location_unavailable);
                longitude.SetText(Resource.String.location_unavailable);
                provider.SetText(Resource.String.could_not_get_last_location);
            }
            else
            {
                var currentCulture = System.Globalization.CultureInfo.InstalledUICulture;
                var numberFormat = (System.Globalization.NumberFormatInfo)currentCulture.NumberFormat.Clone();
                numberFormat.NumberDecimalSeparator = ".";
                latitude.Text = Resources.GetString(Resource.String.latitude_string, location.Latitude);
                longitude.Text = Resources.GetString(Resource.String.longitude_string, location.Longitude);
                globalLocationLat = location.Latitude.ToString().Replace(',','.');
                globalLocationLong = location.Longitude.ToString().Replace(',', '.');

                provider.Text = Resources.GetString(Resource.String.provider_string, location.Provider);
                getLastLocationButton.SetText(Resource.String.get_last_location_button_text);
            }
        }

        #endregion
        


        #region Android Device states
        protected override async void OnResume()
        {
            base.OnResume();
            if (CheckSelfPermission(Manifest.Permission.AccessFineLocation) == Permission.Granted)
            {
                if (isRequestingLocationUpdates)
                {
                    await StartRequestingLocationUpdates();
                }
            }
            else
            {
                RequestLocationPermission(RC_LAST_LOCATION_PERMISSION_CHECK);
            }
        }

        protected override void OnPause()
        {
            StopRequestLocationUpdates();
            base.OnPause();
        }

        protected override void OnSaveInstanceState(Bundle outState)
        {
            outState.PutBoolean(KEY_REQUESTING_LOCATION_UPDATES, isRequestingLocationUpdates);
            base.OnSaveInstanceState(outState);
        }
        #endregion



        #region Permission requesting

        public override async void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            if (requestCode == RC_LAST_LOCATION_PERMISSION_CHECK || requestCode == RC_LOCATION_UPDATES_PERMISSION_CHECK)
            {
                if (grantResults.Length == 1 && grantResults[0] == Permission.Granted)
                {
                    if (requestCode == RC_LAST_LOCATION_PERMISSION_CHECK)
                    {
                        await GetLastLocationFromDevice();
                    }
                    else
                    {
                        await StartRequestingLocationUpdates();
                        isRequestingLocationUpdates = true;
                    }
                }
                else
                {
                    Snackbar.Make(rootLayout, Resource.String.permission_not_granted_termininating_app, Snackbar.LengthIndefinite)
                            .SetAction(Resource.String.ok, delegate { FinishAndRemoveTask(); })
                            .Show();
                    return;
                }
            }
            else
            {
                Log.Debug("FusedLocationProviderSample", "Don't know how to handle requestCode " + requestCode);
            }

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        void RequestLocationPermission(int requestCode)
        {
            if (ActivityCompat.ShouldShowRequestPermissionRationale(this, Manifest.Permission.AccessFineLocation))
            {
                Snackbar.Make(rootLayout, Resource.String.permission_location_rationale, Snackbar.LengthIndefinite)
                        .SetAction(Resource.String.ok,
                                   delegate
                                   {
                                       ActivityCompat.RequestPermissions(this, new[] {Manifest.Permission.AccessFineLocation}, requestCode);
                                   })
                        .Show();
            }
            else
            {
                ActivityCompat.RequestPermissions(this, new[] {Manifest.Permission.AccessFineLocation}, requestCode);
            }
        }

        bool IsGooglePlayServicesInstalled()
        {
            var queryResult = GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(this);
            if (queryResult == ConnectionResult.Success)
            {
                Log.Info("MainActivity", "Google Play Services is installed on this device.");
                return true;
            }

            if (GoogleApiAvailability.Instance.IsUserResolvableError(queryResult))
            {
                var errorString = GoogleApiAvailability.Instance.GetErrorString(queryResult);
                Log.Error("MainActivity", "There is a problem with Google Play Services on this device: {0} - {1}",
                          queryResult, errorString);
            }

            return false;
        }

        public void OnProviderDisabled(string provider)
        {
            throw new NotImplementedException();
        }

        public void OnProviderEnabled(string provider)
        {
            throw new NotImplementedException();
        }

        public void OnStatusChanged(string provider, [GeneratedEnum] Availability status, Bundle extras)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
