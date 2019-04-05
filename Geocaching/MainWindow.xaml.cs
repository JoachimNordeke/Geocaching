﻿using Geocaching.Data;
using Geocaching.Models;
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


        //This makes it easier to pick a color.
        private Dictionary<string, SolidColorBrush> colors = new Dictionary<string, SolidColorBrush>
        {
            ["Blue"] = new SolidColorBrush(Colors.Blue),
            ["Gray"] = new SolidColorBrush(Colors.Gray),
            ["Red"] = new SolidColorBrush(Colors.Red),
            ["Green"] = new SolidColorBrush(Colors.Green),
            ["Black"] = new SolidColorBrush(Colors.Black)
        };

        private AppDbContext db = new AppDbContext();

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
                var point = e.GetPosition(this);
                latestClickLocation = ConvertPointToGeoCoordinate(point);
            };

            map.ContextMenu = new ContextMenu();

            var addPersonMenuItem = new MenuItem { Header = "Add Person" };
            map.ContextMenu.Items.Add(addPersonMenuItem);
            addPersonMenuItem.Click += OnAddPersonClick;

            var addGeocacheMenuItem = new MenuItem { Header = "Add Geocache" };
            map.ContextMenu.Items.Add(addGeocacheMenuItem);
            addGeocacheMenuItem.Click += OnAddGeocacheClick;

            var reloadFromDatabaseItem = new MenuItem { Header = "Reload from database" };
            map.ContextMenu.Items.Add(reloadFromDatabaseItem);
            reloadFromDatabaseItem.Click += OnReloadFromDatabaseClick;
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

            // It is recommended (but optional) to use this method for setting the color and opacity of each pin after every user interaction that might change something.
            // This method should then be called once after every significant action, such as clicking on a pin, clicking on the map, or clicking a context menu option.
        }

        private void OnMapLeftClick()
        {
            // Handle map click here.
            UpdateMap();
        }

        private async void OnReloadFromDatabaseClick(object sender, RoutedEventArgs args)
        {
            layer.Children.Clear();
            personPins.Clear();
            cachePins.Clear();

            await LoadFromDatabaseAsync();
        }

        private void OnAddGeocacheClick(object sender, RoutedEventArgs args)
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
            var pin = AddPin(latestClickLocation, tooltip, Colors.Black);

            pin.MouseLeftButtonDown += OnCachePinClick;


            var geocache = new Geocache()
            {
                Contents = contents,
                Coordinates = latestClickLocation,
                Message = message,
                Person = activePinPerson
            };
            db.Geocache.Add(geocache);
            db.SaveChanges();

            pin.Tag = new Dictionary<string, ITag> { ["Person"] = activePinPerson, ["Geocache"] = geocache };
            cachePins.Add(pin);
        }

        private void OnAddPersonClick(object sender, RoutedEventArgs args)
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
            // Add person to map and database here.
            var pin = AddPin(latestClickLocation, tooltip, Colors.Blue);

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

            db.Add(person);

            db.SaveChanges();
            //This catpture the ID set by the DatabBase
            pin.Tag = person;

            activePinPerson = person;

        }

        private void OnPersonPinClick(object sender, MouseButtonEventArgs args)
        {
            var pushpin = sender as Pushpin;
            activePinPerson = (Person)pushpin.Tag;

            pushpin.Opacity = 1;

            personPins.Where(p => (Person)p.Tag != activePinPerson).ToList().ForEach(p => p.Opacity = 0.5);

            var foundGeocaches =  db.FoundGeocache.Where(f => f.Person == activePinPerson).Include(f => f.Geocache).Select(f => f.Geocache).ToArray();

            foreach (var pin in cachePins)
            {
                var cachePinPerson = (Person)(pin.Tag as Dictionary<string, ITag>)["Person"];
                var cachePinCache = (Geocache)(pin.Tag as Dictionary<string, ITag>)["Geocache"];
                
                if (cachePinPerson == activePinPerson)
                    pin.Background = colors["Black"];
                else if (foundGeocaches.Contains(cachePinCache))
                    pin.Background = colors["Green"];
                else
                    pin.Background = colors["Red"];
            }

            args.Handled = true;
        }
       
        private void OnCachePinClick(object sender, MouseButtonEventArgs args)
        {
            if (activePinPerson == null) return;
            var pin = sender as Pushpin;
            Geocache cachePinCache = (Geocache)(pin.Tag as Dictionary<string, ITag>)["Geocache"];

            if (pin.Background == colors["Red"])
            {
                try
                {
                    db.Add(new FoundGeocache { Person = activePinPerson, Geocache = cachePinCache });
                    db.SaveChanges();
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
                    db.Remove(db.FoundGeocache.Where(f => f.Person == activePinPerson && f.Geocache == cachePinCache).Single());
                    db.SaveChanges();
                    pin.Background = colors["Red"];
                }
                catch (Exception e)
                {
                    MessageBox.Show("Something went wrong when updating the database.\r\n\r\n" +
                        e.Message, "Error");
                }
            }

            // To prevent the calling of OnMapLeftClick.
            args.Handled = true;
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

        private async void OnLoadFromFileClick(object sender, RoutedEventArgs args)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                DefaultExt = ".txt",
                Filter = "Text documents (.txt)|*.txt"
            };

            bool? result = dialog.ShowDialog();

            if (result != true) return;

            string path = dialog.FileName;

            string[] lines = await Task.Run(() =>
            {
                return File.ReadLines(path).ToArray();
            });

            db.Person.RemoveRange(db.Person);
            db.Geocache.RemoveRange(db.Geocache);
            db.FoundGeocache.RemoveRange(db.FoundGeocache);

            bool AddNewPerson = true;
            Person person = null; // Set to null to be usable in the 'else if' statement below.
            Geocache geocache;
            var PersonFoundGeocaches = new Dictionary<Person, int[]>();
            var GeocacheIdFromFile = new Dictionary<int, Geocache>();

            foreach (var line in lines)
            {
                if (line != "")
                {
                    if (AddNewPerson)
                    {
                        string[] temp = line.Split('|').Select(l => l.Trim()).ToArray();
                        person = new Person
                        {
                            FirstName = temp[0],
                            LastName = temp[1],
                            Country = temp[2],
                            City = temp[3],
                            StreetName = temp[4],
                            StreetNumber = byte.Parse(temp[5]),
                            Coordinates = new GeoCoordinate {
                                Latitude = double.Parse(temp[6].Replace('.', ',')),
                                Longitude = double.Parse(temp[7].Replace('.', ','))
                            }
                        };

                        db.Add(person);
                        AddNewPerson = false;
                    }
                    else if (!line.StartsWith("Found"))
                    {
                        string[] temp = line.Split('|').Select(l => l.Trim()).ToArray();
                        geocache = new Geocache
                        {
                            Person = person,
                            Coordinates = new GeoCoordinate {
                                Latitude = double.Parse(temp[1].Replace('.', ',')),
                                Longitude = double.Parse(temp[2].Replace('.', ',')) },
                            Contents = temp[3],
                            Message = temp[4]
                        };
                        GeocacheIdFromFile.Add(int.Parse(temp[0]), geocache);
                        db.Add(geocache);
                    }
                    else
                    {
                        PersonFoundGeocaches.Add(person, line.Substring(7).Split(',').Select(l => int.Parse(l.Trim())).ToArray());
                        AddNewPerson = true;
                    }
                }
            }

            foreach (var p in PersonFoundGeocaches)
            {
                p.Value.ToList().ForEach(async CacheID => await db.AddAsync(new FoundGeocache { Person = p.Key, Geocache = GeocacheIdFromFile[CacheID] }));
            }

            try { db.SaveChanges(); OnReloadFromDatabaseClick(sender, args); UpdateMap(); }
            catch { MessageBox.Show("Something went wrong when loading the file.", "Error"); }
        }

        private async void OnSaveToFileClick(object sender, RoutedEventArgs args)
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

            var lines = new List<string>();
            foreach (var p in await db.Person.Include(p => p.Geocaches).Include(p => p.FoundGeocaches).ThenInclude(p => p.Geocache).ToListAsync())
            {
                lines.Add(string.Join(" | ", new[] { p.FirstName, p.LastName, p.Country, p.City, p.StreetName,
                    Convert.ToString(p.StreetNumber), Convert.ToString(p.Coordinates.Latitude),
                    Convert.ToString(p.Coordinates.Longitude) }));

                foreach (var g in p.Geocaches)
                {
                    lines.Add(string.Join(" | ", new[] { g.ID.ToString(), Convert.ToString(g.Coordinates.Latitude),
                            Convert.ToString(g.Coordinates.Longitude), g.Contents, g.Message }));
                }

                int[] foundGeocachesId = p.FoundGeocaches.Select(fg => fg.Geocache.ID).ToArray();

                lines.Add("Found: " + string.Join(", ", foundGeocachesId));

                lines.Add("");
            }

            await Task.Run(() =>
             {
                 File.WriteAllLines(path, lines);
             });

        }

        private async Task LoadFromDatabaseAsync()
        {
            foreach (var p in await db.Person.Include(p => p.Geocaches).ToListAsync())
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
