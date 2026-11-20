// Fronts & Rivals: formats the F9 position snippet for fronts.json. Pure, unit-tested.
using System.Globalization;

namespace FrontsAndRivals.Dev
{
    public static class DevFormat
    {
        /// <summary>{ "x": 1.23, "y": -4.56, "z": 7.89, "h": 180.0 } with a dot decimal on every system locale.</summary>
        public static string PositionJson(float x, float y, float z, float heading)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            float h = heading % 360f;
            if (h < 0) h += 360f;
            return "{ \"x\": " + x.ToString("0.00", c)
                 + ", \"y\": " + y.ToString("0.00", c)
                 + ", \"z\": " + z.ToString("0.00", c)
                 + ", \"h\": " + h.ToString("0.0", c) + " }";
        }
    }
}
