namespace HaversineShared;

public interface IHaversineFormula
{
    double Reference(double X0, double Y0, double X1, double Y1, double EarthRadius = 6372.8);
}

public class HaversineFormula : IHaversineFormula
{
    private double Square(double A)
    {
        double Result = (A*A);
        return Result;
    }

    private double RadiansFromDegrees(double Degrees)
    {
        double Result = 0.01745329251994329577 * Degrees;
        return Result;
    }

    public double Reference(double X0, double Y0, double X1, double Y1, double EarthRadius = 6372.8)
    {
        // NOTE(kstandbridge): Intentionally not a "good" way to calculate this.

        double Lat1 = Y0;
        double Lat2 = Y1;
        double Lon1 = X0;
        double Lon2 = X1;

        double dLat = RadiansFromDegrees(Lat2 - Lat1);
        double dLon = RadiansFromDegrees(Lon2 - Lon1);
        Lat1 = RadiansFromDegrees(Lat1);
        Lat2 = RadiansFromDegrees(Lat2);

        double A = Square(Math.Sin(dLat/2.0)) + Math.Cos(Lat1)*Math.Cos(Lat2)*Square(Math.Sin(dLon/2));
        double C = 2.0f*Math.Asin(Math.Sqrt(A));

        double Result = EarthRadius * C;

        return Result;

    }

}
