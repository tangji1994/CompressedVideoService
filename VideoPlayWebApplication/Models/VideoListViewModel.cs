namespace VideoPlayWebApplication.Models
{
    public class VideoListViewModel
    {
        public List<Video> Videos { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }

        // 搜索参数
        public string UserName { get; set; }
        public string SupplierName { get; set; }
        public string ProductModelName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // 分页显示辅助属性
        public bool HasPreviousPage => CurrentPage > 1;
        public bool HasNextPage => CurrentPage < TotalPages;
    }

}
