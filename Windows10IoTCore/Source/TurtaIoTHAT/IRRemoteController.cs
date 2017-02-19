﻿/* Turta® IoT HAT Helper for Windows® 10 IoT Core
 * Copyright © 2017 Turta
 * Distributed under the terms of the MIT license.
 */

using System;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

namespace TurtaIoTHAT
{
    #region Events

    // Data for Events
    public class IRRemoteDataEventArgs : EventArgs
    {
        private byte[] remoteData;

        // Constructor
        public IRRemoteDataEventArgs(byte[] remoteData)
        {
            this.remoteData = remoteData;
        }

        public byte[] RemoteData
        {
            get { return remoteData; }
        }
    }

    // Delegate Decleration
    public delegate void IRRemoteDataEventHandler(object sender, IRRemoteDataEventArgs e);

    #endregion

    public class IRRemoteController : IDisposable
    {
        #region Globals

        // I2C Device
        private I2cDevice mcuIR = null;

        // Interrupt Pin
        private static GpioPin irInt;

        // I2C Slave Address
        internal const byte MCU_I2C_ADDRESS = 0x28;

        // Registers
        private const byte IR_RECEPTION = 0x02;
        private const byte IR_READ_NEC = 0x40;
        private const byte IR_WRITE_NEC_4BYTE = 0x50;

        #endregion

        #region Constructor

        /// <summary>
        /// Initiates the IR remote transreceiver function to send and receive commands in NEC protocol.
        /// </summary>
        public IRRemoteController()
        {
            // Initiate the transreceiver function.
            Initialize();
            
            // Initiate the GPIO Controller.
            GpioController gpioController = GpioController.GetDefault();

            // Configure the pin.
            irInt = gpioController.OpenPin(18);
            irInt.SetDriveMode(GpioPinDriveMode.InputPullDown);
            irInt.DebounceTimeout = new TimeSpan(100000);
            irInt.ValueChanged += IR_Int_ValueChanged;
        }

        #endregion

        #region I2CCom

        /// <summary>
        /// Initiates the transreceiver function.
        /// </summary>
        private async void Initialize()
        {
            try
            {
                I2cConnectionSettings settings = new I2cConnectionSettings(MCU_I2C_ADDRESS);

                settings.BusSpeed = I2cBusSpeed.FastMode;
                settings.SharingMode = I2cSharingMode.Shared;

                DeviceInformationCollection dis = await DeviceInformation.FindAllAsync(I2cDevice.GetDeviceSelector("I2C1"));

                mcuIR = await I2cDevice.FromIdAsync(dis[0].Id, settings);
            }
            catch (Exception e)
            {
                throw;
            }
        }

        /// <summary>
        /// Writes data to the I2C device.
        /// </summary>
        /// <param name="data">Address and data.</param>
        private async void WriteRegister(byte[] data)
        {
            mcuIR.Write(data);
            await Task.Delay(1);
        }

        /// <summary>
        /// Reads data from the I2C device.
        /// </summary>
        /// <param name="reg">Read address.</param>
        /// <returns>Register data.</returns>
        private byte[] ReadRegister_FourBytes(byte reg)
        {
            byte[] writeBuffer = new byte[] { reg };
            byte[] readBuffer = new byte[] { 0x00, 0x00, 0x00, 0x00 };

            mcuIR.WriteRead(writeBuffer, readBuffer);

            return readBuffer;
        }

        #endregion

        #region Function Configuration

        /// <summary>
        /// Enables or disables onboard IR decoder.
        /// </summary>
        /// <param name="enabled">True for IR decoding enabled. False for IR decoding disabled.</param>
        public async Task SetIRReception(bool enabled)
        {
            WriteRegister(new byte[] { IR_RECEPTION, (enabled ? (byte)0x01 : (byte)0x00) });
            await Task.Delay(1);
        }

        #endregion

        #region IR Communication

        /// <summary>
        /// Transmits 4 Bytes of command using NEC protocol.
        /// </summary>
        /// <param name="cmd">4 Bytes HEX code array. System transmits only if array is 4 Bytes long.</param>
        public void Send4Byte(byte[] cmd)
        {
            if (cmd.Length == 4)
                WriteRegister(new byte[] { IR_WRITE_NEC_4BYTE, cmd[0], cmd[1], cmd[2], cmd[3] });
        }

        /// <summary>
        /// Reads the last IR command decoded.
        /// </summary>
        /// <returns>4 Bytes long remote code.</returns>
        public byte[] GetLastCommand()
        {
            return ReadRegister_FourBytes(IR_READ_NEC);
        }

        #endregion

        #region Interrupts

        private void IR_Int_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            if (args.Edge == GpioPinEdge.RisingEdge)
            {
                byte[] remoteData = ReadRegister_FourBytes(IR_READ_NEC);

                IRRemoteDataEventArgs ea = new IRRemoteDataEventArgs(remoteData);
                OnIRRemoteDataReceive(ea);
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Notifies on incoming IR command transmission.
        /// </summary>
        public event IRRemoteDataEventHandler IRRemoteDataReceived;

        protected virtual void OnIRRemoteDataReceive(IRRemoteDataEventArgs e)
        {
            IRRemoteDataEventHandler handler = IRRemoteDataReceived;
            if (handler != null) handler(this, e);
        }

        #endregion

        #region Disposal

        /// <summary>
        /// Cleans up the resources.
        /// </summary>
        public void Dispose()
        {
            StopIRReception();
            mcuIR.Dispose();

            irInt.ValueChanged += IR_Int_ValueChanged;
            irInt.Dispose();
        }

        /// <summary>
        /// Stops the IR reception for disposal.
        /// </summary>
        private async void StopIRReception()
        {
            await SetIRReception(false);
        }

        #endregion
    }
}
