using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// 命名空间：视频压缩服务的实体层
namespace CompressedVideoService.entity
{
    internal class VideoFileEntity
    {
        // 核心属性
        public int Id { get; set; }                  // 主键标识（通常用于数据库）
        public string FilePath { get; set; }         // 原始文件物理路径（如：/uploads/video.mp4）
        public string Hash { get; set; }             // 文件哈希值（用于校验文件唯一性/完整性）

        // 状态控制
        public int Status { get; set; }              // 处理状态（建议用枚举，例如 0=待处理,1=压缩中,2=已完成,3=失败）
        public DateTime? CompressedDate { get; set; } // 压缩完成时间（可为空）
        public DateTime? ArchivedDate { get; set; }  // 归档完成时间（可为空）

        // 输出信息
        public string OutputPath { get; set; }       // 压缩后文件输出路径（如：/compressed/video_480p.mp4）
        public string ErrorMessage { get; set; }     // 错误信息记录（当Status=3时存储异常堆栈）

        // 系统审计
        public DateTime? LastModified { get; internal set; } // 最后修改时间（内部维护）
        public int? Version { get; internal set; }           // 数据版本号（用于乐观锁并发控制）
    }
}

