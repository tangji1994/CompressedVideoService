using System;
using System.Globalization;
using System.Text.RegularExpressions;
using VideoPlayWebApplication.Models;

namespace VideoPlayWebApplication.Services
{
    public static class FileParser
    {
        public static DateTime ParseDateTimeFromFileName(string fileName)
        {
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

            // 使用更严格的正则表达式，确保匹配完整的日期时间部分
            var match = Regex.Match(
                fileNameWithoutExt,
                // @"^.*?(\d{8})_?(\d{6}).*?$", // 允许前后有其他字符
                @"^.*?(\d{8})(\d{6}).*?$",
                RegexOptions.Compiled // 预编译提升性能
            );

            if (match.Success && match.Groups.Count >= 3)
            {
                string datePart = match.Groups[1].Value;
                string timePart = match.Groups[2].Value;
                string fullDateTimeStr = datePart + timePart;

                if (DateTime.TryParseExact(
                    fullDateTimeStr,
                    "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var result))
                {
                    return result;
                }
                else
                {
                    // 可选：记录解析失败的日志
                    // Console.WriteLine($"解析失败：无效日期时间 {fullDateTimeStr}");
                }
            }
            return DateTime.MinValue;
        }

        public static string ParseUserNameFromFileName(string fileName)
        {
            // 去除扩展名并按 '_' 分割
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string[] parts = fileNameWithoutExt.Split('_');

            // 确保分割后的部分足够多
            if (parts.Length >= 5)
            {
                string username = parts[1]; // 第二个部分是用户名
                return username;
            }
            return "未知用户";
        }

        public static string ParseSupplierNameFromDirectoryName(FileInfo fileInfo)
        {
            string directoryName = Path.GetFileName(Path.GetDirectoryName(fileInfo.FullName)) ?? "unknown_unknown_unknown";
            string[] parts = directoryName.Split('_');

            // 确保分割后的部分足够多
            if (parts.Length == 3)
            {
                string supplierName = parts[1];
                return supplierName;
            }
            return "未知供应商名";
        }

        public static string ParseProductModelNameFromDirectoryName(FileInfo fileInfo)
        {
            string directoryName = Path.GetFileName(Path.GetDirectoryName(fileInfo.FullName)) ?? "unknown_unknown_unknown";
            string[] parts = directoryName.Split('_');

            // 确保分割后的部分足够多
            if (parts.Length == 3)
            {
                string productModelName = parts[2];
                return productModelName;
            }
            return "未知型号名";
        }

        public static Video CreateVideoFromPath(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var dirInfo = Directory.GetParent(filePath);

                return new Video
                {
                    FileName = fileInfo.Name,
                    FilePath = filePath,
                    UserName = ParseUserNameFromFileName(filePath),
                    RecordedTime = ParseDateTimeFromFileName(fileInfo.Name),
                    ProductModelName = ParseProductModelNameFromDirectoryName(fileInfo),
                    SupplierName = ParseSupplierNameFromDirectoryName(fileInfo),
                    CreatedAt = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                // 记录创建失败但允许继续运行
                return null;
            }
        }
    }
}
