//
// Authors: Nathaniel McCallum <npmccallum@redhat.com>
//
// Copyright (C) 2017  Nathaniel McCallum, Red Hat
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Bluetooth;
using Windows.Storage.Streams;

namespace Jelling
{
    class ApplicationCtx : ApplicationContext
    {
        private static Guid SVC_UUID = Guid.Parse("B670003C-0079-465C-9BA7-6C0539CCD67F");
        private static Guid CHR_UUID = Guid.Parse("F4186B06-D796-4327-AF39-AC22C50BDCA8");
        private static Regex DIGITS = new Regex("^[0-9]+$");

        private static GattLocalCharacteristicParameters CHR_PARAMS = new GattLocalCharacteristicParameters
        {
            WriteProtectionLevel = GattProtectionLevel.Plain,
            CharacteristicProperties = GattCharacteristicProperties.Write
                                     | GattCharacteristicProperties.ReliableWrites
                                     | GattCharacteristicProperties.ExtendedProperties,
        };

        private static GattServiceProviderAdvertisingParameters ADV_PARAMS = new GattServiceProviderAdvertisingParameters
        {
            IsConnectable = true,
            IsDiscoverable = true
        };

        [STAThread]
        static void Main()
        {
            // Ensure that this process is a singleton.
            string proc = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
            Process[] RunningProcesses = Process.GetProcessesByName(proc);
            if (RunningProcesses.Length != 1)
                Application.Exit();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new ApplicationCtx());
        }

        private NotifyIcon notifyIcon = new NotifyIcon(new System.ComponentModel.Container())
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath),
            ContextMenuStrip = new ContextMenuStrip(),
            Text = Application.ProductName,
            Visible = true,
        };

        private GattLocalCharacteristic localCharacteristic;
        private GattServiceProvider serviceProvider;

        ApplicationCtx()
        {
            var exit = new ToolStripMenuItem("&Exit");
            notifyIcon.ContextMenuStrip.Items.Add(exit);

            exit.Click += (sender, args) =>
            {
                if (serviceProvider != null &&
                    serviceProvider.AdvertisementStatus == GattServiceProviderAdvertisementStatus.Started)
                    serviceProvider.StopAdvertising();

                notifyIcon.Visible = false;
                Application.Exit();
            };

            SetupGatt();
        }

        private async void SetupGatt()
        {
            var ba = await BluetoothAdapter.GetDefaultAsync();
            if (ba == null)
            {
                notifyIcon.Text = String.Format("{0} (No Bluetooth adapter found!)", Application.ProductName);
                return;
            }

            if (!ba.IsPeripheralRoleSupported)
            {
                notifyIcon.Text = String.Format("{0} (Peripheral mode not supported!)", Application.ProductName);
                return;
            }

            var sr = await GattServiceProvider.CreateAsync(SVC_UUID);
            if (sr.Error != BluetoothError.Success)
            {
                notifyIcon.Text = String.Format("{0} (Error creating service!)", Application.ProductName);
                return;
            }
            serviceProvider = sr.ServiceProvider;

            var cr = await serviceProvider.Service.CreateCharacteristicAsync(CHR_UUID, CHR_PARAMS);
            if (cr.Error != BluetoothError.Success) {
                notifyIcon.Text = String.Format("{0} (Error creating characteristic!)", Application.ProductName);
                return;
            }
            localCharacteristic = cr.Characteristic;

            localCharacteristic.WriteRequested += CharacteristicWriteRequested;
            serviceProvider.StartAdvertising(ADV_PARAMS);
        }

        private async void CharacteristicWriteRequested(GattLocalCharacteristic glc, GattWriteRequestedEventArgs args)
        {
            var def = args.GetDeferral();
            var req = await args.GetRequestAsync();

            var rdr = DataReader.FromBuffer(req.Value);
            var str = rdr.ReadString(req.Value.Length);
            if (DIGITS.IsMatch(str))
                SendKeys.SendWait(str + "{ENTER}");

            if (req.Option == GattWriteOption.WriteWithResponse)
                req.Respond();

            def.Complete();
        }
    }
}
