using System.ComponentModel.DataAnnotations;

namespace RideMatching.Api.DTOs;

public class UpdateLocationRequest
{
    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }
}
