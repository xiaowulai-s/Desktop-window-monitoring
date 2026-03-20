using System;
using System.Globalization;
using System.Windows.Data;

namespace WindowMonitor.UI.Converters
{
    public class HwndMatchConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return false;

            if (values[0] == null || values[1] == null)
                return false;

            // Compare two IntPtr values
            if (values[0] is IntPtr hwnd1 && values[1] is IntPtr hwnd2)
            {
                return hwnd1 == hwnd2;
            }

            // Handle the case where TargetWindow.Hwnd might be compared with the row's Hwnd
            try
            {
                var hwndStr1 = values[0].ToString();
                var hwndStr2 = values[1].ToString();
                return hwndStr1 == hwndStr2;
            }
            catch
            {
                return false;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
