using System;
using System.Windows;

namespace OmniPdf.App.Core
{
    public class MeasurementScale
    {
        public double PointsPerUnit { get; set; } = 72.0; // 72 points = 1 inch
        public string UnitName { get; set; } = "in";     // in, ft, mm, cm, m
        public double RealWorldUnitsPerInch { get; set; } = 1.0;
    }

    public static class EngineeringMeasurementService
    {
        public static (double RealDistance, string Formatted) MeasureDistance(Point p1, Point p2, MeasurementScale scale)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double distancePoints = Math.Sqrt(dx * dx + dy * dy);

            // Convert points to inches then real-world units
            double inches = distancePoints / 72.0;
            double realDist = inches * scale.RealWorldUnitsPerInch;

            return (realDist, $"{realDist:0.00} {scale.UnitName}");
        }

        public static (double RealArea, string Formatted) MeasureRectangleArea(Point p1, Point p2, MeasurementScale scale)
        {
            double wPoints = Math.Abs(p2.X - p1.X);
            double hPoints = Math.Abs(p2.Y - p1.Y);

            double wReal = (wPoints / 72.0) * scale.RealWorldUnitsPerInch;
            double hReal = (hPoints / 72.0) * scale.RealWorldUnitsPerInch;
            double area = wReal * hReal;

            return (area, $"{area:0.00} sq {scale.UnitName}");
        }

        public static double MeasureAngle(Point p1, Point p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double rad = Math.Atan2(dy, dx);
            double deg = rad * (180.0 / Math.PI);
            return (deg + 360.0) % 360.0;
        }
    }
}
