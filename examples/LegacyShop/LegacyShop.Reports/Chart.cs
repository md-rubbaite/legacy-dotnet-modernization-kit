using System.Drawing;

namespace LegacyShop.Reports;

public static class Chart
{
    public static Bitmap Render(int width, int height) => new(width, height);
}
