//---------------------------------------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//
// <Description>
// This program tracks up to 6 people simultaneously.
// If a person is tracked, the associated gesture detector will determine if that person is seated or not.
// If any of the 6 positions are not in use, the corresponding gesture detector(s) will be paused
// and the 'Not Tracked' image will be displayed in the UI.
// </Description>
//----------------------------------------------------------------------------------------------------


//idea: if two gestures are opposites, only do whichever confidence is greater

using WindowsInput;

namespace Microsoft.Samples.Kinect.DiscreteGestureBasics
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for the MainWindow
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private KinectSensor kinectSensor = null;
        private Body[] bodies = null;
        private BodyFrameReader bodyFrameReader = null;
        private string statusText = null;
        private KinectBodyView kinectBodyView = null;
        private List<List<GestureDetector>> detectorPerBodyList = null;

        private String handsUpKey;
        private String rightHandCloseKey;
        private String leftHandCloseKey;
        private String leanRightKey;

        private Body body;

        public MainWindow()
        {
            kinectSensor = KinectSensor.GetDefault();
            kinectSensor.IsAvailableChanged += Sensor_IsAvailableChanged;
            kinectSensor.Open();
            StatusText = kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                           : Properties.Resources.NoSensorStatusText;
            bodyFrameReader = kinectSensor.BodyFrameSource.OpenReader();
            bodyFrameReader.FrameArrived += Reader_BodyFrameArrived;
            kinectBodyView = new KinectBodyView(kinectSensor);

            detectorPerBodyList = new List<List<GestureDetector>>();

            InitializeComponent();

            // set our data context objects for display in UI
            DataContext = this;
            kinectBodyViewbox.DataContext = kinectBodyView;

            // create a gesture detector for each body (6 bodies => 6 detectors) and create content controls to display results in the UI
            createDetectors();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #region shit
        public string StatusText
        {
            get
            {
                return statusText;
            }

            set
            {
                if (statusText != value)
                {
                    statusText = value;

                    // notify any bound elements that the text has changed
                    if (PropertyChanged != null)
                    {
                        PropertyChanged(this, new PropertyChangedEventArgs("StatusText"));
                    }
                }
            }
        }
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                bodyFrameReader.FrameArrived -= Reader_BodyFrameArrived;
                bodyFrameReader.Dispose();
                bodyFrameReader = null;
            }

            if (kinectSensor != null)
            {
                kinectSensor.IsAvailableChanged -= Sensor_IsAvailableChanged;
                kinectSensor.Close();
                kinectSensor = null;
            }
        }
        private void Sensor_IsAvailableChanged(object sender, IsAvailableChangedEventArgs e)
        {
            // on failure, set the status text
            StatusText = kinectSensor.IsAvailable ? Properties.Resources.RunningStatusText
                                                           : Properties.Resources.SensorNotAvailableStatusText;
        }
        #endregion

        private void Reader_BodyFrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;
            processHandStates();
            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (bodies == null)
                    {
                        // creates an array of 6 bodies, which is the max number of bodies that Kinect can track simultaneously
                        bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(bodies);
                    dataReceived = true;
                }
            }

            if (dataReceived)
            {
                kinectBodyView.UpdateBodyFrame(bodies);

                if (bodies != null)
                {
                    int maxBodies = kinectSensor.BodyFrameSource.BodyCount;
                    for (int i = 0; i < maxBodies; ++i)
                    {
                        Body body = bodies[i];
                        ulong trackingId = body.TrackingId;

                        foreach (GestureDetector det in detectorPerBodyList[i])
                        {
                            if (trackingId != det.TrackingId)
                            {
                                det.TrackingId = trackingId;
                                det.IsPaused = trackingId == 0;
                            }
                        }
                    }
                }
            }
        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            handsUpKey = handsUpInput.Text;
            leftHandCloseKey = leftHandCloseInput.Text;
            rightHandCloseKey = rightHandCloseInput.Text;
            leanRightKey = leanRightInput.Text;
             
            createDetectors();
        }
        private void createDetectors()
        {
            if (detectorPerBodyList != null)
            {
                detectorPerBodyList.Clear();
            }
            int maxBodies = 6;
            for (int i = 0; i < maxBodies; ++i)
            {
                List<GestureDetector> list = new List<GestureDetector>();
                GestureDetector d1 = new GestureDetector(kinectSensor, @"Database\handsUp.gbd", "handsUp", 1,  VirtualKeyCode.VK_W, 0.4f);
                GestureDetector d2 = new GestureDetector(kinectSensor, @"Database\handsOut.gbd", "handsOut", 1, VirtualKeyCode.VK_S, 0.4f);
                GestureDetector d3 = new GestureDetector(kinectSensor, @"Database\lean.gbd", "lean_Left", 2, VirtualKeyCode.VK_A, 0.8f);
                GestureDetector d4 = new GestureDetector(kinectSensor, @"Database\lean.gbd", "lean_Right", 2, VirtualKeyCode.VK_D, 0.8f);
                list.Add(d1);
              //  list.Add(d2);
                list.Add(d3);
                list.Add(d4);
                detectorPerBodyList.Add(list);
            }
        }
        private void processHandStates()
        {
            if (bodies != null)
            {
                foreach (Body b in bodies)
                {
                    if (b != null && b.IsTracked)
                    {
                        if (b.HandLeftState == HandState.Closed || b.HandRightState == HandState.Closed)
                        {
                            InputSimulator.SimulateKeyDown(VirtualKeyCode.VK_O);
                        }
                        else
                        {
                            InputSimulator.SimulateKeyUp(VirtualKeyCode.VK_O);
                        }
                    }
                }
            }
        }

        private void pressKeys()
        {
            foreach (List<GestureDetector> detectorLists in detectorPerBodyList)
            {
                List<GestureDetector> ref1 = new List<GestureDetector>();
                List<GestureDetector> ref2 = new List<GestureDetector>();
                List<GestureDetector> ref3 = new List<GestureDetector>();
                List<GestureDetector> ref4 = new List<GestureDetector>();
                List<GestureDetector> ref5 = new List<GestureDetector>();

                foreach (GestureDetector detector in detectorLists)
                {
                    if(detector.ReferenceID == 1) ref1.Add(detector);
                    if (detector.ReferenceID == 1) ref2.Add(detector);
                    if (detector.ReferenceID == 1) ref3.Add(detector);
                    if (detector.ReferenceID == 1) ref4.Add(detector);
                    if (detector.ReferenceID == 1) ref5.Add(detector);
                }

                //loop through ref1 - ref5 to get greatest val, set current gd to that, and apply the button (public function to call button press)
            }
        }
    }
}
