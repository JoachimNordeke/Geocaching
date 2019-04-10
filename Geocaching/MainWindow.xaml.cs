using Geocaching.Data;
using Geocaching.Data.Enitites;
using Geocaching.Service;
using Microsoft.EntityFrameworkCore;
using Microsoft.Maps.MapControl.WPF;
using System;
using System.Collections.Generic;
using System.Device.Location;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Geocaching
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Contains the ID string needed to use the Bing map.
        // Instructions here: https://docs.microsoft.com/en-us/bingmaps/getting-started/bing-maps-dev-center-help/getting-a-bing-maps-key
        private const string applicationId = "ApHORC4egk6ExJWI2PwXMPFrLXa89u0Z5kUo05q-foI9r90BgdG8dqrtDyG8Nl31";

        private readonly DbService _db;

        //This makes it easier to pick a color.
        private Dictionary<string, SolidColorBrush> colors = new Dictionary<string, SolidColorBrush>
        {
            ["Blue"] = new SolidColorBrush(Colors.Blue),
            ["Gray"] = new SolidColorBrush(Colors.Gray),
            ["Red"] = new SolidColorBrush(Colors.Red),
            ["Green"] = new SolidColorBrush(Colors.Green),
            ["Black"] = new SolidColorBrush(Colors.Black)
        };

        // Easier access to all Pins
        private List<Pushpin> personPins = new List<Pushpin>();
        private List<Pushpin> cachePins = new List<Pushpin>();

        //To keep track of wich personPin is selected at the moment. Used in OnPersonPinClick and OnCachePinClick.
        private Person activePinPerson;

        private MapLayer layer;

        // Contains the location of the latest click on the map.
        // The GeoCoordinate object in turn contains information like longitude and latitude.
        private GeoCoordinate latestClickLocation;

        private Location gothenburg = new Location(57.719021, 11.991202);

        public MainWindow()
        {
            _db = new DbService();
            InitializeComponent();
            Start();
        }

        private async void Start()
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            //if (applicationId == null)
            //{
            //    MessageBox.Show("Please set the applicationId variable before running this program.");
            //    Environment.Exit(0);
            //}

            CreateMap();

            await LoadFromDatabaseAsync();
        }

        private void CreateMap()
        {
            map.CredentialsProvider = new ApplicationIdCredentialsProvider(applicationId);
            map.Center = gothenburg;
            map.ZoomLevel = 12;
            layer = new MapLayer();
            map.Children.Add(layer);

            Point? pointerStartPosition = null;

            // This will start tracking the pointer's position by giving mapStartPosition a value
            MouseLeftButtonDown += (sender, e) =>
            {
                pointerStartPosition = e.GetPosition(this);
            };

            // This will occur when the mouse is released and if the pointer hasn't moved.
            // What this gives, is that if you select a Persons pin and then move the map, 
            // OnMapLeftClick will never be called. Therefore, you can select a person pin 
            // and move around the map. But if you just click the map, it will call OnMapLeftClick.
            MouseLeftButtonUp += (sender, e) =>
            {
                var point = e.GetPosition(this);

                if (pointerStartPosition != null && pointerStartPosition == point)
                {
                    latestClickLocation = ConvertPointToGeoCoordinate(point);

                    if (e.LeftButton == MouseButtonState.Released)
                    {
                        OnMapLeftClick();
                    }
                }
            };

            MouseRightButtonUp += (sender, e) =>
            {
                Point point = e.GetPosition(this);
                latestClickLocation = ConvertPointToGeoCoordinate(point);
            };

            map.ContextMenu = new ContextMenu();

            var addPersonMenuItem = new MenuItem { Header = "Add Person" };
            map.ContextMenu.Items.Add(addPersonMenuItem);
            addPersonMenuItem.Click += OnAddPersonClickAsync;

            var addGeocacheMenuItem = new MenuItem { Header = "Add Geocache" };
            map.ContextMenu.Items.Add(addGeocacheMenuItem);
            addGeocacheMenuItem.Click += OnAddGeocacheClickAsync;

            var reloadFromDatabaseItem = new MenuItem { Header = "Reload from database" };
            map.ContextMenu.Items.Add(reloadFromDatabaseItem);
            reloadFromDatabaseItem.Click += OnReloadFromDatabaseClickAsync;
        }

        private void UpdateMap()
        {
            foreach (var pin in personPins)
            {
                pin.Opacity = 1;
            }
            foreach (var pin in cachePins)
            {
                pin.Opacity = 1;
                pin.Background = colors["Gray"];
            }
            
            activePinPerson = null;
        }

        private void OnMapLeftClick()
        {
            // Handle map click here.
            UpdateMap();
        }

        private async void OnReloadFromDatabaseClickAsync(object sender, RoutedEventArgs args)
        {
            layer.Children.Clear();
            personPins.Clear();
            cachePins.Clear();

            await LoadFromDatabaseAsync();
        }

        private async void OnAddGeocacheClickAsync(object sender, RoutedEventArgs args)
        {
            if (activePinPerson == null)
            {
                MessageBox.Show("Select Person First", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new GeocacheDialog
            {
                Owner = this
            };
            dialog.ShowDialog();
            if (dialog.DialogResult == false)
            {
                return;
            }

            string contents = dialog.GeocacheContents;
            string message = dialog.GeocacheMessage;

            string tooltip = $"Latitude:\t\t{latestClickLocation.Latitude}\r\nLongitude:\t{latestClickLocation.Longitude}\r\n" +
                    $"Made by:\t{activePinPerson.FirstName + " " + activePinPerson.LastName}\r\n" +
                    $"Contents:\t{contents}\r\nMessage:\t{message}";


            // Add geocache to map and database here.
            Pushpin pin = AddPin(latestClickLocation, tooltip, Colors.Black);

            pin.MouseLeftButtonDown += OnCachePinClick;
            
            var geocache = new Geocache()
            {
                Contents = contents,
                Coordinates = latestClickLocation,
                Message = message
            };

            await _db.AddGeocacheAsync(geocache, activePinPerson.ID);

            pin.Tag = new Dictionary<string, ITag> { ["Person"] = activePinPerson, ["Geocache"] = geocache };
            cachePins.Add(pin);
        }

        private async void OnAddPersonClickAsync(object sender, RoutedEventArgs args)
        {
            var dialog = new PersonDialog
            {
                Owner = this
            };
            dialog.ShowDialog();
            if (dialog.DialogResult == false)
            {
                return;
            }

            string city = dialog.AddressCity;
            string country = dialog.AddressCountry;
            string streetName = dialog.AddressStreetName;
            byte streetNumber = dialog.AddressStreetNumber;

            string tooltip = $"Latitude:\t\t{latestClickLocation.Latitude}\r\nLongitude:\t{latestClickLocation.Longitude}\r\n" +
                   $"Name:\t\t{dialog.PersonFirstName + " " + dialog.PersonLastName}\r\nStreet address:\t{streetName + " " + streetNumber}";

            Pushpin pin = AddPin(latestClickLocation, tooltip, Colors.Blue);

            pin.MouseLeftButtonDown += OnPersonPinClick;
            personPins.Add(pin);

            Person person = new Person()
            {
                FirstName = dialog.PersonFirstName,
                LastName = dialog.PersonLastName,
                City = city,
                Country = country,
                StreetName = streetName,
                StreetNumber = streetNumber,
                Coordinates = latestClickLocation
            };
            await _db.AddPersonAsync(person);

            pin.Tag = person;
            activePinPerson = person;

            foreach (var personPin in personPins)
            {
                if (personPin == pin)
                    personPin.Opacity = 1;
                else
                    personPin.Opacity = 0.5;
            }

            foreach (var cachePin in cachePins)
            {
                cachePin.Background = colors["Red"];
            }
        }

        private async void OnPersonPinClick(object sender, MouseButtonEventArgs args)
        {
            var pushpin = sender as Pushpin;
            activePinPerson = (Person)pushpin.Tag;

            pushpin.Opacity = 1;

            personPins.Where(p => (Person)p.Tag != activePinPerson).ToList().ForEach(p => p.Opacity = 0.5);

            args.Handled = true;
            
            int[] foundGeocaches = await _db.GetPersonFoundGeocachesAsync(activePinPerson);

            foreach (var pin in cachePins)
            {
                var cachePinPerson = (Person)(pin.Tag as Dictionary<string, ITag>)["Person"];
                var cachePinCache = (Geocache)(pin.Tag as Dictionary<string, ITag>)["Geocache"];

                if (cachePinPerson == activePinPerson)
                    pin.Background = colors["Black"];
                else if (foundGeocaches.Contains(cachePinCache.ID))
                    pin.Background = colors["Green"];
                else
                    pin.Background = colors["Red"];
            }
        }
       
        private async void OnCachePinClick(object sender, MouseButtonEventArgs args)
        {
            if (activePinPerson == null) return;
            var pin = sender as Pushpin;
            Geocache cachePinCache = (Geocache)(pin.Tag as Dictionary<string, ITag>)["Geocache"];
            
            // To prevent the calling of OnMapLeftClick.
            args.Handled = true;

            if (pin.Background == colors["Red"])
            {
                try
                {
                    await _db.AddFoundGeocacheAsync(new FoundGeocache { PersonID = activePinPerson.ID, GeocacheID = cachePinCache.ID });
                    pin.Background = colors["Green"];
                }
                catch (Exception e)
                {
                    MessageBox.Show("Something went wrong when updating the database.\r\n\r\n" +
                        e.Message, "Error");
                }
            }
            else if (pin.Background == colors["Green"])
            {
                try
                {
                    await _db.RemoveFoundGeocacheAsync(activePinPerson, cachePinCache);
                    pin.Background = colors["Red"];
                }
                catch (Exception e)
                {
                    MessageBox.Show("Something went wrong when updating the database.\r\n\r\n" +
                        e.Message, "Error");
                }
            }
        }

        private GeoCoordinate ConvertPointToGeoCoordinate(Point point)
        {
            var location = map.ViewportPointToLocation(point);
            return new GeoCoordinate { Latitude = location.Latitude, Longitude = location.Longitude };
        }

        private Pushpin AddPin(GeoCoordinate location, string tooltip, Color color)
        {
            var pin = new Pushpin
            {
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(color)
            };
            ToolTipService.SetToolTip(pin, tooltip);
            ToolTipService.SetInitialShowDelay(pin, 0);
            layer.AddChild(pin, new Location(location.Latitude, location.Longitude));
            return pin;
        }

        private async void OnLoadFromFileClickAsync(object sender, RoutedEventArgs args)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".txt",
                Filter = "Text documents (.txt)|*.txt"
            };

            bool? result = dialog.ShowDialog();

            if (result != true) return;

            string path = dialog.FileName;

            try
            {
                await _db.ReadFromFileToDatabaseAsync(path);
                OnReloadFromDatabaseClickAsync(sender, args);
                UpdateMap();

            }
            catch (Exception e)
            {
                MessageBox.Show($"Something went wrong when loading the file.\r\n{e}", "Error");
            }
        }

        private async void OnSaveToFileClickAsync(object sender, RoutedEventArgs args)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                DefaultExt = ".txt",
                Filter = "Text documents (.txt)|*.txt",
                FileName = "Geocaches"
            };
            bool? result = dialog.ShowDialog();
            if (result != true)
            {
                return;
            }

            string path = dialog.FileName;

            await _db.SaveToFileFromDatabaseAsync(path);
        }

        private async Task LoadFromDatabaseAsync()
        {
            foreach (var p in await _db.GetPersonsWithGeocachesAsync())
            {
                string pTooltip = $"Latitude:\t\t{p.Coordinates.Latitude}\r\nLongitude:\t{p.Coordinates.Longitude}\r\n" +
                    $"Name:\t\t{p.FirstName + " " + p.LastName}\r\nStreet address:\t{p.StreetName + " " + p.StreetNumber}";

                var pPin = AddPin(p.Coordinates, pTooltip, Colors.Blue);
                pPin.Tag = p;
                pPin.MouseLeftButtonDown += OnPersonPinClick;
                personPins.Add(pPin);

                foreach (var g in p.Geocaches)
                {
                    string gTooltip = $"Latitude:\t\t{g.Coordinates.Latitude}\r\nLongitude:\t{g.Coordinates.Longitude}\r\n" +
                    $"Made by:\t{p.FirstName + " " + p.LastName}\r\n" +
                    $"Contents:\t{g.Contents}\r\nMessage:\t{g.Message}";

                    var gPin = AddPin(g.Coordinates, gTooltip, Colors.Gray);
                    gPin.Tag = new Dictionary<string, ITag> { ["Person"] = p, ["Geocache"] = g };
                    gPin.MouseLeftButtonDown += OnCachePinClick;
                    cachePins.Add(gPin);
                }
            }
        }
    }
}
