using LabWeb.Models;

namespace LabWeb.Api.Dtos;

public record MapPointDto(
    int Id,
    string Name,
    string Address,
    string Settlement,
    double Latitude,
    double Longitude,
    PointStatus Status,
    string? Schedule,
    int Capacity,
    int? Occupancy,
    bool? HasElectricity,
    bool? HasInternet,
    bool? HasHeating,
    DateTime UpdatedAt,
    IReadOnlyList<int> ServiceIds);
