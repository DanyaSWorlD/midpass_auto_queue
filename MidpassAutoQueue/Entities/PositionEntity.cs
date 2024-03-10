namespace MidpassAutoQueue.Entities;

public class PositionEntity
{
    public int Id { get; set; }

    public int Position { get; set; }

    public DateTime PositionDate { get; set; }

    public double MovementPerDay { get; set; }
}
