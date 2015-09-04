///
/// This is a sample project for the Infinite Flight API
/// 

using Fds.IFAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IFCTestApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        IFConnectorClient client = new IFConnectorClient();
        BroadcastReceiver receiver = new BroadcastReceiver();

        public MainWindow()
        {
            InitializeComponent();

            airplaneStateGrid.DataContext = null;
            airplaneStateGrid.DataContext = new APIAircraftState();

            mainTabControl.Visibility = System.Windows.Visibility.Collapsed;
        }

        bool serverInfoReceived = false;

        void receiver_DataReceived(object sender, EventArgs e)
        {
            byte[] data = (byte[])sender;

            var apiServerInfo = Serializer.DeserializeJson<APIServerInfo>(UTF8Encoding.UTF8.GetString(data));

            if (apiServerInfo != null)
            {
                Console.WriteLine("Received Server Info from: {0}:{1}", apiServerInfo.Address, apiServerInfo.Port);
                serverInfoReceived = true;
                receiver.Stop();
                Dispatcher.BeginInvoke((Action)(() => 
                {
                    Connect(IPAddress.Parse(apiServerInfo.Address), apiServerInfo.Port);
                }));
            }
            else
            {
                Console.WriteLine("Invalid Server Info Received");
            }
        }

        private void Connect(IPAddress iPAddress, int port)
        {
            client.Connect(iPAddress.ToString(), port);

            connectionStateTextBlock.Text = String.Format("Connected ({0}:{1})", iPAddress, port);

            overlayGrid.Visibility = System.Windows.Visibility.Collapsed;
            mainTabControl.Visibility = System.Windows.Visibility.Visible;

            client.CommandReceived += client_CommandReceived;

            client.SendCommand(new APICall { Command = "InfiniteFlight.GetStatus" });

            Task.Run(() =>
            {

                while (true)
                {
                    try
                    {
                        client.SendCommand(new APICall { Command = "Airplane.GetState" });

                        Thread.Sleep(2000);

                    }
                    catch (Exception ex)
                    {

                    }
                }
            });

            Task.Run(() =>
            {

                while (true)
                {
                    try
                    {
                        client.SendCommand(new APICall { Command = "Live.GetTraffic" });
                        client.SendCommand(new APICall { Command = "Live.ATCFacilities" });

                        Thread.Sleep(5000);

                    }
                    catch (Exception ex)
                    {

                    }
                }
            });
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            receiver.DataReceived += receiver_DataReceived;
            receiver.StartListening();
        }

        void client_CommandReceived(object sender, CommandReceivedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() => 
            {
                var type = Type.GetType(e.Response.Type);

                if (type == typeof(APIAircraftState))
                {
                    var state = Serializer.DeserializeJson<APIAircraftState>(e.CommandString);

                    airplaneStateGrid.DataContext = null;
                    airplaneStateGrid.DataContext = state;
                }
                else if (type == typeof(GetValueResponse))
                {
                    var state = Serializer.DeserializeJson<GetValueResponse>(e.CommandString);

                    Console.WriteLine("{0} -> {1}", state.Parameters[0].Name, state.Parameters[0].Value);
                }
                else if (type == typeof(LiveAirplaneList))
                {
                    var airplaneList = Serializer.DeserializeJson<LiveAirplaneList>(e.CommandString);

                    airplaneDataGrid.ItemsSource = airplaneList.Airplanes;
                }
                else if (type == typeof(FacilityList))
                {
                    var facilityList = Serializer.DeserializeJson<FacilityList>(e.CommandString);

                    facilitiesDataGrid.ItemsSource = facilityList.Facilities;
                }
                else if (type == typeof(IFAPIStatus))
                {
                    var status = Serializer.DeserializeJson<IFAPIStatus>(e.CommandString);

                    versionTextBlock.Text = status.AppVersion;
                    userNameTextBlock.Text = status.LoggedInUser;
                    
                }
            }));
            
        }

        private void toggleBrakesButton_Click(object sender, RoutedEventArgs e)
        {
            //client.SetValue("Aircraft.Systems.Autopilot.EnableHeading", "True");
//            
        }
        
        private void toggleBrakesButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            client.ExecuteCommand("Commands.Brakes", new CallParameter[] { new CallParameter { Name = "KeyAction", Value = "Down" } } );
        }

        private void toggleBrakesButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            client.ExecuteCommand("Commands.Brakes", new CallParameter[] { new CallParameter { Name = "KeyAction", Value = "Up" } });
        }

        private void parkingBrakeButton_Click(object sender, RoutedEventArgs e)
        {
            client.ExecuteCommand("Commands.ParkingBrakes");
            client.GetValue("Aircraft.State.IsBraking");
        }

        private void prevCameraButton_Click(object sender, RoutedEventArgs e)
        {
            client.ExecuteCommand("Commands.PrevCamera");
        }

        private void nextCameraButton_Click(object sender, RoutedEventArgs e)
        {
            client.ExecuteCommand("Commands.NextCamera");
        }

        private void autopilotHeadingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            client.ExecuteCommand("Commands.Autopilot.SetHeading", new CallParameter[] { new CallParameter { Name = "Heading", Value = autopilotHeadingSlider.Value.ToString() } });            
        }

        private void setGearStateButton_Click(object sender, RoutedEventArgs e)
        {
            client.ExecuteCommand("Commands.LandingGear");
        }

        private void flapsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            client.ExecuteCommand("Commands.Aircraft.SetFlapState", new CallParameter[] { new CallParameter { Name = "Heading", Value = flapsSlider.Value.ToString() } });
        }

        private void addFplItems_Click(object sender, RoutedEventArgs e)
        {
            var items = flightPlanItemsTextBlock.Text.Split(',');
            client.ExecuteCommand("Commands.FlightPlan.AddWaypoints", items.Select(x => new CallParameter { Name = "WPT", Value = x }).ToArray());
        }

        private void clearFplItems_Click(object sender, RoutedEventArgs e)
        {
            client.ExecuteCommand("Commands.FlightPlan.Clear");
        }

        private void activateLegButton_Click(object sender, RoutedEventArgs e)        
        {
            client.ExecuteCommand("Commands.FlightPlan.ActivateLeg", new CallParameter[] { new CallParameter { Name = "Index", Value = "3" } });
        }

        private void aileronsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            client.ExecuteCommand("NetworkJoystick.SetAxisValue", new CallParameter[] 
            { 
                new CallParameter 
                {
                    Name = "0", // axis index
                    Value = aileronsSlider.Value.ToString()
                } 
            });
        }
        
        private void Button_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Button button = sender as Button;

            client.ExecuteCommand("NetworkJoystick.SetButtonState", new CallParameter[] 
            {
                new CallParameter 
                { 
                    Name = button.Content.ToString(),  // button index
                    Value = "Down"
                }
            });
        }

        private void Button_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            Button button = sender as Button;

            client.ExecuteCommand("NetworkJoystick.SetButtonState", new CallParameter[] 
            {
                new CallParameter 
                { 
                    Name = button.Content.ToString(),  // button index
                    Value = "Up"
                }
            });
        }

        private void POVButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var button = sender as Button;
            var type = button.Content.ToString();

            var xValue = 0;
            var yValue = 0;

            if (type == "Up")
            {
                xValue = 0;
                yValue = 1;
            }
            else if (type == "Down")
            {
                xValue = 0;
                yValue = -1;
            }
            else if (type == "Left")
            {
                xValue = -1;
                yValue = 0;
            }
            else if (type == "Right")
            {
                xValue = 1;
                yValue = 0;
            }

            client.ExecuteCommand("NetworkJoystick.SetPOVState", new CallParameter[] 
                {
                    new CallParameter 
                    { 
                        Name = "X",
                        Value = xValue.ToString()
                    },
                    new CallParameter 
                    { 
                        Name = "Y",
                        Value = yValue.ToString()
                    }
                });


        }

        private void POVButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            var button = sender as Button;
            var type = button.Content.ToString();

            var xValue = 0;
            var yValue = 0;

            if (type == "Up")
            {
                xValue = 0;
                yValue = 0;
            }
            else if (type == "Down")
            {
                xValue = 0;
                yValue = 0;
            }
            else if (type == "Left")
            {
                xValue = 0;
                yValue = 0;
            }
            else if (type == "Right")
            {
                xValue = 0;
                yValue = 0;
            }

            client.ExecuteCommand("NetworkJoystick.SetPOVState", new CallParameter[] 
                {
                    new CallParameter 
                    { 
                        Name = "X",
                        Value = xValue.ToString()
                    },
                    new CallParameter 
                    { 
                        Name = "Y",
                        Value = yValue.ToString()
                    }
                });
        }
        
        private void checkbox_Checked(object sender, RoutedEventArgs e)
        {
            var checkbox = sender as CheckBox;

            if (checkbox.Equals(altitudeStateCheckbox))
                client.ExecuteCommand("Commands.Autopilot.SetAltitudeState", new CallParameter[] { new CallParameter { Value = checkbox.IsChecked.ToString() } });
            if (checkbox.Equals(headingStateCheckbox))
                client.ExecuteCommand("Commands.Autopilot.SetHeadingState", new CallParameter[] { new CallParameter { Value = checkbox.IsChecked.ToString() } });
            if (checkbox.Equals(verticalSpeedStateCheckbox))
                client.ExecuteCommand("Commands.Autopilot.SetVSState", new CallParameter[] { new CallParameter { Value = checkbox.IsChecked.ToString() } });
            if (checkbox.Equals(speedStateCheckbox))
                client.ExecuteCommand("Commands.Autopilot.SetSpeedState", new CallParameter[] { new CallParameter { Value = checkbox.IsChecked.ToString() } });
            if (checkbox.Equals(apprStateCheckbox))
                client.ExecuteCommand("Commands.Autopilot.SetApproachModeState", new CallParameter[] { new CallParameter { Value = checkbox.IsChecked.ToString() } });
        }

        private void speedTextBlock_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBlock = sender as TextBox;

            if (textBlock.Equals(speedTextBlock))
                client.ExecuteCommand("Commands.Autopilot.SetSpeed", new CallParameter[] { new CallParameter { Value = textBlock.Text.ToString() } });
            if (textBlock.Equals(altitudeTextBlock))
                client.ExecuteCommand("Commands.Autopilot.SetAltitude", new CallParameter[] { new CallParameter { Value = textBlock.Text.ToString() } });
            if (textBlock.Equals(verticalSpeedTextBlock))
                client.ExecuteCommand("Commands.Autopilot.SetVS", new CallParameter[] { new CallParameter { Value = textBlock.Text.ToString() } });
            if (textBlock.Equals(headingTextBlock))
                client.ExecuteCommand("Commands.Autopilot.SetHeading", new CallParameter[] { new CallParameter { Value = textBlock.Text.ToString() } });

        }
    }
}
