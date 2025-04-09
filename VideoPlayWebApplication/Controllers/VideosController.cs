using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VideoPlayWebApplication.Data;
using VideoPlayWebApplication.Models;

namespace VideoPlayWebApplication.Controllers
{
    public class VideosController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public VideosController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }


        public async Task<IActionResult> Index(
            string username,
            string suppliername,
            string productmodelname,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1)  // 添加分页参数，默认第一页
        {
            const int pageSize = 30;  // 每页数据量
            ViewBag.WatchedFolder = _config["WatchedFolder"];
            ViewBag.Users = await _context.Videos
                .Select(v => v.UserName)
                .Distinct()
                .ToListAsync();
            ViewBag.SelectedUser = username;

            ViewBag.SupplierNames = await _context.Videos
                .Select(v => v.SupplierName)
                .Distinct()
                .ToListAsync();
            ViewBag.SelectedSupplierName = suppliername;

            ViewBag.ProductModelNames = await _context.Videos
                .Select(v => v.ProductModelName)
                .Distinct()
                .ToListAsync();
            ViewBag.SelectedProductModelName = productmodelname;

            var query = _context.Videos.AsQueryable();
            
            // 当没有搜索条件时，默认显示当天数据
            bool hasSearch = !string.IsNullOrEmpty(username) || string.IsNullOrEmpty(suppliername) || string.IsNullOrEmpty(productmodelname) || startDate.HasValue || endDate.HasValue;
            if (!hasSearch)
            {
                var today = DateTime.Today;
                startDate = new DateTime(2025, 4, 1);
                endDate = today.AddDays(1).AddTicks(-1);
            }

            if (!string.IsNullOrEmpty(username))
            {
                query = query.Where(v => v.UserName == username);
            }
            if (!string.IsNullOrEmpty(suppliername)) {
                query = query.Where(v => v.SupplierName == suppliername);
            }
            if (!string.IsNullOrEmpty(productmodelname)) {
                query = query.Where(v => v.ProductModelName == productmodelname);
            }
            if (startDate.HasValue)
            {
                query = query.Where(v => v.RecordedTime.Date >= startDate.Value.Date);
            }
            if (endDate.HasValue)
            {
                query = query.Where(v => v.RecordedTime.Date <= endDate.Value.Date);
            }

            // 计算总数和分页信息
            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

            // 规范页码输入
            page = Math.Max(1, Math.Min(page, totalPages));

            // 应用分页
            var pagedVideos = await query
                //.OrderByDescending(v => v.RecordedTime)
                //.OrderByDescending(v => v.RecordedTime)
                .OrderBy(v => v.RecordedTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 构造分页视图模型
            var viewModel = new VideoListViewModel
            {
                Videos = pagedVideos,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize,
                UserName = username,
                SupplierName = suppliername,
                ProductModelName = productmodelname,
                StartDate = startDate,
                EndDate = endDate
            };

            return View(viewModel);
        }

    }
}
