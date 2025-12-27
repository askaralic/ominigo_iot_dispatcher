using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver.GeoJsonObjectModel;

namespace DispatcherService.Models;

[BsonIgnoreExtraElements]
public class VehicleLiveDocument
{
    [BsonElement("vehicle_uno")]
    public long VehicleUno { get; set; }

    [BsonElement("company_uno")]
    public long CompanyUno { get; set; }

    [BsonElement("status_uno")]
    public int? StatusUno { get; set; }

    [BsonElement("loc")]
    public GeoJsonPoint<GeoJson2DGeographicCoordinates>? Loc { get; set; }

    [BsonElement("lat")]
    public double? Latitude { get; set; }

    [BsonElement("lon")]
    public double? Longitude { get; set; }

    [BsonElement("distance_meters")]
    public double? DistanceMeters { get; set; }
}
