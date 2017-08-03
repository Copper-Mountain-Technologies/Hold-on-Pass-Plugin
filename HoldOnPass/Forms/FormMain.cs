// Copyright ©2015-2017 Copper Mountain Technologies
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR
// ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH
// THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using CopperMountainTech;
using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace HoldOnPass
{
    public partial class FormMain : Form
    {
        // ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

        private enum ComConnectionStateEnum
        {
            INITIALIZED,
            NOT_CONNECTED,
            CONNECTED_VNA_NOT_READY,
            CONNECTED_VNA_READY
        }

        private ComConnectionStateEnum previousComConnectionState = ComConnectionStateEnum.INITIALIZED;
        private ComConnectionStateEnum comConnectionState = ComConnectionStateEnum.NOT_CONNECTED;

        // ------------------------------------------------------------------------------------------------------------

        private enum StateEnum
        {
            PRE_INIT,
            INIT,
            WAIT,
            HOLD
        };

        private StateEnum s = StateEnum.PRE_INIT;

        private StateEnum currentState
        {
            get
            {
                return s;
            }
            set
            {
                s = value;

                // update trigger state
                updateTriggerState();

                // update ui
                updateUi();
            }
        }

        // ------------------------------------------------------------------------------------------------------------

        private int selectedChannel = -1;
        private int selectedTrace = -1;

        // ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

        public FormMain()
        {
            InitializeComponent();

            // --------------------------------------------------------------------------------------------------------

            // set form icon
            Icon = Properties.Resources.app_icon;

            // set form title
            Text = Program.programName;

            // disable resizing the window
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = true;

            // position the plug-in in the lower right corner of the screen
            Rectangle workingArea = Screen.GetWorkingArea(this);
            Location = new Point(workingArea.Right - Size.Width - 130,
                                 workingArea.Bottom - Size.Height - 50);

            // always display on top
            TopMost = true;

            // --------------------------------------------------------------------------------------------------------

            // disable ui
            panelMain.Enabled = false;

            // set version label text
            toolStripStatusLabelVersion.Text = "v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);

            // init user message label
            labelUserMessage.Visible = true;
            labelUserMessage.Text = "";

            // init ui
            updateUi();

            // update the channel selection combo box
            updateChanComboBox();

            // --------------------------------------------------------------------------------------------------------

            // start the ready timer
            readyTimer.Interval = 250; // 250 ms interval
            readyTimer.Enabled = true;
            readyTimer.Start();

            // start the update timer
            updateTimer.Interval = 250; // 250 ms interval
            updateTimer.Enabled = true;
            updateTimer.Start();

            // --------------------------------------------------------------------------------------------------------
        }

        // ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
        //
        // Timers
        //
        // ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

        private void readyTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                // is vna ready?
                if (Program.vna.app.Ready)
                {
                    // yes... vna is ready
                    comConnectionState = ComConnectionStateEnum.CONNECTED_VNA_READY;
                }
                else
                {
                    // no... vna is not ready
                    comConnectionState = ComConnectionStateEnum.CONNECTED_VNA_NOT_READY;
                }
            }
            catch (COMException)
            {
                // com connection has been lost
                comConnectionState = ComConnectionStateEnum.NOT_CONNECTED;
                Application.Exit();
                return;
            }

            if (comConnectionState != previousComConnectionState)
            {
                previousComConnectionState = comConnectionState;

                switch (comConnectionState)
                {
                    default:
                    case ComConnectionStateEnum.NOT_CONNECTED:

                        // update vna info text box
                        toolStripStatusLabelVnaInfo.ForeColor = Color.White;
                        toolStripStatusLabelVnaInfo.BackColor = Color.Red;
                        toolStripStatusLabelSpacer.BackColor = toolStripStatusLabelVnaInfo.BackColor;
                        toolStripStatusLabelVnaInfo.Text = "VNA NOT CONNECTED";

                        // disable ui
                        panelMain.Enabled = false;

                        break;

                    case ComConnectionStateEnum.CONNECTED_VNA_NOT_READY:

                        // update vna info text box
                        toolStripStatusLabelVnaInfo.ForeColor = Color.White;
                        toolStripStatusLabelVnaInfo.BackColor = Color.Red;
                        toolStripStatusLabelSpacer.BackColor = toolStripStatusLabelVnaInfo.BackColor;
                        toolStripStatusLabelVnaInfo.Text = "VNA NOT READY";

                        // disable ui
                        panelMain.Enabled = false;

                        break;

                    case ComConnectionStateEnum.CONNECTED_VNA_READY:

                        // get vna info
                        Program.vna.PopulateInfo(Program.vna.app.NAME);

                        // update vna info text box
                        toolStripStatusLabelVnaInfo.ForeColor = SystemColors.ControlText;
                        toolStripStatusLabelVnaInfo.BackColor = SystemColors.Control;
                        toolStripStatusLabelSpacer.BackColor = toolStripStatusLabelVnaInfo.BackColor;
                        toolStripStatusLabelVnaInfo.Text = Program.vna.modelString + "   " + "SN:" + Program.vna.serialNumberString + "   " + Program.vna.versionString;

                        // enable ui
                        panelMain.Enabled = true;

                        break;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------------------

        private void updateTimer_Tick(object sender, EventArgs e)
        {
            if (comConnectionState == ComConnectionStateEnum.CONNECTED_VNA_READY)
            {
                // are we wait state?
                if (currentState == StateEnum.WAIT)
                {
                    // yes...
                    try
                    {
                        object err;

                        // make sure selected trace is active
                        err = Program.vna.app.SCPI.CALCulate[selectedChannel].PARameter[selectedTrace].SELect;

                        // get the sweep status
                        string sweepStatus = "";
                        if (Program.vna.family == VnaFamilyEnum.S2)
                        {
                            sweepStatus = Program.vna.app.SCPI.TRIGger.SEQuence.STATus;
                        }
                        else if (Program.vna.family == VnaFamilyEnum.TR)
                        {
                            sweepStatus = Program.vna.app.SCPI.TRIGger.SEQuence.STATe;
                        }
                        else
                        {
                            sweepStatus = Program.vna.app.SCPI.TRIGger.SEQuence.STATus;
                        }

                        // has sweep completed?
                        if (sweepStatus != "MEAS")
                        {
                            // yes...
                            // what is the limit status of the selected trace?
                            bool hasFailed = Program.vna.app.SCPI.CALCulate[selectedChannel].SELected.LIMit.FAIL;
                            if (hasFailed == true)
                            {
                                // limit failed...

                                // initiate another single trigger
                                err = Program.vna.app.SCPI.INITiate[selectedChannel].IMMediate;
                                err = Program.vna.app.SCPI.TRIGger.SEQuence.IMMediate;
                            }
                            else
                            {
                                // limit passed...

                                // select hold state
                                currentState = StateEnum.HOLD;
                            }
                        }
                    }
                    catch (COMException)
                    {
                    }
                }
                else
                {
                    // no... update the channel combo box
                    if ((comboBoxChannel.DroppedDown == false) &&
                        (comboBoxTrace.DroppedDown == false))
                    {
                        updateChanComboBox();
                    }
                }
            }
        }

        // ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
        //
        // Channel
        //
        // ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

        private void updateChanComboBox()
        {
            // save previously selected channel index
            int selectedChannelIndex = comboBoxChannel.SelectedIndex;

            // prevent combo box from flickering when update occurs
            comboBoxChannel.BeginUpdate();

            // clear channel selection combo box
            comboBoxChannel.Items.Clear();

            long splitIndex = 0;
            long activeChannel = 0;
            try
            {
                // get the split index (needed to determine number of channels)
                splitIndex = Program.vna.app.SCPI.DISPlay.SPLit;

                // determine the active channel
                activeChannel = Program.vna.app.SCPI.SERVice.CHANnel.ACTive;
            }
            catch (COMException)
            {
            }

            // determine number of channels from the split index
            int numOfChannels = Program.vna.DetermineNumberOfChannels(splitIndex);

            // populate the channel number combo box
            for (int ch = 1; ch < numOfChannels + 1; ch++)
            {
                comboBoxChannel.Items.Add(ch.ToString());
            }

            if ((selectedChannelIndex == -1) ||
                (selectedChannelIndex >= comboBoxChannel.Items.Count))
            {
                // init channel selection to the active channel
                comboBoxChannel.Text = activeChannel.ToString();
            }
            else
            {
                // restore previous channel selection
                comboBoxChannel.SelectedIndex = selectedChannelIndex;
            }

            // prevent combo box from flickering when update occurs
            comboBoxChannel.EndUpdate();
        }

        private void chanComboBox_SelectedIndexChanged(object sender, EventArgs args)
        {
            // save previously selected trace index
            int selectedTraceIndex = comboBoxTrace.SelectedIndex;

            // has channel selection changed?
            if (selectedChannel != comboBoxChannel.SelectedIndex + 1)
            {
                // yes... update selected channel
                selectedChannel = comboBoxChannel.SelectedIndex + 1;

                // and make sure we're in init state
                if (currentState != StateEnum.INIT)
                {
                    currentState = StateEnum.INIT;
                }
            }

            long numOfTraces = 1;
            long activeTrace = 0;
            try
            {
                // get number of traces for this channel
                numOfTraces = Program.vna.app.SCPI.CALCulate[selectedChannel].PARameter.COUNt;

                // determine the active trace for this channel
                activeTrace = Program.vna.app.SCPI.SERVice.CHANnel[selectedChannel].TRACe.ACTive;
            }
            catch (COMException)
            {
            }

            // prevent combo box from flickering when update occurs
            comboBoxTrace.BeginUpdate();

            // clear trace selection combo box
            comboBoxTrace.Items.Clear();

            // loop thru all traces on the selected channel
            for (int tr = 1; tr < numOfTraces + 1; tr++)
            {
                string traceMeasParameter = "";
                try
                {
                    // get this trace's measurement parameter
                    traceMeasParameter = Program.vna.app.SCPI.CALCulate[selectedChannel].PARameter[tr].DEFine;
                }
                catch (COMException)
                {
                }

                // populate trace selection combo box
                comboBoxTrace.Items.Add(tr.ToString() + " " + "(" + traceMeasParameter + ")");
            }

            // prevent combo box from flickering when update occurs
            comboBoxTrace.EndUpdate();

            if ((selectedTraceIndex == -1) ||
                (selectedTraceIndex >= comboBoxTrace.Items.Count))
            {
                // init trace selection to the active trace
                comboBoxTrace.SelectedIndex = (int)activeTrace - 1;
            }
            else
            {
                // restore previous trace selection
                comboBoxTrace.SelectedIndex = selectedTraceIndex;
            }
        }

        private void traceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            // has trace selection changed?
            if (selectedTrace != comboBoxTrace.SelectedIndex + 1)
            {
                // yes... update selected trace
                selectedTrace = comboBoxTrace.SelectedIndex + 1;

                // and make sure we're in init state
                if (currentState != StateEnum.INIT)
                {
                    currentState = StateEnum.INIT;
                }
            }
        }

        private void startStopButton_Click(object sender, EventArgs args)
        {
            // are we in wait state?
            if (currentState == StateEnum.WAIT)
            {
                // yes... 'stop' button was pressed
                // select init state
                currentState = StateEnum.INIT;
            }
            else
            {
                // no... 'start' or 'restart' button was pressed
                bool isLimitTestEnabled = false;
                try
                {
                    object err;

                    // make selected trace active
                    err = Program.vna.app.SCPI.CALCulate[selectedChannel].PARameter[selectedTrace].SELect;

                    // get limit test enabled state for the selected trace
                    isLimitTestEnabled = Program.vna.app.SCPI.CALCulate[selectedChannel].SELected.LIMit.STATe;

                    // is limit test enabled?
                    if (isLimitTestEnabled == false)
                    {
                        // no... enabled limit test
                        Program.vna.app.SCPI.CALCulate[selectedChannel].SELected.LIMit.STATe = true;
                    }

                    // select wait state
                    currentState = StateEnum.WAIT;

                    // initiate a single trigger
                    err = Program.vna.app.SCPI.INITiate[selectedChannel].IMMediate;
                    err = Program.vna.app.SCPI.TRIGger.SEQuence.IMMediate;
                }
                catch (COMException e)
                {
                    // display error message
                    showMessageBoxForComException(e);
                    return;
                }
            }
        }

        // ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

        private void updateUi()
        {
            switch (currentState)
            {
                default:
                case StateEnum.INIT:
                    {
                        // init
                        labelUserMessage.Text = "Select a Trace and Click Start";
                        labelUserMessage.ForeColor = Color.DarkBlue;
                        buttonStartStop.Text = "&Start";
                        labelChannel.Enabled = true;
                        comboBoxChannel.Enabled = true;
                        labelTrace.Enabled = true;
                        comboBoxTrace.Enabled = true;
                    }
                    break;

                case StateEnum.WAIT:
                    {
                        // wait
                        labelUserMessage.Text = "Waiting for Limit Test to Pass...";
                        labelUserMessage.ForeColor = Color.Red;
                        buttonStartStop.Text = "&Stop";
                        labelChannel.Enabled = false;
                        comboBoxChannel.Enabled = false;
                        labelTrace.Enabled = false;
                        comboBoxTrace.Enabled = false;
                    }
                    break;

                case StateEnum.HOLD:
                    {
                        // hold
                        labelUserMessage.Text = "Limit Test Passed. Holding...";
                        labelUserMessage.ForeColor = Color.Green;
                        buttonStartStop.Text = "&Restart";
                        labelChannel.Enabled = true;
                        comboBoxChannel.Enabled = true;
                        labelTrace.Enabled = true;
                        comboBoxTrace.Enabled = true;
                    }
                    break;
            }
        }

        private void updateTriggerState()
        {
            try
            {
                switch (currentState)
                {
                    default:
                        {
                            // do nothing
                        }
                        break;

                    case StateEnum.INIT:
                        {
                            // init
                            // set trigger source to internal
                            Program.vna.app.SCPI.TRIGger.SEQuence.SOURce = "INTernal";

                            // turn on continuous trigger mode
                            Program.vna.app.SCPI.INITiate[selectedChannel].CONTinuous = true;
                        }
                        break;

                    case StateEnum.WAIT:
                        {
                            // wait
                            // set trigger source to bus
                            Program.vna.app.SCPI.TRIGger.SEQuence.SOURce = "BUS";

                            // turn off continuous trigger mode
                            Program.vna.app.SCPI.INITiate[selectedChannel].CONTinuous = false;
                        }
                        break;

                    case StateEnum.HOLD:
                        {
                            // hold
                            // set trigger source to bus
                            Program.vna.app.SCPI.TRIGger.SEQuence.SOURce = "BUS";

                            // turn off continuous trigger mode
                            Program.vna.app.SCPI.INITiate[selectedChannel].CONTinuous = false;
                        }
                        break;
                }
            }
            catch (COMException)
            {
            }
        }

        // ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::

        private void showMessageBoxForComException(COMException e)
        {
            MessageBox.Show(Program.vna.GetUserMessageForComException(e),
                Program.programName,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // ::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
    }
}