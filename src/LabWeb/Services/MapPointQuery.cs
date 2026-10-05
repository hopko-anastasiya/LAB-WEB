using LabWeb.Api.Dtos;
using LabWeb.Models;

namespace LabWeb.Services;

public static class MapPointQuery
{
    public static IQueryable<MapPointDto> ToMapPoints(this IQueryable<Point> points) =>
        points.Select(p => new
            {
                Point = p,
                Last = p.Reports.OrderByDescending(r => r.CreatedAt).FirstOrDefault()
            })
            .Select(x => new MapPointDto(
                x.Point.Id,
                x.Point.Name,
                x.Point.Address,
                x.Point.Settlement,
                x.Point.Latitude,
                x.Point.Longitude,
                x.Point.Status,
                x.Point.Schedule,
                x.Point.Capacity,
                x.Last == null ? null : x.Last.Occupancy,
                x.Last == null ? null : x.Last.HasElectricity,
                x.Last == null ? null : x.Last.HasInternet,
                x.Last == null ? null : x.Last.HasHeating,
                x.Point.UpdatedAt,
                x.Point.Services.Select(s => s.Id).ToList()));
}
