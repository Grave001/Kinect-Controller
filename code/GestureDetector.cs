//------------------------------------------------------------------------------
// <copyright file="GestureDetector.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.DiscreteGestureBasics
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Kinect;
    using Microsoft.Kinect.VisualGestureBuilder;
    using System.Windows.Media;
    using System.Windows.Forms;
    using WindowsInput;

    /// <summary>
    /// Gesture Detector class which listens for VisualGestureBuilderFrame events from the service
    /// and updates the associated GestureResultView object with the latest results for the 'Seated' gesture
    /// </summary>
    public class GestureDetector : IDisposable
    {
        private string gestureDatabase;
        private string dataName;
        private string pressKey;
        private VirtualKeyCode keyPress;
        private float threshHold = .2f;

        private VisualGestureBuilderFrameSource vgbFrameSource = null;
        private VisualGestureBuilderFrameReader vgbFrameReader = null;

        private readonly Brush[] trackedColors = new Brush[] { Brushes.Red, Brushes.Orange, Brushes.Green, Brushes.Blue, Brushes.Indigo, Brushes.Violet };
        private Brush bodyColor = Brushes.Gray;
        private int bodyIndex = 0;
        private float confidence = 0.0f;
        private bool detected = false;
        private bool isTracked = false;
        private int referenceID;

        public GestureDetector(KinectSensor kinectSensor, string dataBase, string gestureName, int refID, VirtualKeyCode key, float thresh)
        {
            if (kinectSensor == null)
            {
                throw new ArgumentNullException("kinectSensor");
            }


            gestureDatabase = dataBase;
            dataName = gestureName;
            //pressKey = key;
            keyPress = key;
            threshHold = thresh;
            referenceID = refID;

            // create the vgb source. The associated body tracking ID will be set when a valid body frame arrives from the sensor.
            this.vgbFrameSource = new VisualGestureBuilderFrameSource(kinectSensor, 0);
            this.vgbFrameSource.TrackingIdLost += this.Source_TrackingIdLost;

            // open the reader for the vgb frames
            this.vgbFrameReader = this.vgbFrameSource.OpenReader();
            if (this.vgbFrameReader != null)
            {
                //this.vgbFrameReader.IsPaused = true;
                this.vgbFrameReader.FrameArrived += this.Reader_GestureFrameArrived;
            }

            // load the 'Seated' gesture from the gesture database
            using (VisualGestureBuilderDatabase database = new VisualGestureBuilderDatabase(this.gestureDatabase))
            {
                // we could load all available gestures in the database with a call to vgbFrameSource.AddGestures(database.AvailableGestures), 
                // but for this program, we only want to track one discrete gesture from the database, so we'll load it by name
                foreach (Gesture gesture in database.AvailableGestures)
                {
                    if (gesture.Name.Equals(this.dataName))
                    {
                        this.vgbFrameSource.AddGesture(gesture);
                    }
                }
            }
        }

        #region bullshit
        public ulong TrackingId
        {
            get
            {
                return this.vgbFrameSource.TrackingId;
            }

            set
            {
                if (this.vgbFrameSource.TrackingId != value)
                {
                    this.vgbFrameSource.TrackingId = value;
                }
            }
        }
        public bool IsPaused
        {
            get
            {
                return this.vgbFrameReader.IsPaused;
            }

            set
            {
                if (this.vgbFrameReader.IsPaused != value)
                {
                    this.vgbFrameReader.IsPaused = value;
                }
            }
        }
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        public int BodyIndex
        {
            get
            {
                return this.bodyIndex;
            }

            private set
            {
                if (this.bodyIndex != value)
                {
                    this.bodyIndex = value;
                }
            }
        }
        public Brush BodyColor
        {
            get
            {
                return this.bodyColor;
            }

            private set
            {
                if (this.bodyColor != value)
                {
                    this.bodyColor = value;
                }
            }
        }
        public bool IsTracked
        {
            get
            {
                return this.isTracked;
            }

            private set
            {
                if (this.IsTracked != value)
                {
                    this.isTracked = value;
                }
            }
        }
        public bool Detected
        {
            get
            {
                return this.detected;
            }

            private set
            {
                if (this.detected != value)
                {
                    this.detected = value;
                }
            }
        }
        public int ReferenceID
        {
            get { return referenceID; }
        }
        public float Confidence
        {
            get
            {
                return this.confidence;
            }

            private set
            {
                if (this.confidence != value)
                {
                    this.confidence = value;
                }
            }
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.vgbFrameReader != null)
                {
                    this.vgbFrameReader.FrameArrived -= this.Reader_GestureFrameArrived;
                    this.vgbFrameReader.Dispose();
                    this.vgbFrameReader = null;
                }

                if (this.vgbFrameSource != null)
                {
                    this.vgbFrameSource.TrackingIdLost -= this.Source_TrackingIdLost;
                    this.vgbFrameSource.Dispose();
                    this.vgbFrameSource = null;
                }
            }
        }
        #endregion


        private void Reader_GestureFrameArrived(object sender, VisualGestureBuilderFrameArrivedEventArgs e)
        {
            VisualGestureBuilderFrameReference frameReference = e.FrameReference;
            using (VisualGestureBuilderFrame frame = frameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    // get the discrete gesture results which arrived with the latest frame
                    IReadOnlyDictionary<Gesture, DiscreteGestureResult> discreteResults = frame.DiscreteGestureResults;

                    if (discreteResults != null)
                    {
                        // we only have one gesture in this source object, but you can get multiple gestures
                        foreach (Gesture gesture in this.vgbFrameSource.Gestures)
                        {
                            if (gesture.Name.Equals(this.dataName) && gesture.GestureType == GestureType.Discrete)
                            {
                                DiscreteGestureResult result = null;
                                discreteResults.TryGetValue(gesture, out result);

                                if (result != null)
                                {

                                    // update the GestureResultView object with new gesture result values
                                    UpdateGestureResult(true, result.Detected, result.Confidence);
                                }
                            }
                        }
                    }
                }
            }
        }
        private void Source_TrackingIdLost(object sender, TrackingIdLostEventArgs e)
        {
            // update the GestureResultView object to show the 'Not Tracked' image in the UI
            UpdateGestureResult(false, false, 0.0f);
        }
        public void UpdateGestureResult(bool isBodyTrackingIdValid, bool isGestureDetected, float detectionConfidence)
        {
            this.IsTracked = isBodyTrackingIdValid;
            Confidence = 0f;

            if (!this.IsTracked)
            {
                this.Detected = false;
                this.BodyColor = Brushes.Gray;
            }
            else
            {
                if (detectionConfidence > threshHold && isGestureDetected)
                {
                    this.Detected = true;
                }
                else
                {
                    this.Detected = false;
                }

                this.BodyColor = this.trackedColors[this.BodyIndex];

                if (this.Detected)
                {
                    this.Confidence = detectionConfidence;
                }
            }

            if (Detected)
            {
                InputSimulator.SimulateKeyDown(keyPress);
                Console.WriteLine(dataName + ", " + Confidence);
            }
            else
            {
                InputSimulator.SimulateKeyUp(keyPress);
            }
        }
    }
}
