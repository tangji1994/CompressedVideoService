namespace VideoPlayWebApplication.Models
{
    public class Video
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string UserName { get; set; }
        public string SupplierName { get; set; }
        public string ProductModelName { get; set; }
        public DateTime RecordedTime { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
