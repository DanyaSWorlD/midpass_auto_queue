using LiteDB;

using Microsoft.Playwright;

using MidpassAutoQueue.Entities;

using System.Text.RegularExpressions;

namespace MidpassAutoQueue;

public class QueueService
{
    private const string NA = "Неизвестно";
    private const string DB = "statistics.db";

    public async Task<QueueSatateMessage> GetQueueStateMessage(IResponse? response)
    {
        if (response is null)
            return new(NA, NA, NA);

        var position = await ExtractPosition(response);

        using var db = new LiteDatabase(DB);
        var positions = db.GetCollection<PositionEntity>();

        var now = DateTime.UtcNow;
        var first = positions.Query().FirstOrDefault();

        if (first is null)
        {
            positions.Insert(new PositionEntity()
            {
                Position = position,
                PositionDate = now,
                MovementPerDay = 0,
            });

            return new(position.ToString(), NA, NA);
        }

        var timeDiff = now - first.PositionDate;
        var posDiff = first.Position - position;

        var movementPerDay = posDiff / timeDiff.TotalDays;

        positions.Insert(new PositionEntity()
        {
            Position = position,
            PositionDate = now,
            MovementPerDay = movementPerDay,
        });

        if (movementPerDay <= 0)
            return new(position.ToString(), NA, NA);

        var daysUntil = (int)Math.Ceiling(position / movementPerDay);
        var newDate = (DateTime.Now + TimeSpan.FromDays(daysUntil)).ToString("dd.MM.yy");

        return new(position.ToString(), daysUntil.ToString(), newDate);
    }

    private async Task<int> ExtractPosition(IResponse response)
    {
        var body = System.Text.Encoding.Default.GetString(await response.BodyAsync());
        var regex = new Regex("\"PlaceInQueue\":([0-9]+),");
        var placeInQueue = regex.Match(body).Groups.Values.Last();
        return int.Parse(placeInQueue.Value);
    }

    public record QueueSatateMessage(string Position, string DaysUntil, string ExpectedDate);
}
