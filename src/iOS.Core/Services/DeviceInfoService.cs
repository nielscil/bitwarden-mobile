﻿using Bit.App.Abstractions;
using Foundation;
using LocalAuthentication;
using UIKit;

namespace Bit.iOS.Core.Services
{
    public class DeviceInfoService : IDeviceInfoService
    {
        public string Type => Xamarin.Forms.Device.iOS;
        public string Model => UIDevice.CurrentDevice.Model;
        public int Version
        {
            get
            {
                var versionParts = UIDevice.CurrentDevice.SystemVersion.Split('.');
                if(versionParts.Length > 0 && int.TryParse(versionParts[0], out int version))
                {
                    return version;
                }

                // unable to determine version
                return -1;
            }
        }
        public float Scale => (float)UIScreen.MainScreen.Scale;
        public bool NfcEnabled => CoreNFC.NFCNdefReaderSession.ReadingAvailable;
        public bool HasCamera => true;
        public bool AutofillServiceSupported => false;
        public bool HasFaceIdSupport
        {
            get
            {
                if(Version < 11)
                {
                    return false;
                }

                var context = new LAContext();
                if(!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out NSError e))
                {
                    return false;
                }

                return context.BiometryType == LABiometryType.FaceId;
            }
        }
    }
}
