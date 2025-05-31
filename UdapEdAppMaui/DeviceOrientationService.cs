#if IOS
using UIKit;

namespace UdapEdAppMaui
{
    public class DeviceOrientationService : IDeviceOrientationService
    {
        public UIDeviceOrientation GetOrientation()
        {
            var orientation = UIDevice.CurrentDevice.Orientation;
            return orientation;
        }
    }
}

public interface IDeviceOrientationService
{
    UIDeviceOrientation GetOrientation();
}

#endif