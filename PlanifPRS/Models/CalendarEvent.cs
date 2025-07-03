namespace PlanifPRS.Models
{
    public class CalendarEvent
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Start { get; set; } // Format ISO 8601 (ex: "2025-06-15T08:00:00")
        public string End { get; set; }   // Format ISO 8601
        public string Color { get; set; }
        public string Type { get; set; }
    }
}
