using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CompressedVideoService.utility
{
    internal static class HashUtility
    {
        /// <summary>
        /// 计算文件的哈希值。
        /// </summary>
        /// <param name="filePath">文件路径。</param>
        /// <param name="algorithm">哈希算法（默认使用MD5）。</param>
        /// <returns>文件的哈希值（十六进制字符串）。</returns>
        public static string ComputeFileHash(string filePath, HashAlgorithm algorithm = null)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"文件未找到: {filePath}");

            using (var stream = File.OpenRead(filePath))
            {
                algorithm = algorithm ?? MD5.Create();
                var hashBytes = algorithm.ComputeHash(stream);
                return BytesToHexString(hashBytes);
            }
        }

        /// <summary>
        /// 校验文件的哈希值是否与给定的哈希值匹配。
        /// </summary>
        /// <param name="filePath">文件路径。</param>
        /// <param name="expectedHash">预期的哈希值（十六进制字符串）。</param>
        /// <param name="algorithm">哈希算法（默认使用MD5）。</param>
        /// <returns>如果匹配返回true，否则返回false。</returns>
        public static bool VerifyFileHash(string filePath, string expectedHash, HashAlgorithm algorithm = null)
        {
            if (string.IsNullOrEmpty(expectedHash))
                throw new ArgumentException("预期的哈希值不能为空。");

            var actualHash = ComputeFileHash(filePath, algorithm);
            return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 将字节数组转换为十六进制字符串。
        /// </summary>
        /// <param name="bytes">字节数组。</param>
        /// <returns>十六进制字符串。</returns>
        private static string BytesToHexString(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.Append(b.ToString("x2")); // 每个字节转换为两位十六进制
            }
            return sb.ToString();
        }
    }
}
